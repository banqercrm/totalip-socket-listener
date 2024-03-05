namespace Banqer.TotalIpSocketListener.Client;

public sealed class PublishingApiClient : IPublishingApiClient
{
    public PublishingApiClient(HttpClient httpClient)
    {
        this.HttpClient = httpClient;
    }

    public HttpClient HttpClient { get; }

}