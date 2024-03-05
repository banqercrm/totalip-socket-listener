namespace Banqer.TotalIpSocketListener.Contracts;

public sealed record SocketMessage
{
    public required string Content { get; init; }
}