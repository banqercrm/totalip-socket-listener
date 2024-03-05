using Banqer.TotalIpSocketListener.Client;
using Banqer.TotalIpSocketListener.Contracts;
using MassTransit;

namespace Banqer.TotalIpSocketListener.Consumers;

public sealed class SocketMessageConsumerDefinition : ConsumerDefinition<SocketMessageConsumer>
{
    public SocketMessageConsumerDefinition()
    {
#if DEBUG
        this.ConcurrentMessageLimit = 1;
#else 
        this.ConcurrentMessageLimit = 5;
#endif
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<SocketMessageConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r =>
        {
            r.Handle<HttpRequestException>();
            r.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
        });
    }
}

public sealed class SocketMessageConsumer : IConsumer<SocketMessage>
{
    private readonly IPublishingApiClient publishingApiClient;

    public SocketMessageConsumer(IPublishingApiClient publishingApiClient)
    {
        this.publishingApiClient = publishingApiClient;
    }

    public async Task Consume(ConsumeContext<SocketMessage> context)
    {
        var cancellationToken = context.CancellationToken;
        using var stringContent = new StringContent(context.Message.Content);
        using var httpResponseMessage =
            await this.publishingApiClient.HttpClient.PostAsync("api/totalip/socket",
                stringContent, cancellationToken);
        httpResponseMessage.EnsureSuccessStatusCode();
    }
}