using System.Reflection;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SledFishMod.FishDiary;

/// <summary>
/// MonoBehaviour that owns the Fish Diary overlay panel.
/// Lives on a DontDestroyOnLoad GameObject created at plugin load.
/// </summary>
public class DiaryUi : MonoBehaviour
{
    // IL2CPP requires this constructor pattern for custom MonoBehaviours.
    public DiaryUi(IntPtr ptr) : base(ptr) { }

    // ── Singleton ─────────────────────────────────────────────────────────
    public static DiaryUi Instance { get; private set; }

    /// <summary>Show or hide the diary panel.</summary>
    public static void Toggle() => Instance?.TogglePanel();

    // ── State ─────────────────────────────────────────────────────────────
    private Transform _canvasTransform;
    private GameObject _diaryGroup;
    private GameObject _panel;
    private TextMeshProUGUI _headerText;
    private Transform _gridContent;
    private bool _panelOpen;
    private GameObject _cornerButton;

    // Cursor state saved on open, restored on close
    private CursorLockMode _savedLockMode;
    private bool _savedCursorVisible;

    private DiaryFilter _filter = DiaryFilter.All;
    private enum DiaryFilter { All, Caught, Uncaught }

    // ── Game-matching colour palette ──────────────────────────────────────
    // Sampled from the fish inventory panel in the screenshot
    private static readonly Color C_PanelBg    = new Color(0.07f, 0.12f, 0.21f, 0.97f); // outer panel
    private static readonly Color C_CardBg     = new Color(0.10f, 0.18f, 0.30f, 1.00f); // card base
    private static readonly Color C_CardBgDim  = new Color(0.07f, 0.13f, 0.22f, 1.00f); // uncaught card
    private static readonly Color C_BtnBg      = new Color(0.12f, 0.20f, 0.34f, 1.00f); // filter button
    private static readonly Color C_BtnActive  = new Color(0.18f, 0.30f, 0.50f, 1.00f); // active filter
    private static readonly Color C_BtnHover   = new Color(0.16f, 0.27f, 0.44f, 1.00f);
    private static readonly Color C_BtnPress   = new Color(0.09f, 0.15f, 0.26f, 1.00f);
    private static readonly Color C_TextPrimary = Color.white;
    private static readonly Color C_TextDim    = new Color(0.65f, 0.75f, 0.85f, 1.00f);
    private static readonly Color C_TextFaint  = new Color(0.45f, 0.55f, 0.68f, 1.00f);
    private static readonly Color C_Backdrop   = new Color(0.02f, 0.04f, 0.08f, 0.78f);
    private static readonly Color C_Divider    = new Color(0.15f, 0.25f, 0.40f, 1.00f);
    private static readonly Color C_CornerBtn  = new Color(0.09f, 0.16f, 0.27f, 0.92f);

    // Rarity badge colours (matches game rarity tiers)
    private static readonly Dictionary<string, Color> RarityColors = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Common",    new Color(0.72f, 0.78f, 0.85f) },
        { "Uncommon",  new Color(0.35f, 0.82f, 0.45f) },
        { "Rare",      new Color(0.30f, 0.62f, 1.00f) },
        { "UltraRare", new Color(0.72f, 0.32f, 1.00f) },
        { "Legendary", new Color(1.00f, 0.72f, 0.08f) },
    };

    private static readonly Dictionary<string, int> RarityOrder = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Legendary", 0 }, { "UltraRare", 1 }, { "Rare", 2 }, { "Uncommon", 3 }, { "Common", 4 },
    };

    private static Sprite _diarySprite;
    private static Sprite _roundedRectSprite;

    // ── Factory ───────────────────────────────────────────────────────────

    public static DiaryUi Create(ManualLogSource log)
    {
        var host = new GameObject("SledFishMod_DiaryUi");
        DontDestroyOnLoad(host);
        var ui = host.AddComponent<DiaryUi>();
        Instance = ui;
        return ui;
    }

    // ── MonoBehaviour lifecycle ───────────────────────────────────────────

    private void Awake() => BuildPanel();

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.J))
            TogglePanel();
        else if (_panelOpen && Input.GetKeyDown(KeyCode.Escape))
            TogglePanel();
    }

    // ── Corner button (persistent HUD button) ─────────────────────────────

    public void ShowCornerButton()
    {
        if (_cornerButton != null) return;

        _cornerButton = new GameObject("DiaryCornerBtn");
        _cornerButton.transform.SetParent(_canvasTransform, false);

        var rt = _cornerButton.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(14f, 14f);
        rt.sizeDelta = new Vector2(138f, 32f);

        var img = _cornerButton.AddComponent<Image>();
        img.sprite = GetRoundedRect();
        img.type = Image.Type.Sliced;
        img.color = C_CornerBtn;

        var btn = _cornerButton.AddComponent<Button>();
        var cols = btn.colors;
        cols.normalColor      = C_CornerBtn;
        cols.highlightedColor = C_BtnHover;
        cols.pressedColor     = C_BtnPress;
        cols.colorMultiplier  = 1f;
        btn.colors = cols;
        btn.targetGraphic = img;
        btn.onClick.AddListener(new Action(TogglePanel));

        // Newspaper icon (left side)
        var sprite = LoadDiarySprite();
        float textLeft = 8f;
        if (sprite != null)
        {
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(_cornerButton.transform, false);
            var irt = iconGo.AddComponent<RectTransform>();
            irt.anchorMin = new Vector2(0, 0);
            irt.anchorMax = new Vector2(0, 1);
            irt.offsetMin = new Vector2(5, 4);
            irt.offsetMax = new Vector2(26, -4);
            var iImg = iconGo.AddComponent<Image>();
            iImg.sprite = sprite;
            iImg.preserveAspect = true;
            textLeft = 29f;
        }

        // Label
        var lbl = MakeTmpText(_cornerButton.transform, "Label", "Fish Diary  [J]", 11f);
        lbl.color = C_TextDim;
        lbl.alignment = TextAlignmentOptions.MidlineLeft;
        var lrt = lbl.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(textLeft, 1); lrt.offsetMax = new Vector2(-6, -1);

        Plugin.Log.LogInfo("[FishDiary] Corner button added.");
    }

    // ── Panel construction ────────────────────────────────────────────────

    private void BuildPanel()
    {
        // Always-active canvas
        var canvasGo = new GameObject("DiaryCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();
        _canvasTransform = canvasGo.transform;

        // Overlay group — only this is toggled on open/close
        _diaryGroup = new GameObject("DiaryGroup");
        _diaryGroup.transform.SetParent(_canvasTransform, false);
        var dgRt = _diaryGroup.AddComponent<RectTransform>();
        Stretch(dgRt);

        // Dim backdrop — click to close
        var backdrop = new GameObject("Backdrop");
        backdrop.transform.SetParent(_diaryGroup.transform, false);
        var bdRt = backdrop.AddComponent<RectTransform>();
        Stretch(bdRt);
        backdrop.AddComponent<Image>().color = C_Backdrop;
        backdrop.AddComponent<Button>().onClick.AddListener(new Action(TogglePanel));

        // ── Main panel ──────────────────────────────────────────────────
        _panel = new GameObject("DiaryPanel");
        _panel.transform.SetParent(_diaryGroup.transform, false);
        var panelRt = _panel.AddComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(900f, 608f);

        var panelImg = _panel.AddComponent<Image>();
        panelImg.sprite = GetRoundedRect();
        panelImg.type = Image.Type.Sliced;
        panelImg.color = C_PanelBg;

        var vl = _panel.AddComponent<VerticalLayoutGroup>();
        vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
        vl.childControlWidth = true;     vl.childControlHeight = true;
        vl.spacing = 0;
        vl.padding = new RectOffset(14, 14, 14, 14);

        // ── Header row ──────────────────────────────────────────────────
        var header = MakeChild(_panel.transform, "Header");
        var hLE = header.AddComponent<LayoutElement>(); hLE.preferredHeight = 40f;
        var hHL = header.AddComponent<HorizontalLayoutGroup>();
        hHL.childForceExpandWidth = false; hHL.childForceExpandHeight = true;
        hHL.childControlWidth = true; hHL.childControlHeight = true;
        hHL.spacing = 8; hHL.padding = new RectOffset(4, 4, 0, 0);

        _headerText = MakeTmpText(header.transform, "Title", "Fish Diary", 17f);
        _headerText.alignment = TextAlignmentOptions.MidlineLeft;
        _headerText.fontStyle = FontStyles.Bold;
        _headerText.GetComponent<LayoutElement>().flexibleWidth = 1f;

        var xBtn = MakeIconButton(header.transform, "CloseBtn", "✕", 32f, 32f, TogglePanel);
        var xLE = xBtn.GetComponent<LayoutElement>();
        xLE.preferredWidth = 32f; xLE.preferredHeight = 32f;

        // Thin divider
        MakeDivider(_panel.transform);

        // ── Filter row ──────────────────────────────────────────────────
        var filterRow = MakeChild(_panel.transform, "FilterRow");
        var fLE = filterRow.AddComponent<LayoutElement>(); fLE.preferredHeight = 30f;
        var fHL = filterRow.AddComponent<HorizontalLayoutGroup>();
        fHL.childForceExpandWidth = false; fHL.childForceExpandHeight = true;
        fHL.childControlWidth = true; fHL.childControlHeight = true;
        fHL.spacing = 6; fHL.padding = new RectOffset(4, 4, 2, 2);

        MakeFilterBtn(filterRow.transform, "All",      DiaryFilter.All);
        MakeFilterBtn(filterRow.transform, "Caught",   DiaryFilter.Caught);
        MakeFilterBtn(filterRow.transform, "Uncaught", DiaryFilter.Uncaught);

        // Thin divider
        MakeDivider(_panel.transform);

        // ── Scroll view ─────────────────────────────────────────────────
        var scrollHost = MakeChild(_panel.transform, "ScrollHost");
        var sLE = scrollHost.AddComponent<LayoutElement>(); sLE.flexibleHeight = 1f;

        var scrollRect = scrollHost.AddComponent<ScrollRect>();
        scrollRect.horizontal = false; scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 40f;

        var viewport = MakeChild(scrollHost.transform, "Viewport");
        var vpRt = viewport.GetComponent<RectTransform>(); Stretch(vpRt);
        viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        var mask = viewport.AddComponent<Mask>(); mask.showMaskGraphic = false;
        scrollRect.viewport = vpRt;

        var content = MakeChild(viewport.transform, "Content");
        var cRt = content.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0, 1); cRt.anchorMax = new Vector2(1, 1);
        cRt.pivot = new Vector2(0.5f, 1f); cRt.sizeDelta = Vector2.zero;

        var grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(164f, 185f);
        grid.spacing = new Vector2(8f, 8f);
        grid.padding = new RectOffset(4, 4, 6, 8);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        grid.childAlignment = TextAnchor.UpperLeft;

        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = cRt;
        _gridContent = content.transform;

        _diaryGroup.SetActive(false);
        _panelOpen = false;
    }

    // ── Open / close ──────────────────────────────────────────────────────

    private void TogglePanel()
    {
        _panelOpen = !_panelOpen;
        _diaryGroup.SetActive(_panelOpen);

        if (_panelOpen)
        {
            // Unlock mouse so player can click diary items
            _savedLockMode    = Cursor.lockState;
            _savedCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;

            FishDiaryStore.ClearNewFlags();
            Refresh();
        }
        else
        {
            // Restore cursor state from before opening
            Cursor.lockState = _savedLockMode;
            Cursor.visible   = _savedCursorVisible;
        }
    }

    // ── Grid ─────────────────────────────────────────────────────────────

    public void Refresh()
    {
        for (var i = _gridContent.childCount - 1; i >= 0; i--)
            Destroy(_gridContent.GetChild(i).gameObject);

        var all      = BuildRows();
        var filtered = _filter switch
        {
            DiaryFilter.Caught   => all.Where(r => r.Entry?.Caught == true).ToList(),
            DiaryFilter.Uncaught => all.Where(r => r.Entry?.Caught != true).ToList(),
            _ => all,
        };

        var total  = all.Count;
        var caught = all.Count(r => r.Entry?.Caught == true);
        var pct    = total > 0 ? (int)(caught * 100f / total) : 0;
        _headerText.text = $"Fish Diary  ·  {caught} / {total}  ({pct}%)";

        foreach (var row in filtered)
            BuildCard(row);
    }

    // ── Row model ─────────────────────────────────────────────────────────

    private sealed class Row
    {
        public string Key, Name, Rarity, PackLabel;
        public int    RuntimeId;
        public DiaryEntry Entry;
    }

    private List<Row> BuildRows()
    {
        var rows = new List<Row>();

        foreach (var def in CustomFishRegistry.All)
        {
            rows.Add(new Row
            {
                Key       = def.Guid,
                Name      = def.Name,
                Rarity    = def.Rarity,
                PackLabel = def.PackId,
                RuntimeId = def.RuntimeId,
                Entry     = FishDiaryStore.GetEntry(def.Guid),
            });
        }

        foreach (var kvp in FishDiaryStore.AllEntries)
        {
            if (kvp.Key.StartsWith("vanilla_") && kvp.Value.Caught)
            {
                rows.Add(new Row
                {
                    Key       = kvp.Key,
                    Name      = kvp.Key.Replace("vanilla_", ""),
                    Rarity    = "Vanilla",
                    PackLabel = "Base Game",
                    Entry     = kvp.Value,
                });
            }
        }

        rows.Sort((a, b) =>
        {
            int oa = RarityOrder.TryGetValue(a.Rarity, out var va) ? va : 5;
            int ob = RarityOrder.TryGetValue(b.Rarity, out var vb) ? vb : 5;
            int c  = oa.CompareTo(ob);
            return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        return rows;
    }

    // ── Card ──────────────────────────────────────────────────────────────

    private void BuildCard(Row row)
    {
        var caught     = row.Entry?.Caught == true;
        var rarityColor = RarityColors.TryGetValue(row.Rarity, out var rc) ? rc : C_TextDim;

        // Card container
        var card = MakeChild(_gridContent, "Card");
        var cardImg = card.AddComponent<Image>();
        cardImg.sprite = GetRoundedRect();
        cardImg.type   = Image.Type.Sliced;
        cardImg.color  = caught ? C_CardBg : C_CardBgDim;

        var vl = card.AddComponent<VerticalLayoutGroup>();
        vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
        vl.childControlWidth = true;     vl.childControlHeight = true;
        vl.spacing = 3;
        vl.padding = new RectOffset(7, 7, 8, 7);

        // ── Fish image ──────────────────────────────────
        var imgHolder = MakeChild(card.transform, "ImgHolder");
        var ihLE = imgHolder.AddComponent<LayoutElement>(); ihLE.preferredHeight = 96f;
        var fishImg = imgHolder.AddComponent<Image>();
        fishImg.preserveAspect = true;

        if (row.RuntimeId > 0)
        {
            var sprite = SpriteLoader.Load(row.RuntimeId);
            if (sprite != null)
            {
                fishImg.sprite = sprite;
                fishImg.color  = caught ? Color.white : new Color(0.1f, 0.1f, 0.15f);
            }
            else
            {
                fishImg.color = new Color(0.12f, 0.18f, 0.26f);
            }
        }
        else
        {
            fishImg.color = new Color(0.12f, 0.18f, 0.26f);
        }

        // ── Fish name ──────────────────────────────────
        var nameTxt = MakeTmpText(card.transform, "Name",
            caught ? row.Name : "???", 12.5f);
        nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.color = caught ? C_TextPrimary : C_TextFaint;
        nameTxt.GetComponent<LayoutElement>().preferredHeight = 18f;

        // ── Rarity pill ────────────────────────────────
        var rarityHolder = MakeChild(card.transform, "RarityHolder");
        var rhLE = rarityHolder.AddComponent<LayoutElement>(); rhLE.preferredHeight = 16f;
        var rhImg = rarityHolder.AddComponent<Image>();
        rhImg.sprite = GetRoundedRect();
        rhImg.type   = Image.Type.Sliced;
        rhImg.color  = new Color(rarityColor.r * 0.22f, rarityColor.g * 0.22f, rarityColor.b * 0.22f);
        var rarityTxt = MakeTmpText(rarityHolder.transform, "Rarity", row.Rarity, 9f);
        rarityTxt.color = rarityColor;
        rarityTxt.alignment = TextAlignmentOptions.Center;
        var rarityRt = rarityTxt.GetComponent<RectTransform>();
        rarityRt.anchorMin = Vector2.zero; rarityRt.anchorMax = Vector2.one;
        rarityRt.offsetMin = Vector2.zero; rarityRt.offsetMax = Vector2.zero;

        // ── Pack label ─────────────────────────────────
        var packTxt = MakeTmpText(card.transform, "Pack", row.PackLabel, 8.5f);
        packTxt.color = C_TextFaint;
        packTxt.alignment = TextAlignmentOptions.Center;
        packTxt.GetComponent<LayoutElement>().preferredHeight = 12f;

        if (caught)
        {
            // ── Best size ─────────────────────────────
            var sizeTxt = MakeTmpText(card.transform, "Size",
                $"{row.Entry.BiggestLength * 100f:F1} cm", 11f);
            sizeTxt.fontStyle = FontStyles.Bold;
            sizeTxt.color = C_TextPrimary;
            sizeTxt.alignment = TextAlignmentOptions.Center;
            sizeTxt.GetComponent<LayoutElement>().preferredHeight = 15f;

            // ── Count + shiny ─────────────────────────
            var stats = $"×{row.Entry.CatchCount}";
            if (row.Entry.HasShiny) stats += "  ★";
            var statsTxt = MakeTmpText(card.transform, "Stats", stats, 9.5f);
            statsTxt.color = row.Entry.HasShiny
                ? new Color(1.00f, 0.85f, 0.20f)
                : C_TextDim;
            statsTxt.alignment = TextAlignmentOptions.Center;
            statsTxt.GetComponent<LayoutElement>().preferredHeight = 13f;
        }

        if (row.Entry?.IsNew == true)
        {
            var newTxt = MakeTmpText(card.transform, "New", "NEW!", 9.5f);
            newTxt.color = new Color(0.40f, 1.00f, 0.55f);
            newTxt.fontStyle = FontStyles.Bold;
            newTxt.alignment = TextAlignmentOptions.Center;
            newTxt.GetComponent<LayoutElement>().preferredHeight = 13f;
        }
    }

    // ── Filter buttons ────────────────────────────────────────────────────

    private void MakeFilterBtn(Transform parent, string label, DiaryFilter mode)
    {
        var captured = mode;
        var go = MakeChild(parent, $"Btn_{mode}");
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 84f; le.preferredHeight = 24f;

        var img = go.AddComponent<Image>();
        img.sprite = GetRoundedRect();
        img.type   = Image.Type.Sliced;
        img.color  = C_BtnBg;

        var btn = go.AddComponent<Button>();
        var cols = btn.colors;
        cols.normalColor      = C_BtnBg;
        cols.highlightedColor = C_BtnHover;
        cols.pressedColor     = C_BtnPress;
        cols.selectedColor    = C_BtnActive;
        cols.colorMultiplier  = 1f;
        btn.colors = cols;
        btn.targetGraphic = img;
        btn.onClick.AddListener(new Action(() =>
        {
            _filter = captured;
            if (_panelOpen) Refresh();
        }));

        var txt = MakeTmpText(go.transform, "Label", label, 11f);
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = C_TextDim;
        var trt = txt.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
    }

    // ── Build helpers ─────────────────────────────────────────────────────

    private static GameObject MakeChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static TextMeshProUGUI MakeTmpText(Transform parent, string name, string text, float size)
    {
        var go = MakeChild(parent, name);
        go.AddComponent<LayoutElement>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = C_TextPrimary;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }

    private static GameObject MakeIconButton(Transform parent, string name, string label,
        float w, float h, Action onClick)
    {
        var go = MakeChild(parent, name);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = w; le.preferredHeight = h;

        var img = go.AddComponent<Image>();
        img.sprite = GetRoundedRect();
        img.type   = Image.Type.Sliced;
        img.color  = new Color(0.18f, 0.10f, 0.10f, 0.80f);

        var btn = go.AddComponent<Button>();
        var cols = btn.colors;
        cols.normalColor      = new Color(0.18f, 0.10f, 0.10f, 0.80f);
        cols.highlightedColor = new Color(0.35f, 0.12f, 0.12f, 1.00f);
        cols.pressedColor     = new Color(0.12f, 0.07f, 0.07f, 1.00f);
        cols.colorMultiplier  = 1f;
        btn.colors = cols;
        btn.targetGraphic = img;
        btn.onClick.AddListener(new Action(onClick));

        var txt = MakeTmpText(go.transform, "Label", label, 13f);
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = new Color(1f, 0.65f, 0.65f);
        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        return go;
    }

    private static void MakeDivider(Transform parent)
    {
        var go = MakeChild(parent, "Divider");
        var le = go.AddComponent<LayoutElement>(); le.preferredHeight = 1f;
        go.AddComponent<Image>().color = C_Divider;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // ── Rounded-rect sprite (9-sliced) ────────────────────────────────────

    private static Sprite GetRoundedRect()
    {
        if (_roundedRectSprite != null) return _roundedRectSprite;

        const int size   = 64;
        const int radius = 14;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = UnityEngine.FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Find nearest corner centre
                float cx = x < radius ? radius : (x >= size - radius ? size - 1 - radius : x);
                float cy = y < radius ? radius : (y >= size - radius ? size - 1 - radius : y);
                bool inCornerZone = (x < radius || x >= size - radius) &&
                                    (y < radius || y >= size - radius);

                byte a = 255;
                if (inCornerZone)
                {
                    float dx   = x - cx;
                    float dy   = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    // Smooth anti-aliased edge
                    a = (byte)(Mathf.Clamp01(radius - dist + 0.75f) * 255f);
                }
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        const int border = 20;
        _roundedRectSprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            1f, 0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));

        return _roundedRectSprite;
    }

    // ── Diary icon (embedded newspaper.png) ──────────────────────────────

    public static Sprite LoadDiarySprite()
    {
        if (_diarySprite != null) return _diarySprite;
        try
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SledFishMod.FishDiary.newspaper.png");
            if (stream == null) return null;

            var bytes = new byte[stream.Length];
            _ = stream.Read(bytes, 0, bytes.Length);

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            ImageConversion.LoadImage(tex, bytes);
            _diarySprite = Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[FishDiary] Could not load diary icon: {ex.Message}");
        }
        return _diarySprite;
    }
}
