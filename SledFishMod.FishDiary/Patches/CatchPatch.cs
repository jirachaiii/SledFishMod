using HarmonyLib;

namespace SledFishMod.FishDiary.Patches;

/// <summary>
/// Records every fish catch into the diary store.
/// Hooks the same UiReferenceController.FishCaughtPopup that SledFishMod already patches
/// for name overrides — we just run after it.
/// </summary>
[HarmonyPatch(typeof(UiReferenceController), "FishCaughtPopup")]
public static class CatchPatch
{
    [HarmonyPostfix]
    static void Postfix(FishData fishData)
    {
        if (fishData == null) return;

        var typeId = (int)fishData.fishType;
        string key;

        if (CustomFishRegistry.IsCustom(typeId))
        {
            // Custom fish — key is the stable GUID string
            var def = CustomFishRegistry.Get(typeId);
            if (def == null) return;
            key = def.Guid;
        }
        else
        {
            // Vanilla fish — key is "vanilla_{enumValue}"
            key = $"vanilla_{typeId}";
        }

        FishDiaryStore.RecordCatch(key, fishData.length, fishData.isShiny);

        Plugin.Log.LogDebug($"[FishDiary] Recorded catch: key={key} length={fishData.length:F2}m shiny={fishData.isShiny}");
    }
}
