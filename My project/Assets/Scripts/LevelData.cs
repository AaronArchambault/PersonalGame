using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  LevelData  (ScriptableObject)
//  One asset per level. Can be authored by hand in the Inspector,
//  or generated at runtime by LevelGenerator.
// ─────────────────────────────────────────────────────────────────────────────
[CreateAssetMenu(fileName = "LevelData", menuName = "Match3/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Identity")]
    public int    levelNumber   = 1;
    public string levelTitle    = "Level 1";

    [Header("Grid")]
    public int    boardWidth    = 8;
    public int    boardHeight   = 8;

    [Header("Rules")]
    public int    movesAllowed  = 20;
    public int    targetScore   = 0;     // 0 = not a score goal

    [Header("Goals")]
    public GoalEntry[] goals    = new GoalEntry[0];

    [Header("Board Layout")]
    [Tooltip("Row-major: index = y * boardWidth + x.  Leave empty for random.")]
    public CellData[] cells     = new CellData[0];

    // ─────────────────────────────────────────────────────────────────────────
    public CellData GetCell(int x, int y)
    {
        int idx = y * boardWidth + x;
        if (cells == null || idx >= cells.Length)
            return CellData.Default();          // random normal gem
        return cells[idx];
    }

    public bool HasLayout => cells != null && cells.Length == boardWidth * boardHeight;
}

// ─────────────────────────────────────────────────────────────────────────────
//  CellData  — serialisable per-cell descriptor
// ─────────────────────────────────────────────────────────────────────────────
[System.Serializable]
public class CellData
{
    [Tooltip("-1 = random")]
    public int gemType      = -1;
    public int specialType  = 0;    // SpecialType enum index
    public int obstacleType = 0;    // ObstacleType enum index
    public int obstacleHp   = 1;

    public static CellData Default() => new CellData { gemType = -1 };

    public bool IsRandom => gemType < 0;
}