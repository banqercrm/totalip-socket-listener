using System.ComponentModel.DataAnnotations;

namespace Banqer.TotalIpSocketListener.Settings;

internal sealed class TotalIpOptions
{
    public const string SectionName = "TotalIp";

    [Required(AllowEmptyStrings = false, ErrorMessage = $"{SectionName}:{nameof(SocketServerHost)} is not set.")]
    public required string SocketServerHost { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = $"{SectionName}:{nameof(SocketServerPort)} is not set.")]
    public required int SocketServerPort { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = $"{SectionName}:{nameof(PublishingApiKey)} is not set.")]
    public required string PublishingApiKey { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = $"{SectionName}:{nameof(PublishingApiBaseAddress)} is not set.")]
    public required string PublishingApiBaseAddress { get; set; }

    public int SocketServerReceiveTimeout { get; set; } = 5 * 60;
}