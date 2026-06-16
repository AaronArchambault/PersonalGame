using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

// ─────────────────────────────────────────────────────────────────────────────
//  LevelGenerator
//  Procedurally builds LevelData assets based on a difficulty curve.
//  Attach to a GameObject in the scene (or use static methods from tooling).
//
//  Call Generate(levelNumber) to produce a fully configured LevelData,
//  then either save it as an asset (Editor) or hand it to BoardManager (runtime).
// ─────────────────────────────────────────────────────────────────────────────
public class LevelGenerator : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Grid")]
    public int boardWidth  = 8;
    public int boardHeight = 8;

    [Header("Difficulty Curve")]
    [Tooltip("Level number at which all obstacle types are unlocked")]
    public int fullDifficultyAtLevel = 20;

    [Header("Obstacle Density")]
    [Range(0f, 0.3f)] public float maxLockedRatio    = 0.12f;
    [Range(0f, 0.3f)] public float maxIceRatio       = 0.15f;
    [Range(0f, 0.2f)] public float maxChocolateRatio = 0.08f;

    [Header("Runtime")]
    public LevelData runtimeLevel;  // populated at Start if no BoardManager.currentLevel

    // ── Called by BoardManager at Start if a generator is present ─────────────
    public void PopulateBoard(BoardManager board)
    {
        int lvl = PlayerPrefs.GetInt("CurrentLevel", 1);
        runtimeLevel = Generate(lvl);
        board.LoadFromData(runtimeLevel);

        var goalMgr = FindObjectOfType<LevelGoalManager>();
        if (goalMgr != null)
            goalMgr.currentLevel = runtimeLevel;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Main generation entry point
    // ─────────────────────────────────────────────────────────────────────────
    public LevelData Generate(int levelNumber, int? seed = null)
    {
        var data = ScriptableObject.CreateInstance<LevelData>();
        data.levelNumber  = levelNumber;
        data.levelTitle   = $"Level {levelNumber}";
        data.boardWidth   = boardWidth;
        data.boardHeight  = boardHeight;

        float t = Mathf.Clamp01((float)(levelNumber - 1) / (fullDifficultyAtLevel - 1));

        // ── Moves ─────────────────────────────────────────────────────────────
        // Fewer moves as levels go up (30 → 15)
        data.movesAllowed = Mathf.RoundToInt(Mathf.Lerp(30, 15, t));

        // ── Goals ─────────────────────────────────────────────────────────────
        data.goals = BuildGoals(levelNumber, t);

        // ── Cell layout ───────────────────────────────────────────────────────
        var rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random(levelNumber * 997 + 13);
        data.cells = BuildCells(t, rng);

        return data;
    }

    // ── Goal building ─────────────────────────────────────────────────────────
    GoalEntry[] BuildGoals(int level, float t)
    {
        var goals = new List<GoalEntry>();

        // Primary goal: always clear a colour (scaled count)
        int colourCount = Mathf.RoundToInt(Mathf.Lerp(15, 45, t));
        int targetGem   = (level - 1) % 5;   // rotate through colours
        goals.Add(new GoalEntry
        {
            type          = GoalType.ClearGemColor,
            targetGemType = targetGem,
            targetCount   = colourCount,
            label         = GemColorName(targetGem)
        });

        // Secondary goal: clear ice (from level 5)
        if (level >= 5)
        {
            int iceCount = Mathf.RoundToInt(Mathf.Lerp(3, 12, t));
            goals.Add(new GoalEntry
            {
                type        = GoalType.ClearObstacle,
                targetCount = iceCount,
                label       = "Break Ice"
            });
        }

        // Tertiary goal: score target (from level 10)
        if (level >= 10)
        {
            int scoreTarget = Mathf.RoundToInt(Mathf.Lerp(500, 3000, t) / 100f) * 100;
            goals.Add(new GoalEntry
            {
                type        = GoalType.ReachScore,
                targetCount = scoreTarget,
                label       = "Score"
            });
        }

        return goals.ToArray();
    }

    string GemColorName(int idx) => idx switch
    {
        0 => "Clear Red",
        1 => "Clear Blue",
        2 => "Clear Green",
        3 => "Clear Yellow",
        4 => "Clear Purple",
        _ => "Clear Gems"
    };

    // ── Cell layout ───────────────────────────────────────────────────────────
    CellData[] BuildCells(float t, System.Random rng)
    {
        int total = boardWidth * boardHeight;
        var cells = new CellData[total];

        // Compute obstacle budgets
        int lockedBudget    = Mathf.RoundToInt(total * maxLockedRatio    * t);
        int iceBudget       = Mathf.RoundToInt(total * maxIceRatio       * t);
        int chocolateBudget = Mathf.RoundToInt(total * maxChocolateRatio * t);

        // Shuffle all positions
        var positions = new List<int>();
        for (int i = 0; i < total; i++) positions.Add(i);
        for (int i = positions.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (positions[i], positions[j]) = (positions[j], positions[i]);
        }

        int posIdx = 0;

        // Place obstacles
        for (int i = 0; i < lockedBudget && posIdx < positions.Count; i++, posIdx++)
            cells[positions[posIdx]] = new CellData { gemType = rng.Next(0, 5), obstacleType = (int)ObstacleType.Locked, obstacleHp = 1 };

        for (int i = 0; i < iceBudget && posIdx < positions.Count; i++, posIdx++)
        {
            int hp = t > 0.5f && rng.NextDouble() > 0.6f ? 2 : 1;
            cells[positions[posIdx]] = new CellData { gemType = rng.Next(0, 5), obstacleType = (int)ObstacleType.Ice, obstacleHp = hp };
        }

        for (int i = 0; i < chocolateBudget && posIdx < positions.Count; i++, posIdx++)
            cells[positions[posIdx]] = new CellData { gemType = rng.Next(0, 5), obstacleType = (int)ObstacleType.Chocolate, obstacleHp = 1 };

        // Fill remaining with random normal gems
        for (int i = 0; i < total; i++)
        {
            if (cells[i] == null)
                cells[i] = new CellData { gemType = rng.Next(0, 5) };
        }

        return cells;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Editor utility — generate and save a range of levels as assets
    // ─────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("Generate Levels 1-30 as Assets")]
    public void GenerateLevelAssets()
    {
        string folder = "Assets/Levels";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "Levels");

        for (int n = 1; n <= 30; n++)
        {
            var data = Generate(n);
            string path = $"{folder}/Level_{n:D3}.asset";
            AssetDatabase.CreateAsset(data, path);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Match3] Generated 30 level assets in Assets/Levels/");
    }
#endif
}