using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2PathHelper;

internal sealed class ModConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("hide_from_multiplayer_mod_list")]
    public bool HideFromMultiplayerModList { get; set; } = true;

    public static ModConfig Load(string path)
    {
        ModConfig defaults = new();
        try
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                ModConfig? parsed = JsonSerializer.Deserialize<ModConfig>(json, JsonOptions);
                if (parsed != null)
                {
                    return parsed;
                }
            }
        }
        catch
        {
        }

        defaults.Write(path);
        return defaults;
    }

    public void Write(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(this, JsonOptionsIndented);
        File.WriteAllText(path, json);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions JsonOptionsIndented = new()
    {
        WriteIndented = true
    };
}
