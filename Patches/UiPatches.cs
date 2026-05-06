using HarmonyLib;
using TMPro;

namespace SledFishMod.Patches;

[HarmonyPatch(typeof(UiReferenceController), "GetLocalisedFishType")]
public static class UiReferenceController_GetLocalisedFishType_Patch
{
    [HarmonyPrefix]
    static bool Prefix(FishType fishType, ref string __result)
    {
        var id = (int)fishType;
        if (!CustomFishRegistry.IsCustom(id)) return true;

        var def = CustomFishRegistry.Get(id);
        __result = def?.Name ?? $"Unknown Fish ({id})";
        return false;
    }
}

// FishCaughtPopup sets fishCaughtPopupText then triggers the animation.
// The localization table has no entry for custom FishType IDs so we override
// the text directly after the original runs.
[HarmonyPatch(typeof(UiReferenceController), "FishCaughtPopup")]
public static class UiReferenceController_FishCaughtPopup_Patch
{
    [HarmonyPostfix]
    static void Postfix(UiReferenceController __instance, FishData fishData)
    {
        if (__instance == null || fishData == null) return;
        var typeId = (int)fishData.fishType;
        if (!CustomFishRegistry.IsCustom(typeId)) return;

        var def = CustomFishRegistry.Get(typeId);
        if (def == null) return;

        var popupText = __instance.fishCaughtPopupText;
        if (popupText == null) return;

        string sizeName;
        try { sizeName = __instance.GetLocalisedFishSize(fishData.FishSize); }
        catch { sizeName = fishData.FishSize.ToString(); }

        string lengthStr;
        try { lengthStr = fishData.FishLengthString; }
        catch { lengthStr = $"{fishData.length * 100f:F1} cm"; }

        popupText.text = $"You caught a {sizeName} {def.Name}! ({lengthStr})";
    }
}

// ItemDisplayUiFish.SetFish populates the inventory card. For custom fish the
// localization table lookup fails so we override name and sprite directly.
[HarmonyPatch(typeof(ItemDisplayUiFish), "SetFish")]
public static class ItemDisplayUiFish_SetFish_Patch
{
    [HarmonyPostfix]
    static void Postfix(ItemDisplayUiFish __instance, FishData newFishData)
    {
        if (__instance == null || newFishData == null) return;
        var typeId = (int)newFishData.fishType;
        if (!CustomFishRegistry.IsCustom(typeId)) return;

        var def = CustomFishRegistry.Get(typeId);
        if (def == null) return;

        // Override name text
        var nameText = __instance.fishNameText;
        if (nameText != null)
        {
            string sizeName;
            try
            {
                var uiCtrl = UiReferenceController.Instance;
                sizeName = uiCtrl != null
                    ? uiCtrl.GetLocalisedFishSize(newFishData.FishSize)
                    : newFishData.FishSize.ToString();
            }
            catch { sizeName = newFishData.FishSize.ToString(); }

            nameText.text = $"{sizeName} {def.Name}";
        }

        // Override sprite
        var sprite = SpriteLoader.Load(typeId);
        if (sprite != null)
        {
            var img = __instance.fishImage;
            if (img != null) img.sprite = sprite;
        }
    }
}
