using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // ─── Inspector references ────────────────────────────────────────────────
    [Header("Core References")]
    public FruitData   fruitData;
    public Transform   dropPoint;
    public GameObject  fruitPrefab;
    public LineRenderer dropLine;

    [Header("UI")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI highScoreText;
    public TextMeshProUGUI nextFruitLabel;
    public TextMeshProUGUI multiplierText; // shows "2×" when active
    public TextMeshProUGUI levelNameText;
    public GameObject      gameOverPanel;
    public TextMeshProUGUI gameOverScoreText;
    public GameObject      levelSelectPanel; // the LevelSelectUI panel

    [Header("Settings")]
    public float containerTopY = 3.5f;
    public float dropXMin      = -2.2f;
    public float dropXMax      =  2.2f;
    public int   maxNextFruitIndex = 4;

    [Header("Classic Mode Toggle")]
    [Tooltip("When true: no power-ups, no obstacles — pure Suika experience")]
    public bool classicMode = false;

    // ─── Private state ───────────────────────────────────────────────────────
    private int   currentScore   = 0;
    private int   highScore      = 0;
    private int   dropCount      = 0;
    private int   nextFruitIndex = 0;
    private int   currentFruitIndex = 0;

    private Fruit heldFruit  = null;
    private bool  canDrop    = true;
    private bool  isGameOver = false;
    private float dropX      = 0f;

    [HideInInspector] public LevelData activeLevel = null;
    public float activeLevelFruitGravity => activeLevel?.fruitGravity ?? 1.5f;

    private List<Fruit>             activeFruits   = new List<Fruit>();
    private Dictionary<Fruit,float> overflowTimers = new Dictionary<Fruit,float>();
    private float overflowCheckDelay = 0f;
    private const float OVERFLOW_CHECK_INTERVAL = 0.5f;
    private const float OVERFLOW_GRACE_PERIOD   = 2f;

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        highScore = PlayerPrefs.GetInt("HighScore_Global", 0);
        UpdateScoreUI();

        if (gameOverPanel  != null) gameOverPanel.SetActive(false);
        if (multiplierText != null) multiplierText.gameObject.SetActive(false);

        // Show level select if available, otherwise start default
        if (levelSelectPanel != null)
            levelSelectPanel.SetActive(true);
        else
            StartGame(null);
    }

    void Update()
    {
        if (isGameOver) return;
        if (heldFruit == null && canDrop) return; // waiting for level select

        HandleInput();
        CheckOverflow();
        UpdateMultiplierUI();
    }

    // ─── Level loading ───────────────────────────────────────────────────────

    public void LoadLevel(LevelData level)
    {
        activeLevel = level;
        StartGame(level);
    }

    void StartGame(LevelData level)
    {
        // Reset state
        currentScore = 0;
        dropCount    = 0;
        isGameOver   = false;
        canDrop      = true;
        activeFruits.Clear();
        overflowTimers.Clear();

        // Apply level settings
        if (level != null)
        {
            Physics2D.gravity = new Vector2(0f, -level.gravityScale);
            if (levelNameText != null) levelNameText.text = level.levelName;

            // Spawn obstacles (unless classic mode)
            if (!classicMode && ObstacleSpawner.Instance != null)
                ObstacleSpawner.Instance.SpawnForLevel(level);
        }
        else
        {
            Physics2D.gravity = new Vector2(0f, -18f);
            if (levelNameText != null) levelNameText.text = "Classic";
        }

        UpdateScoreUI();
        PrepareNextFruit();
        SpawnHeldFruit();
    }

    // ─── Input ───────────────────────────────────────────────────────────────

    void HandleInput()
    {
        if (!canDrop || heldFruit == null) return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        float mouseX = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f)).x;
        dropX = Mathf.Clamp(mouseX, dropXMin, dropXMax);

        Vector3 pos = dropPoint.position;
        pos.x = dropX;
        dropPoint.position = pos;
        heldFruit.transform.position = new Vector3(dropX, dropPoint.position.y, 0f);

        if (dropLine != null)
        {
            dropLine.SetPosition(0, new Vector3(dropX, dropPoint.position.y - 0.3f, 0f));
            dropLine.SetPosition(1, new Vector3(dropX, -8f, 0f));
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
            DropFruit();
    }

    // ─── Fruit spawning ──────────────────────────────────────────────────────

    void PrepareNextFruit()
    {
        nextFruitIndex = Random.Range(0, Mathf.Min(maxNextFruitIndex + 1, fruitData.fruits.Length));
        if (nextFruitLabel != null)
            nextFruitLabel.text = "Next: " + fruitData.fruits[nextFruitIndex].fruitName;
    }

    void SpawnHeldFruit()
    {
        currentFruitIndex = nextFruitIndex;
        PrepareNextFruit();

        // Apply shrink power-up
        int spawnIndex = currentFruitIndex;
        if (!classicMode && PowerUpManager.Instance != null && PowerUpManager.Instance.isShrinkActive)
        {
            spawnIndex = Mathf.Max(0, spawnIndex - 1);
            PowerUpManager.Instance.ConsumeShrink();
        }

        GameObject go = Instantiate(fruitPrefab, dropPoint.position, Quaternion.identity);
        heldFruit = go.GetComponent<Fruit>();
        heldFruit.Initialize(spawnIndex, fruitData);
        heldFruit.MakeKinematic();
    }

    void DropFruit()
    {
        if (heldFruit == null || !canDrop) return;

        canDrop = false;
        heldFruit.Drop();
        activeFruits.Add(heldFruit);
        heldFruit = null;

        dropCount++;

        // Offer power-up every N drops
        if (!classicMode && activeLevel != null && activeLevel.powerUpsEnabled
            && PowerUpManager.Instance != null
            && dropCount % activeLevel.powerUpSpawnEveryNDrops == 0)
        {
            PowerUpManager.Instance.OfferRandomPowerUp();
        }

        StartCoroutine(SpawnNextAfterDelay(0.6f));
    }

    IEnumerator SpawnNextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!isGameOver) { SpawnHeldFruit(); canDrop = true; }
    }

    // ─── Merge callback (called from Fruit.cs) ───────────────────────────────

    public void SpawnMergedFruit(int fruitIndex, Vector2 position)
    {
        if (fruitIndex >= fruitData.fruits.Length) return;

        // Wildcard: merge with any fruit (already handled in Fruit.cs; index passed in)
        GameObject go = Instantiate(fruitPrefab, position, Quaternion.identity);
        Fruit f = go.GetComponent<Fruit>();
        f.Initialize(fruitIndex, fruitData);
        f.Drop();
        activeFruits.Add(f);
    }

    // ─── Scoring ─────────────────────────────────────────────────────────────

    public void AddScore(int basePoints)
    {
        float mult = 1f;
        if (!classicMode && activeLevel != null) mult *= activeLevel.scoreMultiplier;
        if (!classicMode && PowerUpManager.Instance != null)
        {
            mult *= PowerUpManager.Instance.scoreMultiplier;
            if (PowerUpManager.Instance.multiplierMergesLeft > 0)
                PowerUpManager.Instance.ConsumeMultiplierCharge();
        }

        currentScore += Mathf.RoundToInt(basePoints * mult);

        if (currentScore > highScore)
        {
            highScore = currentScore;
            PlayerPrefs.SetInt("HighScore_Global", highScore);
        }

        UpdateScoreUI();
    }

    void UpdateScoreUI()
    {
        if (scoreText     != null) scoreText.text     = "Score: " + currentScore;
        if (highScoreText != null) highScoreText.text = "Best: "  + highScore;
    }

    void UpdateMultiplierUI()
    {
        if (multiplierText == null || classicMode) return;
        bool active = PowerUpManager.Instance != null && PowerUpManager.Instance.multiplierMergesLeft > 0;
        multiplierText.gameObject.SetActive(active);
        if (active)
            multiplierText.text = "2× ×" + PowerUpManager.Instance.multiplierMergesLeft;
    }

    // ─── Game over ───────────────────────────────────────────────────────────

    void CheckOverflow()
    {
        overflowCheckDelay -= Time.deltaTime;
        if (overflowCheckDelay > 0f) return;
        overflowCheckDelay = OVERFLOW_CHECK_INTERVAL;

        activeFruits.RemoveAll(f => f == null);
        List<Fruit> dead = new List<Fruit>();
        foreach (var kvp in overflowTimers) if (kvp.Key == null) dead.Add(kvp.Key);
        foreach (var k in dead) overflowTimers.Remove(k);

        foreach (Fruit f in activeFruits)
        {
            if (f == null || f.isMerging) continue;
            bool above = f.transform.position.y > containerTopY;

            if (above && f.hasLanded)
            {
                if (!overflowTimers.ContainsKey(f)) overflowTimers[f] = 0f;
                overflowTimers[f] += OVERFLOW_CHECK_INTERVAL;
                if (overflowTimers[f] >= OVERFLOW_GRACE_PERIOD) { TriggerGameOver(); return; }
            }
            else overflowTimers.Remove(f);
        }
    }

    void TriggerGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;
        canDrop    = false;

        if (heldFruit != null) { Destroy(heldFruit.gameObject); heldFruit = null; }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            if (gameOverScoreText != null)
                gameOverScoreText.text = "Score: " + currentScore + "\nBest: " + highScore;
        }
    }

    public void RestartGame()
    {
        if (ObstacleSpawner.Instance != null) ObstacleSpawner.Instance.ClearObstacles();
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    public void ReturnToLevelSelect()
    {
        if (ObstacleSpawner.Instance != null) ObstacleSpawner.Instance.ClearObstacles();
        if (gameOverPanel  != null) gameOverPanel.SetActive(false);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(true);

        // Reset for fresh game
        isGameOver = false;
        currentScore = 0;
        UpdateScoreUI();
        if (heldFruit != null) { Destroy(heldFruit.gameObject); heldFruit = null; }
        activeFruits.Clear();
        overflowTimers.Clear();
    }
}



/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    public FruitData fruitData;
    public Transform dropPoint;       // Where the preview fruit sits (top center)
    public Transform container;       // The container walls parent
    public GameObject fruitPrefab;    // Base prefab with Fruit script, Rigidbody2D, CircleCollider2D, SpriteRenderer
    public LineRenderer dropLine;     // Dashed vertical line showing where fruit will drop
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI highScoreText;
    public TextMeshProUGUI nextFruitLabel;
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverScoreText;

    [Header("Settings")]
    public float containerTopY = 4.5f;    // Y position of the danger line
    public float dropXMin = -2.2f;
    public float dropXMax = 2.2f;
    public int maxNextFruitIndex = 4;      // Randomly pick from fruits 0..maxNextFruitIndex for drops

    private int currentScore = 0;
    private int highScore = 0;
    private int nextFruitIndex = 0;
    private int currentFruitIndex = 0;

    private Fruit heldFruit;           // The fruit the player is aiming with
    private bool canDrop = true;
    private bool isGameOver = false;

    private float dropX = 0f;

    // Track fruits above the danger line for game-over detection
    private List<Fruit> activeFruits = new List<Fruit>();
    private float overflowCheckDelay = 0f;
    private const float OVERFLOW_CHECK_INTERVAL = 0.5f;
    private const float OVERFLOW_GRACE_PERIOD = 2f; // seconds fruit must be above line

    private Dictionary<Fruit, float> overflowTimers = new Dictionary<Fruit, float>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        highScore = PlayerPrefs.GetInt("HighScore", 0);
        UpdateScoreUI();
        PrepareNextFruit();
        SpawnHeldFruit();

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    void Update()
    {
        if (isGameOver) return;

        HandleInput();
        CheckOverflow();
    }

    // ─── Input ────────────────────────────────────────────────────────────────

    void HandleInput()
    {
        if (!canDrop || heldFruit == null) return;

        // Move preview fruit with mouse/touch (new Input System)
        Vector2 screenPos = Mouse.current.position.ReadValue();
        float mouseX = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f)).x;
        dropX = Mathf.Clamp(mouseX, dropXMin, dropXMax);

        Vector3 pos = dropPoint.position;
        pos.x = dropX;
        dropPoint.position = pos;

        heldFruit.transform.position = new Vector3(dropX, dropPoint.position.y, 0f);

        // Update drop line
        if (dropLine != null)
        {
            dropLine.SetPosition(0, new Vector3(dropX, dropPoint.position.y - 0.3f, 0f));
            dropLine.SetPosition(1, new Vector3(dropX, -6f, 0f));
        }

        // Drop on left click (new Input System)
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            DropFruit();
        }
    }

    // ─── Fruit Spawning ───────────────────────────────────────────────────────

    void PrepareNextFruit()
    {
        nextFruitIndex = Random.Range(0, Mathf.Min(maxNextFruitIndex + 1, fruitData.fruits.Length));
        if (nextFruitLabel != null)
            nextFruitLabel.text = "Next: " + fruitData.fruits[nextFruitIndex].fruitName;
    }

    void SpawnHeldFruit()
    {
        currentFruitIndex = nextFruitIndex;
        PrepareNextFruit();

        GameObject go = Instantiate(fruitPrefab, dropPoint.position, Quaternion.identity);
        heldFruit = go.GetComponent<Fruit>();
        heldFruit.Initialize(currentFruitIndex, fruitData);
        heldFruit.MakeKinematic(); // Doesn't fall until dropped
    }

    void DropFruit()
    {
        if (heldFruit == null || !canDrop) return;

        canDrop = false;
        heldFruit.Drop();
        activeFruits.Add(heldFruit);
        heldFruit = null;

        StartCoroutine(SpawnNextAfterDelay(0.6f));
    }

    IEnumerator SpawnNextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!isGameOver)
        {
            SpawnHeldFruit();
            canDrop = true;
        }
    }

    // Called by Fruit.cs when two fruits merge
    public void SpawnMergedFruit(int fruitIndex, Vector2 position)
    {
        if (fruitIndex >= fruitData.fruits.Length) return;

        GameObject go = Instantiate(fruitPrefab, position, Quaternion.identity);
        Fruit f = go.GetComponent<Fruit>();
        f.Initialize(fruitIndex, fruitData);
        f.Drop(); // already in the container, falls naturally
        activeFruits.Add(f);
    }

    // ─── Scoring ──────────────────────────────────────────────────────────────

    public void AddScore(int points)
    {
        currentScore += points;
        if (currentScore > highScore)
        {
            highScore = currentScore;
            PlayerPrefs.SetInt("HighScore", highScore);
        }
        UpdateScoreUI();
    }

    void UpdateScoreUI()
    {
        if (scoreText != null) scoreText.text = "Score: " + currentScore;
        if (highScoreText != null) highScoreText.text = "Best: " + highScore;
    }

    // ─── Game Over ────────────────────────────────────────────────────────────

    void CheckOverflow()
    {
        overflowCheckDelay -= Time.deltaTime;
        if (overflowCheckDelay > 0f) return;
        overflowCheckDelay = OVERFLOW_CHECK_INTERVAL;

        // Clean destroyed fruits
        activeFruits.RemoveAll(f => f == null);

        List<Fruit> toRemoveTimers = new List<Fruit>();
        foreach (var kvp in overflowTimers)
            if (kvp.Key == null) toRemoveTimers.Add(kvp.Key);
        foreach (var k in toRemoveTimers) overflowTimers.Remove(k);

        foreach (Fruit f in activeFruits)
        {
            if (f == null || f.isMerging) continue;

            bool aboveLine = f.transform.position.y > containerTopY;

            if (aboveLine && f.hasLanded)
            {
                if (!overflowTimers.ContainsKey(f))
                    overflowTimers[f] = 0f;
                overflowTimers[f] += OVERFLOW_CHECK_INTERVAL;

                if (overflowTimers[f] >= OVERFLOW_GRACE_PERIOD)
                {
                    TriggerGameOver();
                    return;
                }
            }
            else
            {
                overflowTimers.Remove(f);
            }
        }
    }

    void TriggerGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;
        canDrop = false;

        if (heldFruit != null)
        {
            Destroy(heldFruit.gameObject);
            heldFruit = null;
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            if (gameOverScoreText != null)
                gameOverScoreText.text = "Score: " + currentScore + "\nBest: " + highScore;
        }

        Debug.Log("Game Over! Score: " + currentScore);
    }

    public void RestartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}
*/







