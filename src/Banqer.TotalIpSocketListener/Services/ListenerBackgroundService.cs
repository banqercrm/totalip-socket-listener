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
    private readonly ResiliencePipeline resiliencePipeline;

    public ListenerBackgroundService(
        IBus bus,
        IOptions<TotalIpOptions> options,
        ILogger<ListenerBackgroundService> log)
    {
        this.bus = bus;
        this.options = options;
        this.log = log;

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
        while (!stoppingToken.IsCancellationRequested)
        {
            await this.resiliencePipeline.ExecuteAsync(ListenAndPublishSocketMessage, stoppingToken);
        }
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
            while (!cancellationToken.IsCancellationRequested)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(this.options.Value.SocketServerReceiveTimeout));
                await using var _ = cancellationToken.Register(cts.Cancel);
                while (await sr.ReadLineAsync(cts.Token) is { } content)
                {
                    // Ignore empty lines.
                    if (string.IsNullOrWhiteSpace(content))
                        continue;
                    this.log.LogInformation("Data Received: {content}", content);
                    await this.bus.Publish(new SocketMessage { Content = content }, cancellationToken);
                }
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