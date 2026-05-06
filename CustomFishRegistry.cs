using BepInEx.Logging;

namespace SledFishMod;

public static class CustomFishRegistry
{
    // Runtime ID threshold — all custom fish hash to values >= 2^20 (1,048,576),
    // safely above any vanilla FishType enum value.
    public const int CustomIdThreshold = 1 << 20;

    // Default spawn weights by rarity tier. Pack authors may override via spawnWeight field.
    public static readonly IReadOnlyDictionary<string, float> RarityDefaults =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            { "Common",    1.00f },
            { "Uncommon",  0.50f },
            { "Rare",      0.25f },
            { "UltraRare", 0.15f },
            { "Legendary", 0.08f },
        };

    private static readonly Dictionary<int, CustomFishDef> _fishById = new();
    private static float _totalWeight;
    private const float VanillaWeight = 20.0f;

    public static float VanillaPoolWeight => VanillaWeight;
    public static float TotalWeight => _totalWeight;
    public static bool HasAny => _fishById.Count > 0;
    public static IReadOnlyCollection<CustomFishDef> All => _fishById.Values;

    // Tracks the custom fish ID chosen by GetRandomFishType for the current FishData
    // construction chain, so downstream patches (GetRandomSize, GetRandomRarity, etc.)
    // can skip the original logic without relying on reading a partially-constructed instance.
    public static int PendingCustomFishId { get; private set; }

    public static void SetPending(int id) => PendingCustomFishId = id;
    public static void ClearPending() => PendingCustomFishId = 0;

    public static void Clear()
    {
        _fishById.Clear();
        _totalWeight = 0f;
    }

    /// <summary>
    /// Load a fish pack into the registry. Does NOT clear existing fish — call Clear() first
    /// if doing a full reload. Skips any fish whose GUID hashes to a runtime ID already taken
    /// by another pack (collision logged as error).
    /// </summary>
    public static void LoadPack(FishDatabase db, string packDir, ManualLogSource log)
    {
        var packId = db.PackId;
        var loaded = 0;
        var skipped = 0;

        foreach (var def in db.Fish)
        {
            if (string.IsNullOrWhiteSpace(def.Guid))
            {
                log.LogWarning($"[{packId}] Skipping '{def.Name}': missing 'guid' field.");
                skipped++;
                continue;
            }

            int runtimeId;
            try { runtimeId = GuidToRuntimeId(def.Guid); }
            catch (Exception ex)
            {
                log.LogWarning($"[{packId}] Skipping '{def.Name}': invalid GUID '{def.Guid}' — {ex.Message}");
                skipped++;
                continue;
            }

            if (_fishById.TryGetValue(runtimeId, out var existing))
            {
                log.LogError(
                    $"[{packId}] GUID collision! '{def.Name}' (guid={def.Guid}) " +
                    $"hashes to runtime ID {runtimeId}, already taken by " +
                    $"'{existing.Name}' from pack '{existing.PackId}'. Skipping.");
                skipped++;
                continue;
            }

            // Resolve spawn weight: explicit override > rarity default > fallback 0.5
            def.ResolvedSpawnWeight = def.SpawnWeight
                ?? (RarityDefaults.TryGetValue(def.Rarity, out var d) ? d : 0.50f);

            // Resolve image path
            var imageName = !string.IsNullOrWhiteSpace(def.Image)
                ? def.Image
                : def.Name.Replace(" ", "_").Replace("'", "") + ".png";
            def.ImagePath = Path.Combine(packDir, "FishImages", imageName);

            def.RuntimeId = runtimeId;
            def.PackId = packId;

            _fishById[runtimeId] = def;
            _totalWeight += def.ResolvedSpawnWeight;
            loaded++;
        }

        log.LogInfo($"[{packId}] Loaded {loaded} fish" +
                    (skipped > 0 ? $", skipped {skipped}" : "") + ".");
    }

    public static bool IsCustom(int id) => id >= CustomIdThreshold && _fishById.ContainsKey(id);

    public static CustomFishDef Get(int id) =>
        _fishById.TryGetValue(id, out var def) ? def : null;

    public static CustomFishDef GetWeightedRandom(float roll)
    {
        var cumulative = 0f;
        foreach (var def in _fishById.Values)
        {
            cumulative += def.ResolvedSpawnWeight;
            if (roll <= cumulative)
                return def;
        }
        return _fishById.Count > 0 ? _fishById.Values.Last() : null;
    }

    /// <summary>
    /// Hashes a GUID string to a stable positive runtime integer >= 2^20.
    /// Uses FNV-1a over the 16 raw GUID bytes.
    /// </summary>
    public static int GuidToRuntimeId(string guid)
    {
        var bytes = System.Guid.Parse(guid).ToByteArray();
        uint hash = 2166136261u; // FNV offset basis
        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= 16777619u; // FNV prime
        }
        // Mask to positive int, then OR in the threshold bit so result >= 2^20
        return (int)(hash & 0x7FFFFFFFu) | CustomIdThreshold;
    }
}
