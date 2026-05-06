using HarmonyLib;
using UnityEngine;

namespace SledFishMod.Patches;

[HarmonyPatch(typeof(CustomizationOptionsHandler), "GetFishInfo")]
public static class CustomizationOptionsHandler_GetFishInfo_Patch
{
    private static readonly Dictionary<int, ItemDisplayInformation> _cache = new();

    [HarmonyPrefix]
    static bool Prefix(CustomizationOptionsHandler __instance, FishType fishType, ref ItemDisplayInformation __result)
    {
        var id = (int)fishType;
        if (!CustomFishRegistry.IsCustom(id)) return true;

        if (!_cache.TryGetValue(id, out var info))
        {
            info = BuildInfo(__instance, id);
            _cache[id] = info;
        }

        __result = info;
        return false;
    }

    static ItemDisplayInformation BuildInfo(CustomizationOptionsHandler handler, int id)
    {
        var def = CustomFishRegistry.Get(id);

        // Clone a vanilla fish entry so itemDisplayType and other flags are set correctly.
        // Bass (FishType = 1) is always present, safe to use as template.
        var template = handler.GetFishInfo(FishType.Bass);
        var info = template != null
            ? UnityEngine.Object.Instantiate(template)
            : ScriptableObject.CreateInstance<ItemDisplayInformation>();

        info.fishType = (FishType)id;
        info.optionName = def.Name;
        info.optionDescription = "";
        info.showInShop = false;

        var sprite = SpriteLoader.Load(id);
        if (sprite != null)
            info.optionImage = sprite;

        return info;
    }
}

// FishDataToString only stores the fish size tier, not the exact length.
// For custom fish, append the exact length so ParseFishDataFromString can restore it.
[HarmonyPatch(typeof(FishData), "FishDataToString")]
public static class FishData_FishDataToString_Patch
{
    [HarmonyPostfix]
    static void Postfix(FishData fishData, ref string __result)
    {
        if (fishData == null || !CustomFishRegistry.IsCustom((int)fishData.fishType)) return;
        __result += $";customLen={fishData.length:R}";
    }
}

// Restores the exact length for custom fish through the serialize/deserialize cycle.
// The Prefix strips our ;customLen= tag BEFORE vanilla parsing runs (otherwise the tag
// corrupts the bool field parse and the method throws/returns a default FishData).
// The Postfix re-applies the saved length after vanilla has constructed the object.
[HarmonyPatch(typeof(FishData), "ParseFishDataFromString")]
public static class FishData_ParseFishDataFromString_Patch
{
    [ThreadStatic] private static float _savedLen;
    [ThreadStatic] private static bool _hasSavedLen;

    [HarmonyPrefix]
    static void Prefix(ref string __0)
    {
        _hasSavedLen = false;
        const string marker = ";customLen=";
        var idx = __0.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return;
        var lenStr = __0.Substring(idx + marker.Length);
        if (float.TryParse(lenStr, System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out var len))
        {
            _savedLen = len;
            _hasSavedLen = true;
        }
        __0 = __0.Substring(0, idx); // clean string before vanilla parsing
    }

    [HarmonyPostfix]
    static void Postfix(ref FishData __result)
    {
        if (!_hasSavedLen || __result == null) return;
        _hasSavedLen = false;
        if (!CustomFishRegistry.IsCustom((int)__result.fishType)) return;
        __result.length = _savedLen;
    }
}

// Skip the original entirely for custom fish — it crashes on missing RarityWeight entries.
// This is also where we clear PendingCustomFishId since rarity is resolved last.
[HarmonyPatch(typeof(FishData), "GetRandomRarity")]
public static class FishData_GetRandomRarity_Patch
{
    [HarmonyPrefix]
    static bool Prefix(ref FishRarity __result)
    {
        var id = CustomFishRegistry.PendingCustomFishId;
        if (id == 0) return true;

        var def = CustomFishRegistry.Get(id);
        CustomFishRegistry.ClearPending();

        if (def == null) return true;

        if (Enum.TryParse<FishRarity>(def.Rarity, ignoreCase: true, out var rarity))
            __result = rarity;
        else
            __result = FishRarity.Common;

        return false;
    }
}
