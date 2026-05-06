using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SledFishMod.FishDiary.Patches;

/// <summary>
/// Injects a "Fish Diary" button into the TabbedMenu (the E-menu).
///
/// Strategy:
///   1. Postfix on TabbedMenu.Awake — finds the existing tab button container and clones
///      one button to create the diary entry.
///   2. If that fails for any reason (TabbedMenu layout changed, no buttons found, etc.)
///      a fallback floating button is shown whenever TabbedMenu is active.
/// </summary>
[HarmonyPatch(typeof(TabbedMenu), "Awake")]
public static class TabbedMenu_Awake_Patch
{
    private static bool _injected;

    [HarmonyPostfix]
    static void Postfix(TabbedMenu __instance)
    {
        if (_injected || __instance == null) return;

        try
        {
            if (TryInjectTabButton(__instance))
            {
                _injected = true;
                Plugin.Log.LogInfo("[FishDiary] Diary button injected into TabbedMenu.");
                return;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[FishDiary] Tab injection failed: {ex.Message}. Using floating button.");
        }

        // Fallback: create a floating button positioned at top-right
        CreateFloatingButton(__instance.gameObject);
        _injected = true;
    }

    // ── Primary injection ─────────────────────────────────────────────────

    private static bool TryInjectTabButton(TabbedMenu menu)
    {
        // Find any Button in the TabbedMenu hierarchy
        var buttons = menu.GetComponentsInChildren<Button>(true);
        if (buttons == null || buttons.Length == 0)
        {
            Plugin.Log.LogWarning("[FishDiary] TabbedMenu has no Button children — falling back.");
            return false;
        }

        // Clone the first button we find (assumed to be a tab button)
        var original = buttons[0];
        var clone = GameObject.Instantiate(original.gameObject, original.transform.parent, false);
        clone.name = "DiaryTabButton";

        // Clear all existing onClick listeners and add ours
        var cloneBtn = clone.GetComponent<Button>();
        cloneBtn.onClick.RemoveAllListeners();
        cloneBtn.onClick.AddListener(new Action(DiaryUi.Toggle));

        // Replace icon or label
        var diarySprite = DiaryUi.LoadDiarySprite();
        var imgs = clone.GetComponentsInChildren<Image>(true);
        if (diarySprite != null && imgs != null && imgs.Length > 0)
            imgs[0].sprite = diarySprite;

        // Update any text label
        var texts = clone.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (texts != null && texts.Length > 0)
            texts[0].text = "Diary";

        return true;
    }

    // ── Fallback floating button ──────────────────────────────────────────

    private static void CreateFloatingButton(GameObject menuGo)
    {
        // Attach the floating button to the menu so it moves with it (and hides with it)
        var btnGo = new GameObject("DiaryFloatingButton");
        btnGo.transform.SetParent(menuGo.transform, false);

        var rt = btnGo.AddComponent<RectTransform>();
        // Position at top-right of the menu
        rt.anchorMin  = new Vector2(1, 1);
        rt.anchorMax  = new Vector2(1, 1);
        rt.pivot      = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-5, -5);
        rt.sizeDelta  = new Vector2(110, 36);

        var img = btnGo.AddComponent<Image>();
        img.color = new Color(0.15f, 0.25f, 0.40f, 0.90f);

        var btn = btnGo.AddComponent<Button>();
        btn.onClick.AddListener(new Action(DiaryUi.Toggle));

        // Sprite
        var diarySprite = DiaryUi.LoadDiarySprite();
        if (diarySprite != null)
        {
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(btnGo.transform, false);
            var iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0, 0);
            iconRt.anchorMax = new Vector2(0, 1);
            iconRt.offsetMin = new Vector2(4, 4);
            iconRt.offsetMax = new Vector2(32, -4);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = diarySprite;
            iconImg.preserveAspect = true;
        }

        // Label
        var lblGo = new GameObject("Label");
        lblGo.transform.SetParent(btnGo.transform, false);
        var lblRt = lblGo.AddComponent<RectTransform>();
        lblRt.anchorMin = new Vector2(0, 0);
        lblRt.anchorMax = new Vector2(1, 1);
        lblRt.offsetMin = new Vector2(diarySprite != null ? 34 : 4, 2);
        lblRt.offsetMax = new Vector2(-4, -2);
        var lbl = lblGo.AddComponent<TextMeshProUGUI>();
        lbl.text = "Fish Diary";
        lbl.fontSize = 12;
        lbl.color = Color.white;
        lbl.alignment = TextAlignmentOptions.MidlineLeft;

        Plugin.Log.LogInfo("[FishDiary] Floating diary button created as fallback.");
    }
}
