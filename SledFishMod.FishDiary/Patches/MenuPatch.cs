using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SledFishMod.FishDiary.Patches;

/// <summary>
/// Tries to inject a diary tab into the E-menu via UiReferenceController.Start.
/// DiaryUi.ShowCornerButton() is always called as a reliable fallback — a small
/// permanent button lives in the corner of the screen and J key always works.
/// </summary>
[HarmonyPatch(typeof(UiReferenceController), "Start")]
public static class UiReferenceController_Start_Patch
{
    private static bool _injected;

    [HarmonyPostfix]
    static void Postfix(UiReferenceController __instance)
    {
        if (_injected || __instance == null) return;
        _injected = true;

        // Corner button is always added — reliable, discoverable
        DiaryUi.Instance?.ShowCornerButton();

        // Also attempt to clone a tab into the E-menu tab bar
        try { TryInjectTabButton(__instance); }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[FishDiary] Tab bar injection failed (non-fatal): {ex.Message}");
        }
    }

    // ── Tab injection ─────────────────────────────────────────────────────

    private static void TryInjectTabButton(UiReferenceController uiCtrl)
    {
        // UiReferenceController has a `tabMenu` field (type TabMenu).
        // Grab it via reflection, then clone one of its child Buttons.
        var field = typeof(UiReferenceController).GetField("tabMenu",
            System.Reflection.BindingFlags.Public   |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (field == null)
        {
            Plugin.Log.LogWarning("[FishDiary] tabMenu field not found on UiReferenceController.");
            return;
        }

        var tabMenuObj = field.GetValue(uiCtrl);
        if (tabMenuObj == null)
        {
            Plugin.Log.LogWarning("[FishDiary] tabMenu field is null.");
            return;
        }

        // Il2CppSystem objects wrap the real UnityEngine.Component
        var comp = tabMenuObj as MonoBehaviour;
        if (comp == null) return;

        var buttons = comp.GetComponentsInChildren<Button>(true);
        if (buttons == null || buttons.Length == 0)
        {
            Plugin.Log.LogWarning("[FishDiary] No Buttons found in tabMenu hierarchy.");
            return;
        }

        var original = buttons[0];
        var clone = GameObject.Instantiate(original.gameObject, original.transform.parent, false);
        clone.name = "DiaryTabButton";

        var cloneBtn = clone.GetComponent<Button>();
        cloneBtn.onClick.RemoveAllListeners();
        cloneBtn.onClick.AddListener(new Action(DiaryUi.Toggle));

        var diarySprite = DiaryUi.LoadDiarySprite();
        if (diarySprite != null)
        {
            var imgs = clone.GetComponentsInChildren<Image>(true);
            if (imgs != null && imgs.Length > 0) imgs[0].sprite = diarySprite;
        }

        var texts = clone.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (texts != null && texts.Length > 0) texts[0].text = "Diary";

        Plugin.Log.LogInfo("[FishDiary] Diary tab cloned into E-menu tab bar.");
    }
}
