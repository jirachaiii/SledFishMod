using HarmonyLib;

namespace SledFishMod.Patches;

// CalculatedSellPrice uses FishWeight[fishType] * SizeWeight[fishSize] * RarityWeight[rarity].
// Custom fish (ID >= 100) have no FishWeight entry, so we intercept and compute a sensible value
// based on rarity and size — keeping it proportional to the vanilla economy.
[HarmonyPatch(typeof(FishData), "get_CalculatedSellPrice")]
public static class FishData_CalculatedSellPrice_Patch
{
    // Approximate base values observed from vanilla fish, scaled by rarity tier.
    private static readonly Dictionary<string, long> RarityBaseValue = new()
    {
        { "Common",    150  },
        { "Uncommon",  400  },
        { "Rare",      1000 },
        { "UltraRare", 3000 },
        { "Legendary", 8000 },
    };

    // Matches the FishSize enum: Mini, Small, Normal, Large, Giant, BIG
    private static readonly float[] SizeMultipliers = { 0.5f, 0.75f, 1.0f, 1.5f, 2.5f, 4.0f };

    [HarmonyPrefix]
    static bool Prefix(FishData __instance, ref long __result)
    {
        var typeId = (int)__instance.fishType;
        if (!CustomFishRegistry.IsCustom(typeId)) return true;

        var rarityName = __instance.rarity.ToString();
        var baseValue = RarityBaseValue.TryGetValue(rarityName, out var v) ? v : 300L;

        var sizeIndex = (int)__instance.FishSize;
        var sizeMult = (sizeIndex >= 0 && sizeIndex < SizeMultipliers.Length)
            ? SizeMultipliers[sizeIndex]
            : 1.0f;

        // Bonus for exceptional raw length (rewards actually catching the big one)
        var lengthBonus = 1.0f + (__instance.length / 10.0f);

        __result = (long)(baseValue * sizeMult * lengthBonus);
        return false;
    }
}
