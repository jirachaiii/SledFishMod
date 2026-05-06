using HarmonyLib;
using URandom = UnityEngine.Random;

namespace SledFishMod.Patches;

[HarmonyPatch(typeof(FishData), "GetRandomFishType")]
public static class FishData_GetRandomFishType_Patch
{
    [HarmonyPostfix]
    static void Postfix(ref FishType __result)
    {
        if (!CustomFishRegistry.HasAny)
        {
            CustomFishRegistry.ClearPending();
            return;
        }

        var totalPool = CustomFishRegistry.VanillaPoolWeight + CustomFishRegistry.TotalWeight;
        var roll = URandom.Range(0f, totalPool);

        if (roll < CustomFishRegistry.VanillaPoolWeight)
        {
            CustomFishRegistry.ClearPending();
            return;
        }

        var custom = CustomFishRegistry.GetWeightedRandom(roll - CustomFishRegistry.VanillaPoolWeight);
        if (custom != null)
        {
            __result = (FishType)custom.RuntimeId;
            CustomFishRegistry.SetPending(custom.RuntimeId);
        }
        else
        {
            CustomFishRegistry.ClearPending();
        }
    }
}

// Skip the original entirely for custom fish — it crashes on missing SizeRanges entries.
[HarmonyPatch(typeof(FishData), "GetRandomSize")]
public static class FishData_GetRandomSize_Patch
{
    [HarmonyPrefix]
    static bool Prefix(ref float __result)
    {
        var id = CustomFishRegistry.PendingCustomFishId;
        if (id == 0) return true;

        var def = CustomFishRegistry.Get(id);
        if (def == null) return true;

        __result = URandom.Range(def.MinLength, def.MaxLength);
        return false;
    }
}

// Skip the original entirely for custom fish — it crashes on missing RarityWeight entries.
// GetRandomIsShiny is last in the constructor chain, so we don't clear pending here;
// GetRandomRarity (in InfoPatches) clears it after rarity is resolved.
[HarmonyPatch(typeof(FishData), "GetRandomIsShiny")]
public static class FishData_GetRandomIsShiny_Patch
{
    [HarmonyPrefix]
    static bool Prefix(ref bool __result)
    {
        var id = CustomFishRegistry.PendingCustomFishId;
        if (id == 0) return true;

        var def = CustomFishRegistry.Get(id);
        if (def == null) return true;

        __result = def.CanBeShiny && URandom.value < 0.01f;
        return false;
    }
}

// get_FishSize property does a dictionary lookup by FishType — crashes for custom IDs.
// Property getters work correctly with IL2CPP Harmony (unlike LengthToFishSize which
// has a trampoline issue where __instance comes through as null).
[HarmonyPatch(typeof(FishData), "get_FishSize")]
public static class FishData_FishSize_Patch
{
    [HarmonyPrefix]
    static bool Prefix(FishData __instance, ref FishSize __result)
    {
        if (__instance == null) return true;
        var typeId = (int)__instance.fishType;
        if (!CustomFishRegistry.IsCustom(typeId)) return true;

        var def = CustomFishRegistry.Get(typeId);
        if (def == null) { __result = FishSize.Normal; return false; }

        var range = def.MaxLength - def.MinLength;
        var t = range > 0f ? (__instance.length - def.MinLength) / range : 0.5f;
        t = Math.Clamp(t, 0f, 1f);

        __result = t switch
        {
            < 0.20f => FishSize.Mini,
            < 0.40f => FishSize.Small,
            < 0.60f => FishSize.Normal,
            < 0.80f => FishSize.Large,
            < 0.95f => FishSize.Giant,
            _ => FishSize.BIG,
        };
        return false;
    }
}

// FishSizeInCentimeters is a property that also does the dictionary lookup.
// Patch separately in case the game calls it without going through LengthToFishSize.
[HarmonyPatch(typeof(FishData), "get_FishSizeInCentimeters")]
public static class FishData_FishSizeInCentimeters_Patch
{
    [HarmonyPrefix]
    static bool Prefix(FishData __instance, ref float __result)
    {
        var typeId = (int)__instance.fishType;
        if (!CustomFishRegistry.IsCustom(typeId)) return true;

        __result = __instance.length * 100f;
        return false;
    }
}

// FishGraphics.SetFish uses a MeshRenderer (flat quad) + material per fish type.
// Prefix substitutes Bass so the original runs without crashing on missing cache entries.
// Postfix restores the real type, replaces the quad's texture, and scales for the
// catch animation (FishGraphics is unparented at this point, so lossyScale == 1).
[HarmonyPatch(typeof(FishGraphics), "SetFish")]
public static class FishGraphics_SetFish_Patch
{
    [HarmonyPrefix]
    static void Prefix(FishData fishData, out FishType __state)
    {
        __state = fishData.fishType;
        if (CustomFishRegistry.IsCustom((int)fishData.fishType))
            fishData.fishType = FishType.Bass;
    }

    [HarmonyPostfix]
    static void Postfix(FishGraphics __instance, FishData fishData, FishType __state)
    {
        fishData.fishType = __state;

        if (__instance == null) return;
        var typeId = (int)__state;
        if (!CustomFishRegistry.IsCustom(typeId)) return;

        var sprite = SpriteLoader.Load(typeId);
        if (sprite == null) return;

        var renderer = __instance.fishRenderer;
        if (renderer == null) return;

        var mat = renderer.material;
        if (mat == null) return;
        mat.mainTexture = sprite.texture;

        ApplyWorldScale(__instance.transform, fishData.length, sprite.texture);
    }

    internal static void ApplyWorldScale(UnityEngine.Transform t, float length, UnityEngine.Texture tex)
    {
        var aspect = (tex.width > 0 && tex.height > 0) ? (float)tex.width / tex.height : 1f;
        var scale = t.localScale;
        var signX = scale.x < 0 ? -1f : 1f;
        var signY = scale.y < 0 ? -1f : 1f;
        var lossy = t.lossyScale;
        var effX = Math.Abs(lossy.x) > 1e-4f ? Math.Abs(lossy.x) / Math.Abs(scale.x) : 1f;
        var effY = Math.Abs(lossy.y) > 1e-4f ? Math.Abs(lossy.y) / Math.Abs(scale.y) : 1f;
        scale.y = signY * length / effY;
        scale.x = signX * length * aspect / effX;
        t.localScale = scale;
    }
}

// Re-applies the correct world-scale after the Fish HeldObject is fully parented
// into the player hierarchy (SetFish fires before reparenting, so the scale set
// there gets multiplied down by the player hierarchy's non-identity lossyScale).
[HarmonyPatch(typeof(Fish), "OnSyncFishDataChanged")]
public static class Fish_OnSyncFishDataChanged_Patch
{
    [HarmonyPostfix]
    static void Postfix(Fish __instance, FishData oldValue, FishData newValue, bool asServer)
    {
        if (__instance == null || newValue == null) return;
        var typeId = (int)newValue.fishType;
        if (!CustomFishRegistry.IsCustom(typeId)) return;

        var fishGraphics = __instance.fishGraphics;
        if (fishGraphics == null) return;

        var sprite = SpriteLoader.Load(typeId);
        if (sprite == null) return;

        var renderer = fishGraphics.fishRenderer;
        if (renderer == null) return;

        var mat = renderer.material;
        if (mat == null) return;
        mat.mainTexture = sprite.texture;

        FishGraphics_SetFish_Patch.ApplyWorldScale(fishGraphics.transform, newValue.length, sprite.texture);
    }
}
