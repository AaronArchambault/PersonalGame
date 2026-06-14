using UnityEngine;

[CreateAssetMenu(fileName = "LevelData", menuName = "SuikaPlus/LevelData")]
public class LevelData : ScriptableObject
{
    [Header("Identity")]
    public string levelName = "Classic";
    public string description = "The original experience.";
    public Sprite thumbnail; // optional preview image

    [Header("Container")]
    public float containerWidth  = 5f;
    public float containerHeight = 7f;

    [Header("Physics")]
    public float gravityScale    = 18f;   // Physics2D.gravity.y magnitude
    public float fruitGravity    = 1.5f;  // per-fruit rb.gravityScale

    [Header("Scoring")]
    public float scoreMultiplier = 1f;

    [Header("Power-ups")]
    public bool powerUpsEnabled  = true;
    public int  powerUpSpawnEveryNDrops = 8; // how often a power-up slot is offered

    [Header("Obstacles")]
    public ObstacleConfig[] obstacles; // placed obstacles for this level

    [Header("Unlock")]
    public int  scoreToUnlock = 0;  // 0 = always unlocked
}

[System.Serializable]
public class ObstacleConfig
{
    public ObstacleType type;
    public Vector2 position;
    public Vector2 size = Vector2.one;
    public float   moveSpeed  = 0f;  // 0 = static
    public float   moveRange  = 0f;
    public bool    horizontal = true; // moving direction
}

public enum ObstacleType
{
    StaticBlock,
    MovingPlatform,
    StickyZone,
    Bumper,
    DividerWall,
    GravityWell
}