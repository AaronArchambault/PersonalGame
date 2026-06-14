using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Add this script to an empty GameObject called "Bootstrapper" in a new empty scene.
/// It will procedurally create the entire game scene at runtime.
/// You still need to:
///   1. Create a FruitData asset (Assets > Create > SuikaGame > FruitData) and fill it in.
///   2. Assign that FruitData asset to the Bootstrapper's fruitData field.
///   3. Make sure TextMeshPro is imported (Window > Package Manager > TMP Essential Resources).
/// </summary>
public class SceneBootstrapper : MonoBehaviour
{
    [Header("Required")]
    public FruitData fruitData;

    // Container dimensions (must match ContainerBuilder values)
    private const float CONTAINER_WIDTH = 5f;
    private const float CONTAINER_HEIGHT = 7f;
    private const float WALL_THICKNESS = 0.5f;

    void Awake()
    {
        if (fruitData == null)
        {
            Debug.LogError("SceneBootstrapper: FruitData not assigned!");
            return;
        }

        SetupCamera();
        SetupPhysics();

        GameObject container = BuildContainer();
        GameObject fruitPrefab = BuildFruitPrefab();
        Transform dropPoint = BuildDropPoint();
        LineRenderer dropLine = BuildDropLine();

        (Canvas canvas, TextMeshProUGUI scoreText, TextMeshProUGUI highScoreText,
         TextMeshProUGUI nextLabel, GameObject gameOverPanel, TextMeshProUGUI gameOverScore) = BuildUI();

        BuildGameManager(container.transform, fruitPrefab, dropPoint, dropLine,
                         scoreText, highScoreText, nextLabel, gameOverPanel, gameOverScore);

        // Build background
        BuildBackground();
    }

    // ─── Camera ───────────────────────────────────────────────────────────────

    void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 7f;
        cam.transform.position = new Vector3(0f, 0.5f, -10f);
        cam.backgroundColor = new Color(0.96f, 0.92f, 0.84f);
        cam.clearFlags = CameraClearFlags.SolidColor;
    }

    // ─── Physics ──────────────────────────────────────────────────────────────

    void SetupPhysics()
    {
        Physics2D.gravity = new Vector2(0f, -18f);
    }

    // ─── Container ────────────────────────────────────────────────────────────

    GameObject BuildContainer()
    {
        // Parent sits at world origin — walls use world positions directly
        GameObject container = new GameObject("Container");
        container.transform.position = Vector3.zero;

        float halfW  = CONTAINER_WIDTH  / 2f;
        float halfH  = CONTAINER_HEIGHT / 2f;
        float floorY = -halfH - WALL_THICKNESS / 2f;   // bottom of interior
        float midY   = 0f;                              // vertical centre

        // Floor — wide flat slab
        AddWall(container, "Floor",
            new Vector3(0f, floorY, 0f),
            new Vector2(CONTAINER_WIDTH + WALL_THICKNESS * 2f, WALL_THICKNESS),
            new Color(0.70f, 0.60f, 0.40f));

        // Left wall
        AddWall(container, "LeftWall",
            new Vector3(-halfW - WALL_THICKNESS / 2f, midY, 0f),
            new Vector2(WALL_THICKNESS, CONTAINER_HEIGHT),
            new Color(0.75f, 0.65f, 0.45f));

        // Right wall
        AddWall(container, "RightWall",
            new Vector3( halfW + WALL_THICKNESS / 2f, midY, 0f),
            new Vector2(WALL_THICKNESS, CONTAINER_HEIGHT),
            new Color(0.75f, 0.65f, 0.45f));

        AddDangerLine(container, 0.75f);

        return container;
    }

    /// <summary>
    /// Creates a wall at the given WORLD position with an accurate BoxCollider2D.
    /// The visual quad is driven by transform.localScale so there is no
    /// sprite/collider size mismatch.
    /// </summary>
    void AddWall(GameObject parent, string wallName, Vector3 worldPos, Vector2 size, Color color)
    {
        GameObject wall = new GameObject(wallName);
        wall.transform.SetParent(parent.transform, false);

        // Place in world space — SetParent(false) keeps local = world here
        // because the parent is at origin with no rotation/scale.
        wall.transform.localPosition = worldPos;
        wall.transform.localScale    = new Vector3(size.x, size.y, 1f);

        // Collider: size (1,1) × scale = correct world size
        BoxCollider2D col = wall.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;

        // Zero-bounce, zero-friction material so fruits settle cleanly
        PhysicsMaterial2D mat = new PhysicsMaterial2D("WallMat");
        mat.bounciness = 0f;
        mat.friction   = 0.4f;
        col.sharedMaterial = mat;

        // Visual quad that exactly matches the collider
        SpriteRenderer sr = wall.AddComponent<SpriteRenderer>();
        sr.sprite       = CreatePixelSprite(color);
        sr.sortingOrder = -1;
    }

    void AddDangerLine(GameObject parent, float heightFraction)
    {
        float dangerLocalY = -CONTAINER_HEIGHT / 2f + CONTAINER_HEIGHT * heightFraction;
        float dangerWorldY = parent.transform.position.y + dangerLocalY;

        GameObject lineObj = new GameObject("DangerLine");
        lineObj.transform.SetParent(parent.transform, false);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.SetPosition(0, new Vector3(-CONTAINER_WIDTH / 2f, dangerLocalY, -0.1f));
        lr.SetPosition(1, new Vector3(CONTAINER_WIDTH / 2f, dangerLocalY, -0.1f));
        lr.startWidth = 0.04f;
        lr.endWidth = 0.04f;

        Material mat = new Material(Shader.Find("Sprites/Default"));
        Color lineColor = new Color(0.9f, 0.2f, 0.2f, 0.6f);
        mat.color = lineColor;
        lr.material = mat;
        lr.startColor = lineColor;
        lr.endColor = lineColor;

        // Store the danger Y for GameManager
        PlayerPrefs.SetFloat("DangerY", dangerWorldY);
    }

    // ─── Fruit Prefab ─────────────────────────────────────────────────────────

    GameObject BuildFruitPrefab()
    {
        GameObject go = new GameObject("FruitPrefab");
        go.transform.position = new Vector3(-9999f, -9999f, 0f); // park off-screen so Awake() fires

        go.AddComponent<Rigidbody2D>();
        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 2;

        go.AddComponent<Fruit>();

        DontDestroyOnLoad(go);
        return go;
    }

    // ─── Drop Point ───────────────────────────────────────────────────────────

    Transform BuildDropPoint()
    {
        // Top of container interior = CONTAINER_HEIGHT/2 = 3.5; sit 1.5 units above that
        GameObject dp = new GameObject("DropPoint");
        dp.transform.position = new Vector3(0f, CONTAINER_HEIGHT / 2f + 1.5f, 0f);
        return dp.transform;
    }

    // ─── Drop Line ────────────────────────────────────────────────────────────

    LineRenderer BuildDropLine()
    {
        GameObject lineObj = new GameObject("DropLine");
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth = 0.03f;
        lr.endWidth = 0.03f;

        Material mat = new Material(Shader.Find("Sprites/Default"));
        Color c = new Color(0f, 0f, 0f, 0.25f);
        mat.color = c;
        lr.material = mat;
        lr.startColor = c;
        lr.endColor = c;
        lr.sortingOrder = 5;

        return lr;
    }

    // ─── UI ───────────────────────────────────────────────────────────────────

    (Canvas, TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI, GameObject, TextMeshProUGUI) BuildUI()
    {
        GameObject canvasGo = new GameObject("Canvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
        canvasGo.AddComponent<GraphicRaycaster>();

        // Score
        TextMeshProUGUI scoreText = CreateLabel(canvasGo, "ScoreText",
            new Vector2(-300, -60), new Vector2(400, 80),
            "Score: 0", 36, TextAlignmentOptions.Left);

        // High Score
        TextMeshProUGUI highScoreText = CreateLabel(canvasGo, "HighScoreText",
            new Vector2(-300, -140), new Vector2(400, 60),
            "Best: 0", 28, TextAlignmentOptions.Left);

        // Next fruit label
        TextMeshProUGUI nextLabel = CreateLabel(canvasGo, "NextLabel",
            new Vector2(200, -60), new Vector2(350, 80),
            "Next: Cherry", 30, TextAlignmentOptions.Right);

        // Game Over Panel
        GameObject gameOverPanel = new GameObject("GameOverPanel");
        gameOverPanel.transform.SetParent(canvasGo.transform, false);
        Image panelImg = gameOverPanel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.75f);
        RectTransform prt = gameOverPanel.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;

        TextMeshProUGUI gameOverScore = CreateLabel(gameOverPanel, "GameOverScore",
            new Vector2(0, 100), new Vector2(700, 300),
            "Score: 0\nBest: 0", 52, TextAlignmentOptions.Center);
        gameOverScore.color = Color.white;

        TextMeshProUGUI gameOverTitle = CreateLabel(gameOverPanel, "GameOverTitle",
            new Vector2(0, 350), new Vector2(700, 150),
            "GAME OVER", 72, TextAlignmentOptions.Center);
        gameOverTitle.color = new Color(1f, 0.4f, 0.3f);
        gameOverTitle.fontStyle = FontStyles.Bold;

        // Restart button
        GameObject btnGo = new GameObject("RestartButton");
        btnGo.transform.SetParent(gameOverPanel.transform, false);
        Image btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.3f, 0.75f, 0.4f);
        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        RectTransform brt = btnGo.GetComponent<RectTransform>();
        brt.anchoredPosition = new Vector2(0, -150);
        brt.sizeDelta = new Vector2(300, 80);
        btn.onClick.AddListener(() => FindFirstObjectByType<GameManager>()?.RestartGame());

        TextMeshProUGUI btnLabel = CreateLabel(btnGo, "BtnLabel",
            Vector2.zero, new Vector2(300, 80),
            "PLAY AGAIN", 36, TextAlignmentOptions.Center);
        btnLabel.color = Color.white;
        btnLabel.fontStyle = FontStyles.Bold;

        gameOverPanel.SetActive(false);

        return (canvas, scoreText, highScoreText, nextLabel, gameOverPanel, gameOverScore);
    }

    TextMeshProUGUI CreateLabel(GameObject parent, string name, Vector2 anchoredPos, Vector2 sizeDelta,
                                  string text, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = new Color(0.25f, 0.15f, 0.05f);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        return tmp;
    }

    // ─── Game Manager ─────────────────────────────────────────────────────────

    void BuildGameManager(Transform container, GameObject fruitPrefab, Transform dropPoint,
        LineRenderer dropLine, TextMeshProUGUI scoreText, TextMeshProUGUI highScoreText,
        TextMeshProUGUI nextLabel, GameObject gameOverPanel, TextMeshProUGUI gameOverScore)
    {
        GameObject gmGo = new GameObject("GameManager");
        GameManager gm = gmGo.AddComponent<GameManager>();

        gm.fruitData = fruitData;
        gm.container = container;
        gm.fruitPrefab = fruitPrefab;
        gm.dropPoint = dropPoint;
        gm.dropLine = dropLine;
        gm.scoreText = scoreText;
        gm.highScoreText = highScoreText;
        gm.nextFruitLabel = nextLabel;
        gm.gameOverPanel = gameOverPanel;
        gm.gameOverScoreText = gameOverScore;

        // Container is at world origin; danger line is 75% up from the bottom
        float dangerY = -CONTAINER_HEIGHT / 2f + CONTAINER_HEIGHT * 0.75f;
        gm.containerTopY = dangerY;

        gm.dropXMin = -CONTAINER_WIDTH / 2f + 0.4f;
        gm.dropXMax =  CONTAINER_WIDTH / 2f - 0.4f;
    }

    // ─── Background ───────────────────────────────────────────────────────────

    void BuildBackground()
    {
        GameObject bg = new GameObject("Background");
        SpriteRenderer sr = bg.AddComponent<SpriteRenderer>();
        sr.sprite = CreatePixelSprite(new Color(0.96f, 0.91f, 0.82f));
        sr.sortingOrder = -10;
        bg.transform.position = new Vector3(0f, 0f, 1f);
        bg.transform.localScale = new Vector3(30f, 30f, 1f);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    Sprite CreatePixelSprite(Color color)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}


/*using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Add this script to an empty GameObject called "Bootstrapper" in a new empty scene.
/// It will procedurally create the entire game scene at runtime.
/// You still need to:
///   1. Create a FruitData asset (Assets > Create > SuikaGame > FruitData) and fill it in.
///   2. Assign that FruitData asset to the Bootstrapper's fruitData field.
///   3. Make sure TextMeshPro is imported (Window > Package Manager > TMP Essential Resources).
/// </summary>
public class SceneBootstrapper : MonoBehaviour
{
    [Header("Required")]
    public FruitData fruitData;

    // Container dimensions (must match ContainerBuilder values)
    private const float CONTAINER_WIDTH = 5f;
    private const float CONTAINER_HEIGHT = 7f;
    private const float WALL_THICKNESS = 0.3f;

    void Awake()
    {
        if (fruitData == null)
        {
            Debug.LogError("SceneBootstrapper: FruitData not assigned!");
            return;
        }

        SetupCamera();
        SetupPhysics();

        GameObject container = BuildContainer();
        GameObject fruitPrefab = BuildFruitPrefab();
        Transform dropPoint = BuildDropPoint();
        LineRenderer dropLine = BuildDropLine();

        (Canvas canvas, TextMeshProUGUI scoreText, TextMeshProUGUI highScoreText,
         TextMeshProUGUI nextLabel, GameObject gameOverPanel, TextMeshProUGUI gameOverScore) = BuildUI();

        BuildGameManager(container.transform, fruitPrefab, dropPoint, dropLine,
                         scoreText, highScoreText, nextLabel, gameOverPanel, gameOverScore);

        // Build background
        BuildBackground();
    }

    // ─── Camera ───────────────────────────────────────────────────────────────

    void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 7f;
        cam.transform.position = new Vector3(0f, 0.5f, -10f);
        cam.backgroundColor = new Color(0.96f, 0.92f, 0.84f);
        cam.clearFlags = CameraClearFlags.SolidColor;
    }

    // ─── Physics ──────────────────────────────────────────────────────────────

    void SetupPhysics()
    {
        Physics2D.gravity = new Vector2(0f, -18f);
    }

    // ─── Container ────────────────────────────────────────────────────────────

    GameObject BuildContainer()
    {
        GameObject container = new GameObject("Container");
        container.transform.position = new Vector3(0f, -1f, 0f);

        // Floor
        AddWall(container, "Floor",
            new Vector2(0f, -CONTAINER_HEIGHT / 2f - WALL_THICKNESS / 2f),
            new Vector2(CONTAINER_WIDTH + WALL_THICKNESS * 2f, WALL_THICKNESS),
            new Color(0.7f, 0.6f, 0.4f));

        // Left wall
        AddWall(container, "LeftWall",
            new Vector2(-CONTAINER_WIDTH / 2f - WALL_THICKNESS / 2f, 0f),
            new Vector2(WALL_THICKNESS, CONTAINER_HEIGHT + WALL_THICKNESS),
            new Color(0.75f, 0.65f, 0.45f));

        // Right wall
        AddWall(container, "RightWall",
            new Vector2(CONTAINER_WIDTH / 2f + WALL_THICKNESS / 2f, 0f),
            new Vector2(WALL_THICKNESS, CONTAINER_HEIGHT + WALL_THICKNESS),
            new Color(0.75f, 0.65f, 0.45f));

        // Danger line
        AddDangerLine(container, 0.75f);

        return container;
    }

    void AddWall(GameObject parent, string wallName, Vector2 localPos, Vector2 size, Color color)
    {
        GameObject wall = new GameObject(wallName);
        wall.transform.SetParent(parent.transform, false);
        wall.transform.localPosition = localPos;

        BoxCollider2D col = wall.AddComponent<BoxCollider2D>();
        col.size = size;
 
        // Visual using a 1x1 sprite
        SpriteRenderer sr = wall.AddComponent<SpriteRenderer>();
        sr.sprite = CreatePixelSprite(Color.white);
        sr.color = color;
        sr.drawMode = SpriteDrawMode.Sliced;
        wall.transform.localScale = new Vector3(size.x, size.y, 1f);
        col.size = Vector2.one; // scale handles dimensions
    }

    void AddDangerLine(GameObject parent, float heightFraction)
    {
        float dangerLocalY = -CONTAINER_HEIGHT / 2f + CONTAINER_HEIGHT * heightFraction;
        float dangerWorldY = parent.transform.position.y + dangerLocalY;

        GameObject lineObj = new GameObject("DangerLine");
        lineObj.transform.SetParent(parent.transform, false);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.SetPosition(0, new Vector3(-CONTAINER_WIDTH / 2f, dangerLocalY, -0.1f));
        lr.SetPosition(1, new Vector3(CONTAINER_WIDTH / 2f, dangerLocalY, -0.1f));
        lr.startWidth = 0.04f;
        lr.endWidth = 0.04f;

        Material mat = new Material(Shader.Find("Sprites/Default"));
        Color lineColor = new Color(0.9f, 0.2f, 0.2f, 0.6f);
        mat.color = lineColor;
        lr.material = mat;
        lr.startColor = lineColor;
        lr.endColor = lineColor;

        // Store the danger Y for GameManager
        PlayerPrefs.SetFloat("DangerY", dangerWorldY);
    }

    // ─── Fruit Prefab ─────────────────────────────────────────────────────────

    GameObject BuildFruitPrefab()
    {
        GameObject go = new GameObject("FruitPrefab");
        go.SetActive(false); // prefab-like; never actually in the scene visible

        go.AddComponent<Rigidbody2D>();
        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 2;

        go.AddComponent<Fruit>();

        DontDestroyOnLoad(go);
        return go;
    }

    // ─── Drop Point ───────────────────────────────────────────────────────────

    Transform BuildDropPoint()
    {
        GameObject dp = new GameObject("DropPoint");
        dp.transform.position = new Vector3(0f, 5f, 0f);
        return dp.transform;
    }

    // ─── Drop Line ────────────────────────────────────────────────────────────

    LineRenderer BuildDropLine()
    {
        GameObject lineObj = new GameObject("DropLine");
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth = 0.03f;
        lr.endWidth = 0.03f;

        Material mat = new Material(Shader.Find("Sprites/Default"));
        Color c = new Color(0f, 0f, 0f, 0.25f);
        mat.color = c;
        lr.material = mat;
        lr.startColor = c;
        lr.endColor = c;
        lr.sortingOrder = 5;

        return lr;
    }

    // ─── UI ───────────────────────────────────────────────────────────────────

    (Canvas, TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI, GameObject, TextMeshProUGUI) BuildUI()
    {
        GameObject canvasGo = new GameObject("Canvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
        canvasGo.AddComponent<GraphicRaycaster>();

        // Score
        TextMeshProUGUI scoreText = CreateLabel(canvasGo, "ScoreText",
            new Vector2(-300, -60), new Vector2(400, 80),
            "Score: 0", 36, TextAlignmentOptions.Left);

        // High Score
        TextMeshProUGUI highScoreText = CreateLabel(canvasGo, "HighScoreText",
            new Vector2(-300, -140), new Vector2(400, 60),
            "Best: 0", 28, TextAlignmentOptions.Left);

        // Next fruit label
        TextMeshProUGUI nextLabel = CreateLabel(canvasGo, "NextLabel",
            new Vector2(200, -60), new Vector2(350, 80),
            "Next: Cherry", 30, TextAlignmentOptions.Right);

        // Game Over Panel
        GameObject gameOverPanel = new GameObject("GameOverPanel");
        gameOverPanel.transform.SetParent(canvasGo.transform, false);
        Image panelImg = gameOverPanel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.75f);
        RectTransform prt = gameOverPanel.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;

        TextMeshProUGUI gameOverScore = CreateLabel(gameOverPanel, "GameOverScore",
            new Vector2(0, 100), new Vector2(700, 300),
            "Score: 0\nBest: 0", 52, TextAlignmentOptions.Center);
        gameOverScore.color = Color.white;

        TextMeshProUGUI gameOverTitle = CreateLabel(gameOverPanel, "GameOverTitle",
            new Vector2(0, 350), new Vector2(700, 150),
            "GAME OVER", 72, TextAlignmentOptions.Center);
        gameOverTitle.color = new Color(1f, 0.4f, 0.3f);
        gameOverTitle.fontStyle = FontStyles.Bold;

        // Restart button
        GameObject btnGo = new GameObject("RestartButton");
        btnGo.transform.SetParent(gameOverPanel.transform, false);
        Image btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.3f, 0.75f, 0.4f);
        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        RectTransform brt = btnGo.GetComponent<RectTransform>();
        brt.anchoredPosition = new Vector2(0, -150);
        brt.sizeDelta = new Vector2(300, 80);
        btn.onClick.AddListener(() => FindFirstObjectByType<GameManager>()?.RestartGame());

        TextMeshProUGUI btnLabel = CreateLabel(btnGo, "BtnLabel",
            Vector2.zero, new Vector2(300, 80),
            "PLAY AGAIN", 36, TextAlignmentOptions.Center);
        btnLabel.color = Color.white;
        btnLabel.fontStyle = FontStyles.Bold;

        gameOverPanel.SetActive(false);

        return (canvas, scoreText, highScoreText, nextLabel, gameOverPanel, gameOverScore);
    }

    TextMeshProUGUI CreateLabel(GameObject parent, string name, Vector2 anchoredPos, Vector2 sizeDelta,
                                  string text, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = new Color(0.25f, 0.15f, 0.05f);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        return tmp;
    }

    // ─── Game Manager ─────────────────────────────────────────────────────────

    void BuildGameManager(Transform container, GameObject fruitPrefab, Transform dropPoint,
        LineRenderer dropLine, TextMeshProUGUI scoreText, TextMeshProUGUI highScoreText,
        TextMeshProUGUI nextLabel, GameObject gameOverPanel, TextMeshProUGUI gameOverScore)
    {
        GameObject gmGo = new GameObject("GameManager");
        GameManager gm = gmGo.AddComponent<GameManager>();

        gm.fruitData = fruitData;
        gm.container = container;
        gm.fruitPrefab = fruitPrefab;
        gm.dropPoint = dropPoint;
        gm.dropLine = dropLine;
        gm.scoreText = scoreText;
        gm.highScoreText = highScoreText;
        gm.nextFruitLabel = nextLabel;
        gm.gameOverPanel = gameOverPanel;
        gm.gameOverScoreText = gameOverScore;

        // Danger Y = container world Y + local danger offset
        float containerY = container.position.y;
        float dangerLocalY = -CONTAINER_HEIGHT / 2f + CONTAINER_HEIGHT * 0.75f;
        gm.containerTopY = containerY + dangerLocalY;

        gm.dropXMin = -CONTAINER_WIDTH / 2f + 0.3f;
        gm.dropXMax = CONTAINER_WIDTH / 2f - 0.3f;
    }

    // ─── Background ───────────────────────────────────────────────────────────

    void BuildBackground()
    {
        GameObject bg = new GameObject("Background");
        SpriteRenderer sr = bg.AddComponent<SpriteRenderer>();
        sr.sprite = CreatePixelSprite(new Color(0.96f, 0.91f, 0.82f));
        sr.sortingOrder = -10;
        bg.transform.position = new Vector3(0f, 0f, 1f);
        bg.transform.localScale = new Vector3(30f, 30f, 1f);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    Sprite CreatePixelSprite(Color color)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}*/