using UnityEngine;

/// <summary>
/// Reads the active LevelData and spawns obstacles into the scene.
/// Attach to an empty GameObject called "ObstacleSpawner".
/// Call SpawnForLevel() from GameManager.LoadLevel().
/// </summary>
public class ObstacleSpawner : MonoBehaviour
{
    public static ObstacleSpawner Instance { get; private set; }

    [Header("Sprites (optional — will use colored squares if null)")]
    public Sprite blockSprite;
    public Sprite bumperSprite;

    private GameObject obstacleRoot;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void SpawnForLevel(LevelData level)
    {
        ClearObstacles();

        if (level.obstacles == null || level.obstacles.Length == 0) return;

        obstacleRoot = new GameObject("Obstacles");

        foreach (ObstacleConfig cfg in level.obstacles)
            SpawnObstacle(cfg);
    }

    public void ClearObstacles()
    {
        if (obstacleRoot != null)
            Destroy(obstacleRoot);
    }

    void SpawnObstacle(ObstacleConfig cfg)
    {
        GameObject go = new GameObject(cfg.type.ToString());
        go.transform.SetParent(obstacleRoot.transform, false);
        go.transform.position = cfg.position;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 1;

        switch (cfg.type)
        {
            case ObstacleType.StaticBlock:
            case ObstacleType.DividerWall:
                SetupBoxObstacle(go, cfg);
                StaticBlock sb = go.AddComponent<StaticBlock>();
                sr.color = cfg.type == ObstacleType.DividerWall
                    ? new Color(0.55f, 0.45f, 0.35f)
                    : new Color(0.4f, 0.4f, 0.45f);
                go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
                break;

            case ObstacleType.MovingPlatform:
                SetupBoxObstacle(go, cfg);
                Rigidbody2D mpRb = go.AddComponent<Rigidbody2D>();
                mpRb.bodyType = RigidbodyType2D.Kinematic;
                MovingPlatform mp = go.AddComponent<MovingPlatform>();
                mp.moveSpeed  = cfg.moveSpeed;
                mp.moveRange  = cfg.moveRange;
                mp.horizontal = cfg.horizontal;
                sr.color = new Color(0.3f, 0.55f, 0.75f);
                break;

            case ObstacleType.StickyZone:
                SetupBoxObstacle(go, cfg, isTrigger: true);
                go.AddComponent<StickyZone>();
                sr.color = new Color(0.6f, 0.45f, 0.2f, 0.45f);
                break;

            case ObstacleType.Bumper:
                float r = Mathf.Max(cfg.size.x, cfg.size.y) * 0.5f;
                CircleCollider2D cc = go.AddComponent<CircleCollider2D>();
                cc.radius = r;
                go.transform.localScale = new Vector3(r * 2f, r * 2f, 1f);
                cc.radius = 0.5f;
                go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
                go.AddComponent<Bumper>().bounceForce = 7f;
                sr.sprite = bumperSprite != null ? bumperSprite : CreateCircleSprite();
                sr.color = new Color(0.9f, 0.3f, 0.6f);
                break;

            case ObstacleType.GravityWell:
                float wr = Mathf.Max(cfg.size.x, cfg.size.y) * 0.5f;
                CircleCollider2D wc = go.AddComponent<CircleCollider2D>();
                wc.radius = wr;
                wc.isTrigger = true;
                go.transform.localScale = new Vector3(wr * 2f, wr * 2f, 1f);
                wc.radius = 0.5f;
                GravityWell gw = go.AddComponent<GravityWell>();
                gw.pullRadius = wr;
                sr.sprite = CreateCircleSprite();
                sr.color = new Color(0.4f, 0.15f, 0.7f, 0.35f);
                break;
        }

        if (sr.sprite == null)
            sr.sprite = CreateSquareSprite();
    }

    void SetupBoxObstacle(GameObject go, ObstacleConfig cfg, bool isTrigger = false)
    {
        go.transform.localScale = new Vector3(cfg.size.x, cfg.size.y, 1f);
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
        col.isTrigger = isTrigger;
    }

    Sprite CreateSquareSprite()
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    Sprite CreateCircleSprite()
    {
        int s = 64;
        Texture2D tex = new Texture2D(s, s);
        float c = s / 2f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) < c ? Color.white : Color.clear);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
    }
}