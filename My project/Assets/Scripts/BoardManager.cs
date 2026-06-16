using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

// ─────────────────────────────────────────────────────────────────────────────
//  BoardManager
//  Central controller: owns the grid, drives the game-state machine,
//  handles user input, and orchestrates every sub-system.
// ─────────────────────────────────────────────────────────────────────────────
public class BoardManager : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Grid")]
    public int   width   = 8;
    public int   height  = 8;
    public float cellSize = 1.1f;   // world-units between cell centres

    [Header("Prefabs  (index must match GemType enum)")]
    public GameObject[] gemPrefabs;  // Red, Blue, Green, Yellow, Purple, Wild

    [Header("Animation")]
    public float swapDuration  = 0.22f;
    public float fallDuration  = 0.18f;   // per cell (scaled by distance)
    public float refillDelay   = 0.12f;

    [Header("Chocolate")]
    [Tooltip("Chance (0-1) each turn that chocolate spreads to an adjacent empty cell")]
    public float chocolateSpreadChance = 0.35f;

    // ── Runtime references ────────────────────────────────────────────────────
    private Gem[,]          board;
    private MatchDetector   detector;
    private ScoreManager    scoreManager;
    private LevelGoalManager goalManager;
    private EffectsManager  effects;
    private LevelGenerator  levelGen;

    // ── State ─────────────────────────────────────────────────────────────────
    private enum State { Idle, Swapping, Processing, GameOver }
    private State   state = State.Idle;
    private Gem     selectedGem;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    void Awake()
    {
        detector     = GetComponent<MatchDetector>() ?? gameObject.AddComponent<MatchDetector>();
        scoreManager = FindObjectOfType<ScoreManager>();
        goalManager  = FindObjectOfType<LevelGoalManager>();
        effects      = FindObjectOfType<EffectsManager>();
        levelGen     = FindObjectOfType<LevelGenerator>();
    }

    void Start()
    {
        if (levelGen != null)
            levelGen.PopulateBoard(this);
        else
            FillBoard();

        ClearStartMatches();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Board initialisation
    // ─────────────────────────────────────────────────────────────────────────
    public void FillBoard()
    {
        board = new Gem[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                SpawnGem(x, y, RandomNormalType());
    }

    /// Re-spawn the whole board using data from a LevelData asset
    public void LoadFromData(LevelData data)
    {
        board = new Gem[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                var cell = data.GetCell(x, y);
                var gem  = SpawnGem(x, y, (GemType)cell.gemType);
                gem.obstacle    = (ObstacleType)cell.obstacleType;
                gem.obstacleHp  = cell.obstacleHp;
                gem.special     = (SpecialType)cell.specialType;
                gem.RefreshVisuals();
            }
    }

    Gem SpawnGem(int x, int y, GemType type)
    {
        int idx = Mathf.Clamp((int)type, 0, gemPrefabs.Length - 1);
        var go  = Instantiate(gemPrefabs[idx], CellToWorld(x, y), Quaternion.identity, transform);
        var gem = go.GetComponent<Gem>();
        gem.col  = x;
        gem.row  = y;
        gem.type = type;
        board[x, y] = gem;
        return gem;
    }

    GemType RandomNormalType()
    {
        // Exclude Wild from random spawns
        return (GemType)Random.Range(0, (int)GemType.Wild);
    }

    void ClearStartMatches()
    {
        // Reshuffle any immediate matches so the board starts clean
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    if (board[x, y] == null) continue;
                    if (HasMatchAt(x, y))
                    {
                        board[x, y].type = RandomNormalType();
                        board[x, y].RefreshVisuals();
                        changed = true;
                    }
                }
        }
    }

    bool HasMatchAt(int x, int y)
    {
        var t = board[x, y].type;
        // Check horizontal triple
        if (x >= 2 && board[x-1,y] != null && board[x-2,y] != null &&
            board[x-1,y].type == t && board[x-2,y].type == t) return true;
        // Check vertical triple
        if (y >= 2 && board[x,y-1] != null && board[x,y-2] != null &&
            board[x,y-1].type == t && board[x,y-2].type == t) return true;
        return false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Input
    // ─────────────────────────────────────────────────────────────────────────
    public void SelectGem(Gem gem)
    {
        if (state != State.Idle) return;
        if (gem.obstacle == ObstacleType.Locked) return;

        if (selectedGem == null)
        {
            selectedGem = gem;
            gem.SetSelected(true);
        }
        else
        {
            gem.SetSelected(false);
            selectedGem.SetSelected(false);

            if (selectedGem == gem)
            {
                selectedGem = null;
                return;
            }

            if (IsAdjacent(selectedGem, gem))
                StartCoroutine(DoSwap(selectedGem, gem));
            else
            {
                selectedGem.SetSelected(false);
                selectedGem = gem;
                gem.SetSelected(true);
            }
        }
    }

    bool IsAdjacent(Gem a, Gem b) =>
        (Mathf.Abs(a.col - b.col) + Mathf.Abs(a.row - b.row)) == 1;

    // ─────────────────────────────────────────────────────────────────────────
    //  Swap coroutine
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator DoSwap(Gem a, Gem b)
    {
        state = State.Swapping;
        int savedACol = a.col, savedARow = a.row;

        // ── Animate ──────────────────────────────────────────────────────────
        var tweenA = a.MoveTo(CellToWorld(b.col, b.row), swapDuration);
        var tweenB = b.MoveTo(CellToWorld(a.col, a.row), swapDuration);
        yield return tweenA.WaitForCompletion();

        // ── Swap data ─────────────────────────────────────────────────────────
        SwapBoardData(a, b);

        // ── Check for special-gem activation ─────────────────────────────────
        bool specialActivated = false;

        // ColorBomb + anything → clear all of that type
        if (a.special == SpecialType.ColorBomb || b.special == SpecialType.ColorBomb)
        {
            specialActivated = true;
            var target = a.special == SpecialType.ColorBomb ? b : a;
            yield return StartCoroutine(ActivateColorBomb(a.special == SpecialType.ColorBomb ? a : b, target.type));
        }
        // Two specials hitting each other
        else if (a.special != SpecialType.None && b.special != SpecialType.None)
        {
            specialActivated = true;
            yield return StartCoroutine(ActivateDoubleSpecial(a, b));
        }

        if (!specialActivated)
        {
            var matchResult = detector.FindMatches(board, width, height, savedACol, savedARow);

            if (matchResult.matchedGems.Count == 0)
            {
                // Revert
                tweenA = a.MoveTo(CellToWorld(b.col, b.row), swapDuration);
                tweenB = b.MoveTo(CellToWorld(a.col, a.row), swapDuration);
                yield return tweenA.WaitForCompletion();
                SwapBoardData(a, b);
                state = State.Idle;
                yield break;
            }

            yield return StartCoroutine(ProcessMatchResult(matchResult));
        }

        goalManager?.CheckLevelComplete();
        state = State.Idle;
        selectedGem = null;
    }

    void SwapBoardData(Gem a, Gem b)
    {
        board[a.col, a.row] = b;
        board[b.col, b.row] = a;
        int tmpCol = a.col; a.col = b.col; b.col = tmpCol;
        int tmpRow = a.row; a.row = b.row; b.row = tmpRow;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Match processing pipeline
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator ProcessMatchResult(MatchDetector.MatchResult result, int cascadeDepth = 0)
    {
        state = State.Processing;

        // ── Damage adjacent obstacles ────────────────────────────────────────
        DamageAdjacentObstacles(result.matchedGems);

        // ── Collect gems to destroy ───────────────────────────────────────────
        var toDestroy = new List<Gem>(result.matchedGems);

        // Expand for specials inside the matched set
        foreach (var g in result.matchedGems)
            CollectSpecialExplosion(g, toDestroy);

        // ── Create special gem if warranted ───────────────────────────────────
        Gem newSpecial = null;
        if (result.specialToCreate != SpecialType.None)
        {
            int sc = result.specialCol, sr = result.specialRow;
            if (sc >= 0 && sr >= 0 && board[sc, sr] != null)
            {
                var pivot = board[sc, sr];
                toDestroy.Remove(pivot);        // keep it alive to become the special
                pivot.special = result.specialToCreate;
                pivot.RefreshVisuals();
                newSpecial = pivot;
            }
        }

        // ── Score + goals ─────────────────────────────────────────────────────
        scoreManager?.AddScore(toDestroy.Count * 10 * (cascadeDepth + 1));
        goalManager?.RegisterMatches(toDestroy);

        // ── Effects + destroy ─────────────────────────────────────────────────
        foreach (var g in toDestroy)
        {
            effects?.PlayDestroyEffect(g.transform.position, g.type);
            board[g.col, g.row] = null;
            g.PlayDestroyAnim();
        }

        yield return new WaitForSeconds(0.25f);

        // ── Gravity ───────────────────────────────────────────────────────────
        yield return StartCoroutine(ApplyGravity());

        // ── Refill ────────────────────────────────────────────────────────────
        yield return StartCoroutine(RefillBoard());

        yield return new WaitForSeconds(refillDelay);

        // ── Chocolate spreads once per cycle ──────────────────────────────────
        SpreadChocolate();

        // ── Cascade check ─────────────────────────────────────────────────────
        var next = detector.FindMatches(board, width, height);
        if (next.matchedGems.Count > 0)
            yield return StartCoroutine(ProcessMatchResult(next, cascadeDepth + 1));
        else
        {
            // ── No-move check ─────────────────────────────────────────────────
            if (!detector.HasAnyValidMove(board, width, height))
                yield return StartCoroutine(Reshuffle());
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Special gem activation
    // ─────────────────────────────────────────────────────────────────────────
    /// Collect all cells a special gem would destroy into toDestroy
    void CollectSpecialExplosion(Gem g, List<Gem> toDestroy)
    {
        switch (g.special)
        {
            case SpecialType.StripedH:
                for (int x = 0; x < width; x++)
                    if (board[x, g.row] != null && !toDestroy.Contains(board[x, g.row]))
                        toDestroy.Add(board[x, g.row]);
                break;

            case SpecialType.StripedV:
                for (int y = 0; y < height; y++)
                    if (board[g.col, y] != null && !toDestroy.Contains(board[g.col, y]))
                        toDestroy.Add(board[g.col, y]);
                break;

            case SpecialType.Bomb:
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int nx = g.col + dx, ny = g.row + dy;
                        if (InBounds(nx, ny) && board[nx, ny] != null && !toDestroy.Contains(board[nx, ny]))
                            toDestroy.Add(board[nx, ny]);
                    }
                break;

            case SpecialType.ColorBomb:
                // handled separately via ActivateColorBomb
                break;
        }
    }

    IEnumerator ActivateColorBomb(Gem colorBomb, GemType targetType)
    {
        var toDestroy = new List<Gem> { colorBomb };
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (board[x, y] != null && board[x, y].type == targetType)
                    toDestroy.Add(board[x, y]);

        scoreManager?.AddScore(toDestroy.Count * 20);
        goalManager?.RegisterMatches(toDestroy);

        foreach (var g in toDestroy)
        {
            effects?.PlayDestroyEffect(g.transform.position, g.type);
            board[g.col, g.row] = null;
            g.PlayDestroyAnim();
        }

        yield return new WaitForSeconds(0.3f);
        yield return StartCoroutine(ApplyGravity());
        yield return StartCoroutine(RefillBoard());

        var next = detector.FindMatches(board, width, height);
        if (next.matchedGems.Count > 0)
            yield return StartCoroutine(ProcessMatchResult(next, 1));
    }

    IEnumerator ActivateDoubleSpecial(Gem a, Gem b)
    {
        // Two stripes → cross (clear both row and column)
        // Stripe + Bomb → large cross
        // Two bombs → 5×5 blast
        // etc.  — simplest: combine both explosions
        var toDestroy = new List<Gem>();
        CollectSpecialExplosion(a, toDestroy);
        CollectSpecialExplosion(b, toDestroy);
        if (!toDestroy.Contains(a)) toDestroy.Add(a);
        if (!toDestroy.Contains(b)) toDestroy.Add(b);

        scoreManager?.AddScore(toDestroy.Count * 15);
        goalManager?.RegisterMatches(toDestroy);

        foreach (var g in toDestroy)
        {
            effects?.PlayDestroyEffect(g.transform.position, g.type);
            board[g.col, g.row] = null;
            g.PlayDestroyAnim();
        }

        yield return new WaitForSeconds(0.3f);
        yield return StartCoroutine(ApplyGravity());
        yield return StartCoroutine(RefillBoard());
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Gravity + refill (DOTween)
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator ApplyGravity()
    {
        var tweens = new List<Tweener>();

        for (int x = 0; x < width; x++)
        {
            int writeY = 0;
            for (int y = 0; y < height; y++)
            {
                if (board[x, y] != null)
                {
                    if (y != writeY)
                    {
                        board[x, writeY] = board[x, y];
                        board[x, y]      = null;
                        board[x, writeY].row = writeY;

                        float dist = y - writeY;
                        float dur  = fallDuration * Mathf.Sqrt(dist);
                        tweens.Add(board[x, writeY].MoveTo(CellToWorld(x, writeY), dur));
                    }
                    writeY++;
                }
            }
        }

        if (tweens.Count > 0)
            yield return tweens[tweens.Count - 1].WaitForCompletion();
    }

    IEnumerator RefillBoard()
    {
        var tweens = new List<Tweener>();

        for (int x = 0; x < width; x++)
        {
            int emptyCount = 0;
            for (int y = 0; y < height; y++)
                if (board[x, y] == null) emptyCount++;

            for (int y = 0; y < height; y++)
            {
                if (board[x, y] == null)
                {
                    // Spawn above the board and fall down
                    Vector2 spawnPos = CellToWorld(x, height + emptyCount);
                    var gem = SpawnGem(x, y, RandomNormalType());
                    gem.transform.position = spawnPos;
                    float dist = (height + emptyCount) - y;
                    tweens.Add(gem.MoveTo(CellToWorld(x, y), fallDuration * Mathf.Sqrt(dist)));
                    emptyCount--;
                }
            }
        }

        if (tweens.Count > 0)
            yield return tweens[tweens.Count - 1].WaitForCompletion();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Obstacle helpers
    // ─────────────────────────────────────────────────────────────────────────
    void DamageAdjacentObstacles(List<Gem> matchedGems)
    {
        var visited = new HashSet<Gem>();
        foreach (var g in matchedGems)
        {
            DamageNeighbour(g.col - 1, g.row, visited);
            DamageNeighbour(g.col + 1, g.row, visited);
            DamageNeighbour(g.col, g.row - 1, visited);
            DamageNeighbour(g.col, g.row + 1, visited);
        }
    }

    void DamageNeighbour(int x, int y, HashSet<Gem> done)
    {
        if (!InBounds(x, y) || board[x, y] == null) return;
        var n = board[x, y];
        if (n.obstacle == ObstacleType.None || done.Contains(n)) return;
        done.Add(n);
        bool cleared = n.DamageObstacle();
        if (cleared && n.obstacle == ObstacleType.None)
            effects?.PlayObstacleClearEffect(n.transform.position);
    }

    void SpreadChocolate()
    {
        var chocolateTiles = new List<Gem>();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (board[x, y] != null && board[x, y].obstacle == ObstacleType.Chocolate)
                    chocolateTiles.Add(board[x, y]);

        foreach (var c in chocolateTiles)
        {
            if (Random.value > chocolateSpreadChance) continue;
            int[] dx = { -1, 1, 0, 0 };
            int[] dy = {  0, 0,-1, 1 };
            for (int i = 0; i < 4; i++)
            {
                int nx = c.col + dx[i], ny = c.row + dy[i];
                if (!InBounds(nx, ny) || board[nx, ny] == null) continue;
                if (board[nx, ny].obstacle != ObstacleType.None) continue;
                board[nx, ny].obstacle   = ObstacleType.Chocolate;
                board[nx, ny].obstacleHp = 1;
                board[nx, ny].RefreshVisuals();
                break;  // spread to only one neighbour per tile per turn
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  No-move reshuffle
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator Reshuffle()
    {
        Debug.Log("[Match3] No valid moves — reshuffling board");

        // Collect all non-obstacle gems
        var gems = new List<Gem>();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (board[x, y] != null && board[x, y].obstacle == ObstacleType.None)
                    gems.Add(board[x, y]);

        // Fisher-Yates shuffle
        for (int i = gems.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Gem tmp = gems[i]; gems[i] = gems[j]; gems[j] = tmp;
        }

        // Reassign positions
        var positions = new List<(int col, int row)>();
        foreach (var g in gems) positions.Add((g.col, g.row));

        for (int i = 0; i < gems.Count; i++)
        {
            gems[i].col = positions[i].col;
            gems[i].row = positions[i].row;
            board[positions[i].col, positions[i].row] = gems[i];
        }

        // Animate bounce-in
        List<Sequence> tweens = new List<Sequence>();
        for (int i = 0; i < gems.Count; i++)
        {
            var g = gems[i];
            Sequence seq = DOTween.Sequence()
                .Append(g.transform.DOScale(Vector3.zero, 0.15f))
                .AppendCallback(() => g.transform.position = CellToWorld(g.col, g.row))
                .Append(g.transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutBack));
            tweens.Add(seq);
        }

        yield return new WaitForSeconds(0.5f);

        if (!detector.HasAnyValidMove(board, width, height))
            yield return StartCoroutine(Reshuffle());   // try again if still stuck
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Utility
    // ─────────────────────────────────────────────────────────────────────────
    public Vector2 CellToWorld(int x, int y) =>
        new Vector2(x * cellSize, y * cellSize);

    bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;
}