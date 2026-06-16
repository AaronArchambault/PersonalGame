using UnityEngine;
using DG.Tweening;

// ─────────────────────────────────────────────
//  GemType  — colour / gem category
// ─────────────────────────────────────────────
public enum GemType { Red, Blue, Green, Yellow, Purple, Wild }

// ─────────────────────────────────────────────
//  SpecialType  — power-up behaviour
// ─────────────────────────────────────────────
public enum SpecialType
{
    None,
    StripedH,   // match-4 horizontal → clears entire row
    StripedV,   // match-4 vertical   → clears entire column
    Bomb,       // match-5            → clears 3×3 area
    ColorBomb   // match-5 L/T shape  → clears all gems of one colour
}

// ─────────────────────────────────────────────
//  ObstacleType  — tile overlay
// ─────────────────────────────────────────────
public enum ObstacleType
{
    None,
    Locked,     // needs a match adjacent to unlock
    Ice,        // 1-2 hits to break; gem is frozen in place
    Chocolate   // spreads each turn; match adjacent to clear
}

// ─────────────────────────────────────────────
//  Gem  — data + visual bridge for a single tile
// ─────────────────────────────────────────────
[RequireComponent(typeof(SpriteRenderer))]
public class Gem : MonoBehaviour
{
    // ── Identity ──────────────────────────────
    public GemType    type;
    public SpecialType special = SpecialType.None;
    public ObstacleType obstacle = ObstacleType.None;
    public int        col, row;
    public int        obstacleHp = 1;   // hits remaining for Ice (up to 2)

    // ── Visuals ───────────────────────────────
    [Header("Visuals")]
    public Sprite normalSprite;
    public Sprite stripedHSprite;
    public Sprite stripedVSprite;
    public Sprite bombSprite;
    public Sprite colorBombSprite;
    public Sprite lockedOverlaySprite;   // rendered by a child SpriteRenderer
    public Sprite iceOverlay1Sprite;
    public Sprite iceOverlay2Sprite;
    public Sprite chocolateSprite;

    private SpriteRenderer  sr;
    private SpriteRenderer  overlaySR;  // child GO "Overlay"
    private BoardManager    board;

    // ── Selection highlight ───────────────────
    private static readonly Color SelectedTint = new Color(1f, 1f, 0.4f);

    // ─────────────────────────────────────────
    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        // Create or find overlay child
        var overlayGO = transform.Find("Overlay");
        if (overlayGO == null)
        {
            overlayGO = new GameObject("Overlay").transform;
            overlayGO.SetParent(transform, false);
            overlayGO.localPosition = Vector3.zero;
        }
        overlaySR = overlayGO.GetComponent<SpriteRenderer>();
        if (overlaySR == null) overlaySR = overlayGO.gameObject.AddComponent<SpriteRenderer>();
        overlaySR.sortingOrder = sr.sortingOrder + 1;
    }

    void Start()
    {
        board = FindObjectOfType<BoardManager>();
        RefreshVisuals();
    }

    // ─────────────────────────────────────────
    //  Input
    // ─────────────────────────────────────────
    void OnMouseDown() => board.SelectGem(this);

    // ─────────────────────────────────────────
    //  Visual helpers
    // ─────────────────────────────────────────
    public void RefreshVisuals()
    {
        // Gem sprite
        sr.sprite = special switch
        {
            SpecialType.StripedH  => stripedHSprite  ? stripedHSprite  : normalSprite,
            SpecialType.StripedV  => stripedVSprite  ? stripedVSprite  : normalSprite,
            SpecialType.Bomb      => bombSprite       ? bombSprite      : normalSprite,
            SpecialType.ColorBomb => colorBombSprite  ? colorBombSprite : normalSprite,
            _                     => normalSprite
        };

        // Obstacle overlay
        overlaySR.sprite = obstacle switch
        {
            ObstacleType.Locked    => lockedOverlaySprite,
            ObstacleType.Ice       => obstacleHp >= 2 ? iceOverlay2Sprite : iceOverlay1Sprite,
            ObstacleType.Chocolate => chocolateSprite,
            _                      => null
        };
    }

    // ─────────────────────────────────────────
    //  Selection tint
    // ─────────────────────────────────────────
    public void SetSelected(bool selected)
    {
        sr.color = selected ? SelectedTint : Color.white;
    }

    // ─────────────────────────────────────────
    //  Movement (DOTween)
    // ─────────────────────────────────────────
    public Tweener MoveTo(Vector2 target, float duration = 0.25f)
    {
        return transform.DOMove(target, duration).SetEase(Ease.OutQuad);
    }

    /// Called after a swap animation — snaps logical data then updates position
    public void SetBoardPosition(int c, int r, float cellSize)
    {
        col = c;
        row = r;
        // Visual position already set by DOTween; this just fixes data
    }

    // ─────────────────────────────────────────
    //  Obstacle damage
    // ─────────────────────────────────────────
    /// Returns true when the obstacle is fully cleared
    public bool DamageObstacle()
    {
        if (obstacle == ObstacleType.None) return false;

        obstacleHp--;
        if (obstacleHp <= 0)
        {
            obstacle = ObstacleType.None;
            RefreshVisuals();
            return true;
        }
        RefreshVisuals();
        return false;
    }

    // ─────────────────────────────────────────
    //  Destroy animation
    // ─────────────────────────────────────────
    public void PlayDestroyAnim(System.Action onComplete = null)
    {
        transform.DOScale(Vector3.zero, 0.18f)
                 .SetEase(Ease.InBack)
                 .OnComplete(() =>
                 {
                     onComplete?.Invoke();
                     Destroy(gameObject);
                 });
    }
}