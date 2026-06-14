using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

        // Move preview fruit with mouse/touch
        float mouseX = Camera.main.ScreenToWorldPoint(Input.mousePosition).x;
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

        // Drop on click or tap
        if (Input.GetMouseButtonDown(0))
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