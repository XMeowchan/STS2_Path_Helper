using System.Text.Json.Serialization;

namespace Sts2PathHelper;

internal sealed class ModManifestInfo
{
    [JsonPropertyName("pck_name")]
    public string PckName { get; set; } = "Sts2PathHelper";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "STS2 Path Helper";
}
