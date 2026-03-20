using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2PathHelper;

internal sealed class ModConfig
{
    public const string PrimaryFileName = "config.cfg";

    public const string LegacyFileName = "config.json";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("hide_from_multiplayer_mod_list")]
    public bool HideFromMultiplayerModList { get; set; } = true;

    public static ModConfig Load(string path, string? legacyPath = null)
    {
        ModConfig defaults = new();
        try
        {
            if (TryRead(path, out ModConfig? parsedPrimary))
            {
                return parsedPrimary;
            }

            if (!string.IsNullOrWhiteSpace(legacyPath) &&
                !Path.GetFullPath(path).Equals(Path.GetFullPath(legacyPath), StringComparison.OrdinalIgnoreCase) &&
                TryRead(legacyPath, out ModConfig? parsedLegacy))
            {
                parsedLegacy.Write(path);
                TryDeleteLegacyConfig(legacyPath);
                return parsedLegacy;
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

    private static bool TryRead(string path, out ModConfig config)
    {
        config = null!;
        if (!File.Exists(path))
        {
            return false;
        }

        string json = File.ReadAllText(path);
        ModConfig? parsed = JsonSerializer.Deserialize<ModConfig>(json, JsonOptions);
        if (parsed == null)
        {
            return false;
        }

        config = parsed;
        return true;
    }

    private static void TryDeleteLegacyConfig(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
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
