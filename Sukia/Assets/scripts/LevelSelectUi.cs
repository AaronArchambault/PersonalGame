using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple level select - shows all levels as a grid of buttons.
/// Assign 'levels' array and 'panel' in the Inspector. Nothing else needed.
/// </summary>
public class LevelSelectUI : MonoBehaviour
{
    [Header("Required")]
    public LevelData[]  levels;
    public GameObject   panel;

    // Built at runtime
    private LevelData   selected;
    private GameObject  infoBox;
    private TextMeshProUGUI infoTitle;
    private TextMeshProUGUI infoDesc;
    private Button      playBtn;

    void Start()
    {
        BuildPanel();
        Show();
    }

    public void Show() { if (panel) panel.SetActive(true);  }
    public void Hide() { if (panel) panel.SetActive(false); }

    void BuildPanel()
    {
        if (panel == null) return;

        // Nuke anything already inside so we start clean
        for (int i = panel.transform.childCount - 1; i >= 0; i--)
            Destroy(panel.transform.GetChild(i).gameObject);

        // Dark background
        Image bg = panel.GetComponent<Image>();
        if (bg == null) bg = panel.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.14f, 0.98f);

        RectTransform panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        // ── Title ─────────────────────────────────────────────────────────────
        MakeText(panel, "Title", "Select a Level",
            new Vector2(0f, 0.88f), new Vector2(1f, 1f),
            36, Color.white, TextAlignmentOptions.Center);

        // ── Button grid ───────────────────────────────────────────────────────
        // We'll place up to 3 columns, auto rows
        int cols     = 3;
        int count    = levels != null ? levels.Length : 0;
        int rows     = Mathf.CeilToInt((float)count / cols);

        // Grid area: from y=0.30 to y=0.86
        float gridTop    = 0.86f;
        float gridBot    = 0.30f;
        float gridLeft   = 0.03f;
        float gridRight  = 0.97f;

        float cellW = (gridRight - gridLeft) / cols;
        float cellH = (gridTop - gridBot) / Mathf.Max(rows, 1);
        float pad   = 0.008f; // padding between cells (in anchor space)

        int highScore = PlayerPrefs.GetInt("HighScore_Global", 0);

        for (int i = 0; i < count; i++)
        {
            LevelData lvl = levels[i];
            if (lvl == null) continue;

            int col = i % cols;
            int row = i / cols;

            float xMin = gridLeft + col * cellW + pad;
            float xMax = gridLeft + (col + 1) * cellW - pad;
            float yMax = gridTop  - row * cellH - pad;
            float yMin = gridTop  - (row + 1) * cellH + pad;

            bool unlocked = highScore >= lvl.scoreToUnlock;
            MakeLevelButton(panel, lvl, unlocked,
                new Vector2(xMin, yMin), new Vector2(xMax, yMax));
        }

        // ── Info bar at bottom ────────────────────────────────────────────────
        infoBox = MakeRect(panel, "InfoBar",
            new Vector2(0f, 0f), new Vector2(1f, 0.28f));
        Image infoImg = infoBox.AddComponent<Image>();
        infoImg.color = new Color(0.05f, 0.05f, 0.10f, 1f);

        infoTitle = MakeText(infoBox, "InfoTitle", "← Choose a level above",
            new Vector2(0.02f, 0.55f), new Vector2(0.72f, 0.95f),
            24, Color.white, TextAlignmentOptions.Left);

        infoDesc = MakeText(infoBox, "InfoDesc", "",
            new Vector2(0.02f, 0.10f), new Vector2(0.72f, 0.54f),
            16, new Color(0.7f, 0.85f, 1f), TextAlignmentOptions.Left);

        // Play button
        GameObject playGo = MakeRect(panel, "PlayBtn",
            new Vector2(0.74f, 0.04f), new Vector2(0.97f, 0.96f));
        // re-parent to infoBox in anchor space
        playGo.transform.SetParent(infoBox.transform, false);
        RectTransform playRT = playGo.GetComponent<RectTransform>();
        playRT.anchorMin = new Vector2(0.74f, 0.08f);
        playRT.anchorMax = new Vector2(0.97f, 0.92f);
        playRT.offsetMin = Vector2.zero;
        playRT.offsetMax = Vector2.zero;

        Image playImg = playGo.AddComponent<Image>();
        playImg.color = new Color(0.15f, 0.65f, 0.30f);
        playBtn = playGo.AddComponent<Button>();
        playBtn.targetGraphic = playImg;
        SetButtonColors(playBtn, new Color(0.15f, 0.65f, 0.30f));
        playBtn.interactable = false;
        playBtn.onClick.AddListener(LaunchSelected);

        MakeText(playGo, "PlayLabel", "▶  PLAY",
            Vector2.zero, Vector2.one, 22, Color.white, TextAlignmentOptions.Center);
    }

    void MakeLevelButton(GameObject parent, LevelData lvl, bool unlocked,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        Color normalCol  = unlocked
            ? new Color(0.18f, 0.38f, 0.72f)
            : new Color(0.22f, 0.22f, 0.26f);

        GameObject go = MakeRect(parent, "Btn_" + lvl.levelName, anchorMin, anchorMax);

        Image img  = go.AddComponent<Image>();
        img.color  = normalCol;
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        SetButtonColors(btn, normalCol);
        btn.interactable = unlocked;

        if (unlocked)
        {
            LevelData cap = lvl;
            btn.onClick.AddListener(() => OnLevelClicked(cap, img));
        }

        // Level name
        Color nameCol = unlocked ? Color.white : new Color(0.5f, 0.5f, 0.55f);
        MakeText(go, "Name", lvl.levelName,
            new Vector2(0.05f, 0.50f), new Vector2(0.95f, 0.95f),
            20, nameCol, TextAlignmentOptions.Center);

        // Score badge (unlock requirement)
        string badge = unlocked ? "✓" : lvl.scoreToUnlock + " pts";
        Color badgeCol = unlocked ? new Color(0.4f, 1f, 0.5f) : new Color(0.7f, 0.4f, 0.4f);
        MakeText(go, "Badge", badge,
            new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.48f),
            14, badgeCol, TextAlignmentOptions.Center);
    }

    void OnLevelClicked(LevelData lvl, Image pressedImg)
    {
        selected = lvl;
        if (infoTitle) infoTitle.text = lvl.levelName;
        if (infoDesc)  infoDesc.text  = lvl.description;
        if (playBtn)   playBtn.interactable = true;
    }

    void LaunchSelected()
    {
        if (selected == null && levels != null && levels.Length > 0)
            selected = levels[0];
        if (selected == null) return;

        GameManager.Instance?.LoadLevel(selected);
        Hide();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    GameObject MakeRect(GameObject parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    TextMeshProUGUI MakeText(GameObject parent, string name, string text,
        Vector2 anchorMin, Vector2 anchorMax,
        float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = MakeRect(parent, name, anchorMin, anchorMax);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text            = text;
        tmp.fontSize        = size;
        tmp.color           = color;
        tmp.alignment       = align;
        tmp.overflowMode    = TextOverflowModes.Ellipsis;
        tmp.enableWordWrapping = true;
        return tmp;
    }

    void SetButtonColors(Button btn, Color normal)
    {
        ColorBlock cb       = btn.colors;
        cb.normalColor      = normal;
        cb.highlightedColor = normal * 1.2f;
        cb.pressedColor     = normal * 0.75f;
        cb.disabledColor    = new Color(0.2f, 0.2f, 0.22f, 0.8f);
        cb.colorMultiplier  = 1f;
        btn.colors          = cb;
    }
}








/*using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds the level select UI entirely in code — no prefab needed.
/// Attach to any GameObject in the scene (e.g. the Canvas itself).
/// Assign your LevelData assets in the Inspector.
/// Set 'panel' to the levelselectPanel Image/GameObject.
/// Everything else is created at runtime.
/// </summary>
public class LevelSelectUI : MonoBehaviour
{
    [Header("Required")]
    public LevelData[] levels;
    public GameObject  panel; // the levelselectPanel — shown/hidden

    [Header("Optional detail labels (can leave empty)")]
    public TextMeshProUGUI detailName;
    public TextMeshProUGUI detailDesc;

    // Colors
    private static readonly Color COL_PANEL_BG    = new Color(0.10f, 0.10f, 0.18f, 0.97f);
    private static readonly Color COL_BTN_NORMAL  = new Color(0.20f, 0.40f, 0.75f, 1.00f);
    private static readonly Color COL_BTN_HOVER   = new Color(0.28f, 0.52f, 0.90f, 1.00f);
    private static readonly Color COL_BTN_PRESS   = new Color(0.14f, 0.28f, 0.55f, 1.00f);
    private static readonly Color COL_BTN_LOCKED  = new Color(0.25f, 0.25f, 0.30f, 0.80f);
    private static readonly Color COL_PLAY_BTN    = new Color(0.15f, 0.70f, 0.35f, 1.00f);
    private static readonly Color COL_TITLE       = new Color(1.00f, 1.00f, 1.00f, 1.00f);
    private static readonly Color COL_DESC        = new Color(0.75f, 0.85f, 1.00f, 1.00f);
    private static readonly Color COL_LOCKED_TEXT = new Color(0.60f, 0.60f, 0.65f, 1.00f);

    private LevelData selectedLevel;
    private List<Button> levelButtons = new List<Button>();

    // ── Built references ──────────────────────────────────────────────────────
    private TextMeshProUGUI builtDetailName;
    private TextMeshProUGUI builtDetailDesc;
    private Button          builtPlayButton;

    void Start()
    {
        SetupPanelBackground();
        BuildUI();
        Show();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show()
    {
        if (panel != null) panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    // ── Panel background ──────────────────────────────────────────────────────

    void SetupPanelBackground()
    {
        if (panel == null) return;
        Image img = panel.GetComponent<Image>();
        if (img == null) img = panel.AddComponent<Image>();
        img.color = COL_PANEL_BG;

        // Ensure it fills the screen
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ── Build entire UI ───────────────────────────────────────────────────────

    void BuildUI()
    {
        if (panel == null) return;

        // Clear anything already inside
        foreach (Transform child in panel.transform)
            Destroy(child.gameObject);

        levelButtons.Clear();

        // ── Title label ───────────────────────────────────────────────────────
        GameObject titleGo = CreateTMPLabel(panel, "SelectTitle",
            "Select a Level", 36, COL_TITLE, TextAlignmentOptions.Center);
        RectTransform titleRT = titleGo.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 0.88f);
        titleRT.anchorMax = new Vector2(1f, 1.00f);
        titleRT.offsetMin = new Vector2(20f, 0f);
        titleRT.offsetMax = new Vector2(-20f, 0f);

        // ── Scroll view for buttons ───────────────────────────────────────────
        GameObject scrollGo = new GameObject("ScrollView");
        scrollGo.transform.SetParent(panel.transform, false);
        ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical   = true;
        scrollGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // invisible mask bg
        Mask mask = scrollGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        RectTransform scrollRT = scrollGo.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0.05f, 0.20f);
        scrollRT.anchorMax = new Vector2(0.95f, 0.87f);
        scrollRT.offsetMin = Vector2.zero;
        scrollRT.offsetMax = Vector2.zero;

        // ── Content container ─────────────────────────────────────────────────
        GameObject contentGo = new GameObject("Content");
        contentGo.transform.SetParent(scrollGo.transform, false);
        RectTransform contentRT = contentGo.GetComponent<RectTransform>();
        if (contentRT == null) contentRT = contentGo.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot     = new Vector2(0.5f, 1f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing            = 10f;
        vlg.padding            = new RectOffset(0, 0, 5, 5);
        vlg.childControlWidth  = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content  = contentRT;
        scroll.viewport = scrollRT;

        // ── Level buttons ─────────────────────────────────────────────────────
        int highScore = PlayerPrefs.GetInt("HighScore_Global", 0);

        foreach (LevelData level in levels)
        {
            if (level == null) continue;
            bool unlocked = highScore >= level.scoreToUnlock;
            CreateLevelButton(contentGo, level, unlocked);
        }

        // ── Detail / play area ────────────────────────────────────────────────
        GameObject detailArea = new GameObject("DetailArea");
        detailArea.transform.SetParent(panel.transform, false);
        Image detailImg = detailArea.AddComponent<Image>();
        detailImg.color = new Color(0.05f, 0.05f, 0.12f, 0.9f);
        RectTransform detailRT = detailArea.GetComponent<RectTransform>();
        detailRT.anchorMin = new Vector2(0.05f, 0.02f);
        detailRT.anchorMax = new Vector2(0.95f, 0.19f);
        detailRT.offsetMin = Vector2.zero;
        detailRT.offsetMax = Vector2.zero;

        // Detail name label
        GameObject dnGo = CreateTMPLabel(detailArea, "DetailName",
            "← Select a level", 22, COL_TITLE, TextAlignmentOptions.Left);
        RectTransform dnRT = dnGo.GetComponent<RectTransform>();
        dnRT.anchorMin = new Vector2(0f, 0.55f);
        dnRT.anchorMax = new Vector2(0.70f, 1f);
        dnRT.offsetMin = new Vector2(12f, 0f);
        dnRT.offsetMax = new Vector2(-4f, 0f);
        builtDetailName = dnGo.GetComponent<TextMeshProUGUI>();

        // Detail desc label
        GameObject ddGo = CreateTMPLabel(detailArea, "DetailDesc",
            "", 16, COL_DESC, TextAlignmentOptions.Left);
        RectTransform ddRT = ddGo.GetComponent<RectTransform>();
        ddRT.anchorMin = new Vector2(0f, 0f);
        ddRT.anchorMax = new Vector2(0.70f, 0.54f);
        ddRT.offsetMin = new Vector2(12f, 4f);
        ddRT.offsetMax = new Vector2(-4f, 0f);
        builtDetailDesc = ddGo.GetComponent<TextMeshProUGUI>();

        // Play button
        GameObject playGo = CreateButton(detailArea, "PlayButton", "▶  PLAY",
            COL_PLAY_BTN, 22, () => LaunchSelected());
        RectTransform playRT = playGo.GetComponent<RectTransform>();
        playRT.anchorMin = new Vector2(0.72f, 0.10f);
        playRT.anchorMax = new Vector2(0.98f, 0.90f);
        playRT.offsetMin = Vector2.zero;
        playRT.offsetMax = Vector2.zero;
        builtPlayButton = playGo.GetComponent<Button>();
        builtPlayButton.interactable = false;
    }

    // ── Create one level button ───────────────────────────────────────────────

    void CreateLevelButton(GameObject parent, LevelData level, bool unlocked)
    {
        Color btnColor = unlocked ? COL_BTN_NORMAL : COL_BTN_LOCKED;
        string label   = level.levelName;
        string subText = unlocked
            ? level.description
            : "Unlock at " + level.scoreToUnlock + " pts";

        GameObject btnGo = new GameObject("Btn_" + level.levelName);
        btnGo.transform.SetParent(parent.transform, false);

        // Size
        RectTransform rt = btnGo.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 75f);
        LayoutElement le = btnGo.AddComponent<LayoutElement>();
        le.minHeight       = 75f;
        le.preferredHeight = 75f;

        // Background image
        Image img   = btnGo.AddComponent<Image>();
        img.color   = btnColor;
        img.sprite  = CreateRoundedSprite();
        img.type    = Image.Type.Sliced;

        // Button
        Button btn  = btnGo.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.interactable  = unlocked;

        ColorBlock cb      = btn.colors;
        cb.normalColor     = btnColor;
        cb.highlightedColor = unlocked ? COL_BTN_HOVER  : COL_BTN_LOCKED;
        cb.pressedColor    = unlocked ? COL_BTN_PRESS  : COL_BTN_LOCKED;
        cb.disabledColor   = COL_BTN_LOCKED;
        cb.colorMultiplier = 1f;
        btn.colors         = cb;

        if (unlocked)
        {
            LevelData captured = level;
            btn.onClick.AddListener(() => SelectLevel(captured));
        }

        levelButtons.Add(btn);

        // Name label
        GameObject nameGo = CreateTMPLabel(btnGo, "Name", label, 22,
            unlocked ? COL_TITLE : COL_LOCKED_TEXT, TextAlignmentOptions.Left);
        RectTransform nameRT = nameGo.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0f, 0.50f);
        nameRT.anchorMax = new Vector2(1f, 1.00f);
        nameRT.offsetMin = new Vector2(16f, 0f);
        nameRT.offsetMax = new Vector2(-16f, 0f);

        // Description label
        GameObject descGo = CreateTMPLabel(btnGo, "Desc", subText, 14,
            unlocked ? COL_DESC : COL_LOCKED_TEXT, TextAlignmentOptions.Left);
        RectTransform descRT = descGo.GetComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0f, 0f);
        descRT.anchorMax = new Vector2(1f, 0.50f);
        descRT.offsetMin = new Vector2(16f, 4f);
        descRT.offsetMax = new Vector2(-16f, 0f);
    }

    // ── Selection & launch ───────────────────────────────────────────────────

    void SelectLevel(LevelData level)
    {
        selectedLevel = level;

        if (builtDetailName != null) builtDetailName.text = level.levelName;
        if (builtDetailDesc  != null) builtDetailDesc.text  = level.description;
        if (builtPlayButton  != null) builtPlayButton.interactable = true;

        // Also write to optional external labels
        if (detailName != null) detailName.text = level.levelName;
        if (detailDesc != null) detailDesc.text  = level.description;
    }

    void LaunchSelected()
    {
        if (selectedLevel == null && levels.Length > 0)
            selectedLevel = levels[0];
        if (selectedLevel == null) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadLevel(selectedLevel);
            Hide();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    GameObject CreateTMPLabel(GameObject parent, string name, string text,
        float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.alignment = alignment;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        go.AddComponent<RectTransform>();
        return go;
    }

    GameObject CreateButton(GameObject parent, string name, string label,
        Color color, float fontSize, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();

        Image img  = go.AddComponent<Image>();
        img.color  = color;
        img.sprite = CreateRoundedSprite();
        img.type   = Image.Type.Sliced;

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock cb = btn.colors;
        cb.normalColor      = color;
        cb.highlightedColor = color * 1.15f;
        cb.pressedColor     = color * 0.80f;
        cb.colorMultiplier  = 1f;
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        GameObject labelGo = CreateTMPLabel(go, "Label", label, fontSize,
            Color.white, TextAlignmentOptions.Center);
        RectTransform lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        return go;
    }

    // Generates a simple white sprite used for tinted UI images
    Sprite CreateRoundedSprite()
    {
        Texture2D tex = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4),
            new Vector2(0.5f, 0.5f), 4f, 0, SpriteMeshType.FullRect,
            new Vector4(1, 1, 1, 1));
    }
}



*/





/*using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to the Level Select Canvas.
/// Populate the 'levels' array in the Inspector with your LevelData assets.
/// Call Show() to display; the panel hides itself when a level is chosen.
/// </summary>
public class LevelSelectUI : MonoBehaviour
{
    [Header("Data")]
    public LevelData[] levels;

    [Header("UI References")]
    public GameObject      panel;          // root panel to show/hide
    public Transform       buttonContainer; // layout group holding buttons
    public GameObject      levelButtonPrefab; // prefab: Button + icon + name + desc labels

    [Header("Detail Panel (optional)")]
    public TextMeshProUGUI detailName;
    public TextMeshProUGUI detailDesc;
    public Button          playButton;

    private LevelData selectedLevel;

    void Start()
    {
        BuildButtons();
        if (playButton != null)
            playButton.onClick.AddListener(LaunchSelected);
        Show();
    }

    public void Show()
    {
        panel.SetActive(true);
    }

    public void Hide()
    {
        panel.SetActive(false);
    }

    void BuildButtons()
    {
        // Clear old buttons
        foreach (Transform child in buttonContainer)
            Destroy(child.gameObject);

        int highScore = PlayerPrefs.GetInt("HighScore_Global", 0);

        for (int i = 0; i < levels.Length; i++)
        {
            LevelData level = levels[i];
            bool unlocked = highScore >= level.scoreToUnlock;

            GameObject btn = Instantiate(levelButtonPrefab, buttonContainer);

            // Texts
            TextMeshProUGUI[] texts = btn.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 0) texts[0].text = level.levelName;
            if (texts.Length > 1) texts[1].text = unlocked ? level.description : "Score " + level.scoreToUnlock + " to unlock";

            // Thumbnail
            Image img = btn.GetComponentInChildren<Image>();
            if (img != null && level.thumbnail != null)
                img.sprite = level.thumbnail;

            // Lock state
            Button b = btn.GetComponent<Button>();
            if (b != null)
            {
                b.interactable = unlocked;
                if (unlocked)
                {
                    LevelData captured = level;
                    b.onClick.AddListener(() => SelectLevel(captured));
                }
            }
        }
    }

    void SelectLevel(LevelData level)
    {
        selectedLevel = level;
        if (detailName != null) detailName.text = level.levelName;
        if (detailDesc  != null) detailDesc.text  = level.description;
    }

    void LaunchSelected()
    {
        if (selectedLevel == null && levels.Length > 0)
            selectedLevel = levels[0];

        if (selectedLevel == null) return;

        // Pass selection to GameManager and start
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadLevel(selectedLevel);
            Hide();
        }
    }

    // Quick-start without UI (e.g. called from Inspector button)
    public void LaunchLevel(int index)
    {
        if (index < 0 || index >= levels.Length) return;
        selectedLevel = levels[index];
        LaunchSelected();
    }
}*/