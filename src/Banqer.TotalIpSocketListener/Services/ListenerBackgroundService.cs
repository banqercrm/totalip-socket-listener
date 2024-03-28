using System.Net;
using System.Net.Sockets;
using Banqer.TotalIpSocketListener.Contracts;
using Banqer.TotalIpSocketListener.Exceptions;
using Banqer.TotalIpSocketListener.Settings;
using MassTransit;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Banqer.TotalIpSocketListener.Services;

internal sealed class ListenerBackgroundService : BackgroundService
{
    private readonly IBus bus;
    private readonly IOptions<TotalIpOptions> options;
    private readonly ILogger<ListenerBackgroundService> log;
    private readonly TimeProvider timeProvider;
    private readonly ResiliencePipeline resiliencePipeline;

    public ListenerBackgroundService(
        IBus bus,
        IOptions<TotalIpOptions> options,
        ILogger<ListenerBackgroundService> log,
        TimeProvider timeProvider)
    {
        this.bus = bus;
        this.options = options;
        this.log = log;
        this.timeProvider = timeProvider;

        this.resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                Delay = TimeSpan.FromSeconds(1),
                MaxRetryAttempts = int.MaxValue
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(10),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(30),
            })
            .Build();
    }

    private static async Task<IPEndPoint> ResolveHostEndpoint(string host, int port, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        if (addresses.Length == 0)
        {
            throw new UnableToResolveHostException();
        }

        return new IPEndPoint(addresses[0], port);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            var currentTime = this.timeProvider.GetLocalNow().DateTime;
            var timeOfDay = currentTime.TimeOfDay;
            this.log.LogDebug("Checking the time of day: {currentTime} (Local time). Current TimeOfDay: {timeOfDay}.", currentTime, timeOfDay);
            if (timeOfDay < this.options.Value.WorkingHoursStart || timeOfDay > this.options.Value.WorkingHoursEnd)
            {
                this.log.LogDebug("Current time {currentTime} is outside of working hours (Start: {start}, End: {end}). Skipping this cycle.", currentTime, this.options.Value.WorkingHoursStart, this.options.Value.WorkingHoursEnd);
                continue;
            }

            this.log.LogInformation("Within working hours. Starting to listen and publish socket messages.");
            await this.resiliencePipeline.ExecuteAsync(this.ListenAndPublishSocketMessage, stoppingToken);
            this.log.LogInformation("Server stream process completed successfully at {currentTime}.", currentTime);

        } while (await timer.WaitForNextTickAsync(stoppingToken));
        this.log.LogWarning("ListenerBackgroundService is exiting due to the cancellation request or the end of the timer.");
    }

    private async ValueTask ListenAndPublishSocketMessage(CancellationToken cancellationToken)
    {
        var host = this.options.Value.SocketServerHost;
        var port = this.options.Value.SocketServerPort;
        try
        {
            var remoteEndpoint = await ResolveHostEndpoint(host, port, cancellationToken);
            using var client = new Socket(remoteEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            await client.ConnectAsync(remoteEndpoint, cancellationToken);
            this.log.LogInformation("Connected to Server {Host}:{Port}.", remoteEndpoint.Address, remoteEndpoint.Port);
            await using var ns = new NetworkStream(client);
            using var sr = new StreamReader(ns);
            var tsTimeout = TimeSpan.FromSeconds(this.options.Value.SocketServerReceiveTimeout);
            this.log.LogDebug("SocketServerReceiveTimeout: {tsTimeout}", tsTimeout);
            using var cts = new CancellationTokenSource(tsTimeout);
            await using var _ = cancellationToken.Register(cts.Cancel);
            this.log.LogInformation("Enter main read loop, waiting content...");
            while (await sr.ReadLineAsync(cts.Token) is { } content)
            {
                // Ignore empty lines.
                if (string.IsNullOrWhiteSpace(content))
                {
                    this.log.LogDebug("Empty line ignored.");
                    continue;
                }
                this.log.LogInformation("Data Received: {content}", content);
                await this.bus.Publish(new SocketMessage { Content = content }, cancellationToken);
            }
        }
        catch (SocketException sex)
        {
            log.LogError("Unable to connect to socket server {host}:{port}, {message}", host, port, sex.Message);
            throw;
        }
        catch (UnableToResolveHostException)
        {
            log.LogError("Unable to resolve server host {host}.", host);
            throw;
        }
        catch (OperationCanceledException)
        {
            log.LogInformation("Operation Canceled.");
        }
        catch (Exception ex)
        {
            log.LogError("Unexpected Exception, {message}.", $"{ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }
}