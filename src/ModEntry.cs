using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace Sts2PathHelper;

[ModInitializer("Initialize")]
public static class ModEntry
{
    private static readonly object InitLock = new();

    private static bool _initialized;

    private static Harmony? _harmony;

    public static string ModId { get; private set; } = "Sts2PathHelper";

    public static string ModName { get; private set; } = "STS2 Path Helper";

    internal static string ModDirectory { get; private set; } = string.Empty;

    internal static ModConfig Config { get; private set; } = new();

    public static void Initialize()
    {
        lock (InitLock)
        {
            if (_initialized)
            {
                return;
            }

            ModDirectory = ResolveModDirectory();
            LoadManifest(ResolveManifestPath(ModDirectory));
            Config = ModConfig.Load(
                Path.Combine(ModDirectory, ModConfig.PrimaryFileName),
                Path.Combine(ModDirectory, ModConfig.LegacyFileName));

            if (!Config.Enabled)
            {
                _initialized = true;
                Log.Info($"{ModId}: disabled by {ModConfig.PrimaryFileName}.", 2);
                return;
            }

            _harmony = new Harmony($"cn.codex.sts2.template.{ModId}");
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            _initialized = true;
            Log.Info($"{ModName} loaded from '{ModDirectory}'.", 2);
        }
    }

    private static void LoadManifest(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            string json = File.ReadAllText(path);
            ModManifestInfo? manifest = JsonSerializer.Deserialize<ModManifestInfo>(json, JsonOptions);
            if (manifest == null)
            {
                return;
            }

            string? manifestId = !string.IsNullOrWhiteSpace(manifest.Id)
                ? manifest.Id
                : manifest.LegacyPckName;
            if (!string.IsNullOrWhiteSpace(manifestId))
            {
                ModId = manifestId.Trim();
            }

            if (!string.IsNullOrWhiteSpace(manifest.Name))
            {
                ModName = manifest.Name.Trim();
            }
        }
        catch
        {
        }
    }

    private static string ResolveManifestPath(string modDirectory)
    {
        string assemblyPath = Assembly.GetExecutingAssembly().Location;
        string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
        if (!string.IsNullOrWhiteSpace(assemblyName))
        {
            string newManifestPath = Path.Combine(modDirectory, $"{assemblyName}.json");
            if (File.Exists(newManifestPath))
            {
                return newManifestPath;
            }
        }

        return Path.Combine(modDirectory, "mod_manifest.json");
    }

    private static string ResolveModDirectory()
    {
        string? assemblyLocation = Assembly.GetExecutingAssembly().Location;
        if (!string.IsNullOrWhiteSpace(assemblyLocation))
        {
            string? directory = Path.GetDirectoryName(assemblyLocation);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                return directory;
            }
        }

        return AppContext.BaseDirectory;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
