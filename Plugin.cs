using System.Reflection;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace SledFishMod;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    public static ManualLogSource Log { get; private set; }

    public override void Load()
    {
        Log = base.Log;
        Log.LogInfo($"SledFishMod {MyPluginInfo.PLUGIN_VERSION} loading...");

        LoadAllFishPacks();

        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        Log.LogInfo("SledFishMod loaded successfully.");
    }

    private void LoadAllFishPacks()
    {
        CustomFishRegistry.Clear();

        // The DLL may sit directly in BepInEx/plugins/ or inside a named subfolder.
        // Scanning both assemblyDir and its parent ensures we find packs regardless of layout:
        //   Direct:    assemblyDir = plugins/        → subdirs include WebFishingFishPack/ ✓
        //   Subfolder: assemblyDir = plugins/SledFishMod/ → parent = plugins/ ✓
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var pluginsDir = Directory.GetParent(assemblyDir)?.FullName ?? assemblyDir;

        // De-duplicated set of directories to search for fish_pack.json
        var searchRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { assemblyDir, pluginsDir };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packFiles = new List<string>();

        foreach (var root in searchRoots)
        {
            // fish_pack.json directly in the root (e.g. alongside the DLL)
            var rootPack = Path.Combine(root, "fish_pack.json");
            if (File.Exists(rootPack) && seen.Add(rootPack))
                packFiles.Add(rootPack);

            // fish_pack.json inside any immediate subdirectory
            foreach (var dir in Directory.GetDirectories(root))
            {
                var packFile = Path.Combine(dir, "fish_pack.json");
                if (File.Exists(packFile) && seen.Add(packFile))
                    packFiles.Add(packFile);
            }
        }

        if (packFiles.Count == 0)
        {
            Log.LogInfo("No fish packs found. Place a folder containing fish_pack.json in BepInEx/plugins/.");
            return;
        }

        foreach (var packFile in packFiles)
            LoadFishPack(packFile);

        LogCatchRates();
    }

    private void LoadFishPack(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip };
            var db = JsonSerializer.Deserialize<FishDatabase>(json, options);
            if (db == null)
            {
                Log.LogError($"Failed to parse fish pack (null result): {path}");
                return;
            }
            var packDir = Path.GetDirectoryName(path)!;
            Log.LogInfo($"Loading fish pack '{db.PackName}' ({db.PackId}) from {packDir}");
            CustomFishRegistry.LoadPack(db, packDir, Log);
        }
        catch (Exception ex)
        {
            Log.LogError($"Error loading fish pack '{path}': {ex.Message}");
        }
    }

    private void LogCatchRates()
    {
        if (!CustomFishRegistry.HasAny) return;

        var total = CustomFishRegistry.VanillaPoolWeight + CustomFishRegistry.TotalWeight;
        Log.LogInfo($"--- Catch rates (total pool: {total:F2}) ---");
        Log.LogInfo($"  [vanilla] ~{CustomFishRegistry.VanillaPoolWeight / total * 100f:F2}%");
        foreach (var def in CustomFishRegistry.All.OrderByDescending(d => d.ResolvedSpawnWeight))
        {
            Log.LogInfo(
                $"  [{def.PackId}] {def.Name} ({def.Rarity}): " +
                $"{def.ResolvedSpawnWeight / total * 100f:F2}%");
        }
    }
}
