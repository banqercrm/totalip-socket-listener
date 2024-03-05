using Banqer.TotalIpSocketListener.Client;
using Banqer.TotalIpSocketListener.Consumers;
using Banqer.TotalIpSocketListener.Services;
using Banqer.TotalIpSocketListener.Settings;
using MassTransit;
using Microsoft.Extensions.Options;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

var services = builder.Services;
var configuration = builder.Configuration;

configuration.AddEnvironmentVariables();
Environment.SetEnvironmentVariable("BASEDIR", AppContext.BaseDirectory);

// Logging
services.AddLogging(options =>
{
    options.ClearProviders();
    var logger = new LoggerConfiguration()
        .ReadFrom.Configuration(configuration)
        .CreateLogger();
    options.AddSerilog(logger);
});

// Settings
services.AddOptions<TotalIpOptions>()
    .Bind(configuration.GetSection(TotalIpOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// HttpClient
services.AddHttpClient<IPublishingApiClient, PublishingApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<TotalIpOptions>>();
    client.BaseAddress = new Uri(options.Value.PublishingApiBaseAddress);
    client.DefaultRequestHeaders.Add("x-api-key", options.Value.PublishingApiKey);
});

// MassTransit
services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddDelayedMessageScheduler();
    x.AddConsumer<SocketMessageConsumer, SocketMessageConsumerDefinition>();
    x.UsingInMemory((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});

// Hosted Services
builder.Services.AddHostedService<ListenerBackgroundService>();

var host = builder.Build();
host.Run();
