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

    /// <summary>Show or hide the diary panel. Safe to call from any context.</summary>
    public static void Toggle() => Instance?.TogglePanel();

    /// <summary>Open the diary (no-op if already open).</summary>
    public static void Open() { if (Instance != null && !Instance._panelOpen) Instance.TogglePanel(); }

    // ── State ─────────────────────────────────────────────────────────────
    private Transform _canvasTransform;  // always-active canvas root
    private GameObject _diaryGroup;     // backdrop + panel — toggled on open/close
    private GameObject _panel;
    private TextMeshProUGUI _headerText;
    private Transform _gridContent;
    private bool _panelOpen;
    private GameObject _cornerButton;

    private FilterMode _filter = FilterMode.All;
    private enum FilterMode { All, Caught, Uncaught }

    // Colours for rarity badges
    private static readonly Dictionary<string, Color> RarityColors = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Common",    new Color(0.85f, 0.85f, 0.85f) },
        { "Uncommon",  new Color(0.40f, 0.85f, 0.40f) },
        { "Rare",      new Color(0.30f, 0.60f, 1.00f) },
        { "UltraRare", new Color(0.75f, 0.35f, 1.00f) },
        { "Legendary", new Color(1.00f, 0.70f, 0.10f) },
    };

    // Sort order for rarities (lower = show first)
    private static readonly Dictionary<string, int> RarityOrder = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Legendary", 0 }, { "UltraRare", 1 }, { "Rare", 2 }, { "Uncommon", 3 }, { "Common", 4 },
    };

    // Cached newspaper sprite loaded from embedded resource
    private static Sprite _diarySprite;

    // ── Factory ───────────────────────────────────────────────────────────

    /// <summary>Creates the DiaryUi host object and initialises the overlay panel.</summary>
    public static DiaryUi Create(ManualLogSource log)
    {
        var host = new GameObject("SledFishMod_DiaryUi");
        DontDestroyOnLoad(host);
        var ui = host.AddComponent<DiaryUi>();
        Instance = ui;
        return ui;
    }

    // ── MonoBehaviour lifecycle ───────────────────────────────────────────

    private void Awake()
    {
        BuildPanel();
    }

    private void Update()
    {
        // J to open/close, Escape to close
        if (Input.GetKeyDown(KeyCode.J))
            TogglePanel();
        else if (_panelOpen && Input.GetKeyDown(KeyCode.Escape))
            TogglePanel();
    }

    // ── Corner button ─────────────────────────────────────────────────────

    /// <summary>
    /// Adds a small "Fish Diary [J]" button in the bottom-left of the screen.
    /// Called by MenuPatch after UiReferenceController.Start fires.
    /// Safe to call more than once — ignored after first call.
    /// </summary>
    public void ShowCornerButton()
    {
        if (_cornerButton != null) return;

        _cornerButton = CreateRectChild(_canvasTransform, "DiaryCornerBtn");
        var rt = _cornerButton.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);
        rt.pivot     = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(12, 12);
        rt.sizeDelta = new Vector2(130, 34);

        var img = _cornerButton.AddComponent<Image>();
        img.color = new Color(0.10f, 0.18f, 0.28f, 0.85f);

        var btn = _cornerButton.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.20f, 0.35f, 0.55f, 1f);
        colors.pressedColor     = new Color(0.08f, 0.14f, 0.22f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(new Action(TogglePanel));

        // Icon (if sprite loaded)
        var sprite = LoadDiarySprite();
        float labelOffsetX = 8f;
        if (sprite != null)
        {
            var iconGo = CreateRectChild(_cornerButton.transform, "Icon");
            var irt = iconGo.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0, 0);
            irt.anchorMax = new Vector2(0, 1);
            irt.offsetMin = new Vector2(5, 4);
            irt.offsetMax = new Vector2(29, -4);
            var iImg = iconGo.AddComponent<Image>();
            iImg.sprite = sprite;
            iImg.preserveAspect = true;
            labelOffsetX = 32f;
        }

        // Label
        var lblGo = CreateRectChild(_cornerButton.transform, "Label");
        var lrt = lblGo.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0, 0);
        lrt.anchorMax = new Vector2(1, 1);
        lrt.offsetMin = new Vector2(labelOffsetX, 0);
        lrt.offsetMax = new Vector2(-4, 0);
        var lbl = lblGo.AddComponent<TextMeshProUGUI>();
        lbl.text = "Fish Diary  [J]";
        lbl.fontSize = 11;
        lbl.color = new Color(0.85f, 0.92f, 1f);
        lbl.alignment = TextAlignmentOptions.MidlineLeft;

        // Canvas is already active (only _diaryGroup toggles), so corner button is immediately visible.
        Plugin.Log.LogInfo("[FishDiary] Corner button shown.");
    }

    // ── Panel construction ────────────────────────────────────────────────

    private void BuildPanel()
    {
        // Root canvas — drawn above everything else
        var canvasGo = new GameObject("DiaryCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();
        _canvasTransform = canvasGo.transform;

        // Diary overlay group — contains the backdrop and panel.
        // Canvas is always active (hosts the corner button too), only this group toggles.
        _diaryGroup = CreateRectChild(canvasGo.transform, "DiaryGroup");
        StretchFull(_diaryGroup.GetComponent<RectTransform>());

        // Semi-transparent backdrop (blocks input to game while diary is open)
        var backdropGo = CreateRectChild(_diaryGroup.transform, "Backdrop");
        StretchFull(backdropGo.GetComponent<RectTransform>());
        var backdropImg = backdropGo.AddComponent<Image>();
        backdropImg.color = new Color(0, 0, 0, 0.70f);
        backdropGo.AddComponent<Button>().onClick
            .AddListener(new Action(TogglePanel));

        // Main panel
        _panel = CreateRectChild(_diaryGroup.transform, "DiaryPanel");
        SetAnchored(_panel.GetComponent<RectTransform>(), 0.5f, 0.5f, 0.5f, 0.5f);
        _panel.GetComponent<RectTransform>().sizeDelta = new Vector2(900, 640);
        var panelImg = _panel.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.08f, 0.12f, 0.97f);

        // Layout stack inside panel
        var vLayout = _panel.AddComponent<VerticalLayoutGroup>();
        vLayout.childForceExpandWidth  = true;
        vLayout.childForceExpandHeight = false;
        vLayout.childControlWidth  = true;
        vLayout.childControlHeight = true;
        vLayout.spacing = 4;
        vLayout.padding = new RectOffset(12, 12, 12, 12);

        // — Header row ——————————————————————————————
        var headerRow = CreateLayoutChild(_panel.transform, "HeaderRow", 50);
        var headerHoriz = headerRow.AddComponent<HorizontalLayoutGroup>();
        headerHoriz.childForceExpandWidth  = false;
        headerHoriz.childForceExpandHeight = true;
        headerHoriz.childControlHeight = true;
        headerHoriz.spacing = 8;

        _headerText = CreateTmpText(headerRow.transform, "HeaderText", "", 18);
        _headerText.alignment = TextAlignmentOptions.MidlineLeft;
        _headerText.GetComponent<LayoutElement>().flexibleWidth = 1;

        var closeBtn = CreateButton(headerRow.transform, "CloseBtn", "X", 40, 40, TogglePanel);
        closeBtn.GetComponent<LayoutElement>().preferredWidth  = 40;
        closeBtn.GetComponent<LayoutElement>().preferredHeight = 40;

        // — Filter row ——————————————————————————————
        var filterRow = CreateLayoutChild(_panel.transform, "FilterRow", 36);
        var filterHoriz = filterRow.AddComponent<HorizontalLayoutGroup>();
        filterHoriz.childForceExpandWidth  = false;
        filterHoriz.childForceExpandHeight = true;
        filterHoriz.childControlHeight = true;
        filterHoriz.spacing = 6;
        filterHoriz.childAlignment = TextAnchor.MiddleLeft;

        var modeAll      = FilterMode.All;
        var modeCaught   = FilterMode.Caught;
        var modeUncaught = FilterMode.Uncaught;
        CreateFilterButton(filterRow.transform, "All",      modeAll);
        CreateFilterButton(filterRow.transform, "Caught",   modeCaught);
        CreateFilterButton(filterRow.transform, "Uncaught", modeUncaught);

        // — Scroll area ——————————————————————————————
        var scrollGo = CreateRectChild(_panel.transform, "ScrollRect");
        var scrollLayout = scrollGo.AddComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1;

        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical   = true;
        scrollRect.scrollSensitivity = 30;

        // Viewport
        var viewportGo = CreateRectChild(scrollGo.transform, "Viewport");
        StretchFull(viewportGo.GetComponent<RectTransform>());
        viewportGo.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        var mask = viewportGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        scrollRect.viewport = viewportGo.GetComponent<RectTransform>();

        // Content grid
        var contentGo = CreateRectChild(viewportGo.transform, "Content");
        var contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot     = new Vector2(0.5f, 1);
        contentRt.sizeDelta = Vector2.zero;

        var grid = contentGo.AddComponent<GridLayoutGroup>();
        grid.cellSize    = new Vector2(160, 210);
        grid.spacing     = new Vector2(8, 8);
        grid.padding     = new RectOffset(8, 8, 8, 8);
        grid.constraint  = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        grid.childAlignment  = TextAnchor.UpperLeft;

        var csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRt;
        _gridContent = contentGo.transform;

        // Diary group starts hidden; canvas stays active so the corner button is always visible
        _diaryGroup.SetActive(false);
        _panelOpen = false;
    }

    // ── Panel show/hide ───────────────────────────────────────────────────

    private void TogglePanel()
    {
        _panelOpen = !_panelOpen;
        _diaryGroup.SetActive(_panelOpen);

        if (_panelOpen)
        {
            FishDiaryStore.ClearNewFlags();
            Refresh();
        }
    }

    // ── Grid population ───────────────────────────────────────────────────

    /// <summary>Destroys and rebuilds all fish cards based on current filter.</summary>
    public void Refresh()
    {
        // Clear existing cards
        for (var i = _gridContent.childCount - 1; i >= 0; i--)
            Destroy(_gridContent.GetChild(i).gameObject);

        var allRows = BuildFishRows();
        var filtered = _filter switch
        {
            FilterMode.Caught   => allRows.Where(r => r.Entry?.Caught == true).ToList(),
            FilterMode.Uncaught => allRows.Where(r => r.Entry?.Caught != true).ToList(),
            _ => allRows,
        };

        var totalKnown  = allRows.Count;
        var totalCaught = allRows.Count(r => r.Entry?.Caught == true);
        var pct = totalKnown > 0 ? (int)(totalCaught * 100f / totalKnown) : 0;
        _headerText.text = $"Fish Diary  |  {totalCaught} / {totalKnown} caught  ({pct}%)";

        foreach (var row in filtered)
            BuildCard(row);
    }

    // ── Fish row model ────────────────────────────────────────────────────

    private sealed class FishRow
    {
        public string Key;
        public string Name;
        public string Rarity;
        public string PackLabel;
        public float  MinLength;
        public float  MaxLength;
        public int    RuntimeId;    // 0 for vanilla
        public DiaryEntry Entry;
    }

    private List<FishRow> BuildFishRows()
    {
        var rows = new List<FishRow>();

        // Custom fish — always shown even if never caught
        foreach (var def in CustomFishRegistry.All)
        {
            rows.Add(new FishRow
            {
                Key       = def.Guid,
                Name      = def.Name,
                Rarity    = def.Rarity,
                PackLabel = def.PackId,
                MinLength = def.MinLength,
                MaxLength = def.MaxLength,
                RuntimeId = def.RuntimeId,
                Entry     = FishDiaryStore.GetEntry(def.Guid),
            });
        }

        // Vanilla fish — only shown once caught (we don't know the full list ahead of time)
        foreach (var kvp in FishDiaryStore.AllEntries)
        {
            if (kvp.Key.StartsWith("vanilla_") && kvp.Value.Caught)
            {
                rows.Add(new FishRow
                {
                    Key       = kvp.Key,
                    Name      = kvp.Key.Replace("vanilla_", ""),
                    Rarity    = "Vanilla",
                    PackLabel = "Base Game",
                    Entry     = kvp.Value,
                });
            }
        }

        // Sort: rarity tier descending (Legendary first), then alpha
        rows.Sort((a, b) =>
        {
            var oa = RarityOrder.TryGetValue(a.Rarity, out var va) ? va : 5;
            var ob = RarityOrder.TryGetValue(b.Rarity, out var vb) ? vb : 5;
            var c = oa.CompareTo(ob);
            return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        return rows;
    }

    // ── Card builder ──────────────────────────────────────────────────────

    private void BuildCard(FishRow row)
    {
        var caught = row.Entry?.Caught == true;
        var rarityColor = RarityColors.TryGetValue(row.Rarity, out var rc) ? rc : Color.white;

        // Card background
        var card = CreateRectChild(_gridContent, $"Card_{row.Key}");
        card.AddComponent<Image>().color = new Color(0.13f, 0.13f, 0.18f);

        var vl = card.AddComponent<VerticalLayoutGroup>();
        vl.childForceExpandWidth  = true;
        vl.childForceExpandHeight = false;
        vl.childControlWidth  = true;
        vl.childControlHeight = true;
        vl.spacing = 3;
        vl.padding = new RectOffset(6, 6, 6, 6);

        // ── Sprite area ──
        var spriteArea = CreateLayoutChild(card.transform, "SpriteArea", 100);
        var spriteImg  = spriteArea.AddComponent<Image>();

        if (row.RuntimeId > 0)
        {
            var sprite = SpriteLoader.Load(row.RuntimeId);
            if (sprite != null)
            {
                spriteImg.sprite = sprite;
                spriteImg.preserveAspect = true;
                spriteImg.color = caught ? Color.white : Color.black;
            }
            else
            {
                spriteImg.color = new Color(0.2f, 0.2f, 0.2f);
            }
        }
        else
        {
            spriteImg.color = caught ? new Color(0.3f, 0.3f, 0.4f) : new Color(0.15f, 0.15f, 0.20f);
        }

        // ── Name ──
        var nameTxt = CreateTmpText(card.transform, "Name", caught ? row.Name : "???", 13);
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.color = caught ? Color.white : new Color(0.5f, 0.5f, 0.5f);
        nameTxt.GetComponent<LayoutElement>().preferredHeight = 20;

        // ── Rarity badge ──
        var rarityRow = CreateLayoutChild(card.transform, "RarityRow", 18);
        var rarityHoriz = rarityRow.AddComponent<HorizontalLayoutGroup>();
        rarityHoriz.childForceExpandWidth  = true;
        rarityHoriz.childForceExpandHeight = true;
        rarityHoriz.childControlWidth  = true;
        rarityHoriz.childControlHeight = true;
        rarityRow.AddComponent<Image>().color = new Color(
            rarityColor.r * 0.3f, rarityColor.g * 0.3f, rarityColor.b * 0.3f);
        var rarityTxt = CreateTmpText(rarityRow.transform, "Rarity", row.Rarity, 10);
        rarityTxt.color = rarityColor;
        rarityTxt.alignment = TextAlignmentOptions.Center;

        // ── Pack label ──
        var packTxt = CreateTmpText(card.transform, "Pack", row.PackLabel, 9);
        packTxt.color = new Color(0.55f, 0.55f, 0.65f);
        packTxt.alignment = TextAlignmentOptions.Center;
        packTxt.GetComponent<LayoutElement>().preferredHeight = 14;

        if (caught)
        {
            // ── Biggest size ──
            var sizeTxt = CreateTmpText(card.transform, "Size",
                $"Best: {row.Entry.BiggestLength * 100f:F1} cm", 10);
            sizeTxt.color = new Color(0.80f, 0.90f, 0.80f);
            sizeTxt.alignment = TextAlignmentOptions.Center;
            sizeTxt.GetComponent<LayoutElement>().preferredHeight = 14;

            // ── Catch count + shiny star ──
            var statsLine = $"x{row.Entry.CatchCount}";
            if (row.Entry.HasShiny) statsLine += "  *shiny*";
            var statsTxt = CreateTmpText(card.transform, "Stats", statsLine, 10);
            statsTxt.color = new Color(0.75f, 0.85f, 0.95f);
            statsTxt.alignment = TextAlignmentOptions.Center;
            statsTxt.GetComponent<LayoutElement>().preferredHeight = 14;
        }

        // ── NEW badge ──
        if (row.Entry?.IsNew == true)
        {
            var newTxt = CreateTmpText(card.transform, "NewBadge", "NEW!", 11);
            newTxt.color = new Color(1f, 0.9f, 0.2f);
            newTxt.fontStyle = FontStyles.Bold;
            newTxt.alignment = TextAlignmentOptions.Center;
            newTxt.GetComponent<LayoutElement>().preferredHeight = 14;
        }
    }

    // ── Filter button ─────────────────────────────────────────────────────

    private void CreateFilterButton(Transform parent, string label, FilterMode mode)
    {
        var capturedMode = mode;
        var btn = CreateButton(parent, $"Filter_{mode}", label, 90, 28, () => SetFilter(capturedMode));
        btn.GetComponent<LayoutElement>().preferredWidth  = 90;
        btn.GetComponent<LayoutElement>().preferredHeight = 28;
    }

    private void SetFilter(FilterMode mode)
    {
        _filter = mode;
        if (_panelOpen) Refresh();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static GameObject CreateRectChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static GameObject CreateLayoutChild(Transform parent, string name, float preferredHeight = -1)
    {
        var go = CreateRectChild(parent, name);
        var le = go.AddComponent<LayoutElement>();
        if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
        return go;
    }

    private static TextMeshProUGUI CreateTmpText(Transform parent, string name, string text, float fontSize)
    {
        var go = CreateRectChild(parent, name);
        go.AddComponent<LayoutElement>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;
        return tmp;
    }

    private static GameObject CreateButton(Transform parent, string name, string label,
        float w, float h, Action onClick)
    {
        var go = CreateRectChild(parent, name);
        go.AddComponent<LayoutElement>();

        var img = go.AddComponent<Image>();
        img.color = new Color(0.20f, 0.20f, 0.30f);

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.30f, 0.30f, 0.45f);
        colors.pressedColor     = new Color(0.15f, 0.15f, 0.25f);
        btn.colors = colors;
        btn.onClick.AddListener(new Action(onClick));

        var txt = CreateTmpText(go.transform, "Label", label, 12);
        txt.alignment = TextAlignmentOptions.Center;
        var rt = txt.GetComponent<RectTransform>();
        StretchFull(rt);

        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetAnchored(RectTransform rt, float ax, float ay, float px, float py)
    {
        rt.anchorMin = new Vector2(ax, ay);
        rt.anchorMax = new Vector2(ax, ay);
        rt.pivot     = new Vector2(px, py);
    }

    // ── Diary button sprite ───────────────────────────────────────────────

    /// <summary>
    /// Loads the embedded newspaper.png as a Sprite.
    /// Returns null on failure (the button falls back to text-only).
    /// </summary>
    public static Sprite LoadDiarySprite()
    {
        if (_diarySprite != null) return _diarySprite;
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("SledFishMod.FishDiary.newspaper.png");
            if (stream == null) return null;

            var bytes = new byte[stream.Length];
            _ = stream.Read(bytes, 0, bytes.Length);

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            ImageConversion.LoadImage(tex, bytes);
            _diarySprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[FishDiary] Could not load diary icon: {ex.Message}");
        }
        return _diarySprite;
    }
}
