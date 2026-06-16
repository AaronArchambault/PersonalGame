using UnityEngine;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
//  MatchDetector
//  Scans the board for 3-in-a-row (and longer) in both axes.
//  Returns a MatchResult that carries which gems matched, and what special
//  gem (if any) should be created at the swap origin.
// ─────────────────────────────────────────────────────────────────────────────
public class MatchDetector : MonoBehaviour
{
    // ── Public result type ────────────────────────────────────────────────────
    public class MatchResult
    {
        public List<Gem>   matchedGems  = new();
        public SpecialType specialToCreate = SpecialType.None;
        public int         specialCol   = -1;
        public int         specialRow   = -1;
    }

    // ── Main entry point ──────────────────────────────────────────────────────
    /// <param name="swapCol">Column of the gem the player actively moved (for special placement)</param>
    /// <param name="swapRow">Row of the gem the player actively moved</param>
    public MatchResult FindMatches(Gem[,] board, int w, int h, int swapCol = -1, int swapRow = -1)
    {
        var result  = new MatchResult();
        var matched = new HashSet<Gem>();

        // ── Horizontal runs ───────────────────────────────────────────────────
        for (int y = 0; y < h; y++)
        {
            int runStart = 0;
            for (int x = 1; x <= w; x++)
            {
                bool endOfRun = x == w
                    || board[x, y] == null
                    || board[runStart, y] == null
                    || board[x, y].type != board[runStart, y].type
                    || board[x, y].obstacle == ObstacleType.Ice
                    || board[runStart, y].obstacle == ObstacleType.Ice;

                if (endOfRun)
                {
                    int len = x - runStart;
                    if (len >= 3)
                        ProcessRun(board, matched, ref result,
                                   runStart, y, len, true, w, h, swapCol, swapRow);
                    runStart = x;
                }
            }
        }

        // ── Vertical runs ─────────────────────────────────────────────────────
        for (int x = 0; x < w; x++)
        {
            int runStart = 0;
            for (int y = 1; y <= h; y++)
            {
                bool endOfRun = y == h
                    || board[x, y] == null
                    || board[x, runStart] == null
                    || board[x, y].type != board[x, runStart].type
                    || board[x, y].obstacle == ObstacleType.Ice
                    || board[x, runStart].obstacle == ObstacleType.Ice;

                if (endOfRun)
                {
                    int len = y - runStart;
                    if (len >= 3)
                        ProcessRun(board, matched, ref result,
                                   x, runStart, len, false, w, h, swapCol, swapRow);
                    runStart = y;
                }
            }
        }

        result.matchedGems = new List<Gem>(matched);
        return result;
    }

    // ── Process a single run of length >= 3 ──────────────────────────────────
    private void ProcessRun(Gem[,] board, HashSet<Gem> matched, ref MatchResult result,
                            int startX, int startY, int len, bool horizontal,
                            int w, int h, int swapCol, int swapRow)
    {
        // Add all gems in the run to the matched set
        for (int i = 0; i < len; i++)
        {
            var g = horizontal ? board[startX + i, startY]
                               : board[startX, startY + i];
            if (g != null) matched.Add(g);
        }

        // ── Determine special gem to create ───────────────────────────────────
        // Priority: longer run wins; ColorBomb > Bomb > StripedH/V
        SpecialType newSpecial = SpecialType.None;
        if      (len >= 5) newSpecial = SpecialType.ColorBomb;
        else if (len == 4) newSpecial = horizontal ? SpecialType.StripedH : SpecialType.StripedV;

        // Upgrade only — don't downgrade an already-set ColorBomb
        bool upgrade = UpgradePriority(newSpecial) > UpgradePriority(result.specialToCreate);
        if (newSpecial != SpecialType.None && upgrade)
        {
            result.specialToCreate = newSpecial;

            // Place the special gem where the player's swap gem was, if it's in this run
            int bestCol = startX;
            int bestRow = startY;

            if (swapCol >= 0)
            {
                if (horizontal && startY == swapRow && swapCol >= startX && swapCol < startX + len)
                    bestCol = swapCol;
                else if (!horizontal && startX == swapCol && swapRow >= startY && swapRow < startY + len)
                    bestRow = swapRow;
            }

            result.specialCol = horizontal ? bestCol : startX;
            result.specialRow = horizontal ? startY  : bestRow;
        }

        // ── L / T shape detection → Bomb ─────────────────────────────────────
        // If this is a horizontal run of 3, check if any gem in it is also part
        // of a vertical run of 3 already in `matched`. That means an L or T.
        if (len == 3 && horizontal)
            CheckLorT(board, matched, ref result, startX, startY, len, true,  w, h, swapCol, swapRow);
        else if (len == 3 && !horizontal)
            CheckLorT(board, matched, ref result, startX, startY, len, false, w, h, swapCol, swapRow);
    }

    // ── L/T shape → Bomb ─────────────────────────────────────────────────────
    private void CheckLorT(Gem[,] board, HashSet<Gem> matched, ref MatchResult result,
                           int startX, int startY, int len, bool horizontal,
                           int w, int h, int swapCol, int swapRow)
    {
        for (int i = 0; i < len; i++)
        {
            int cx = horizontal ? startX + i : startX;
            int cy = horizontal ? startY      : startY + i;

            // Check perpendicular run of 3 from this gem
            int perp = CountRun(board, cx, cy, !horizontal, w, h);
            if (perp >= 3)
            {
                bool upgrade = UpgradePriority(SpecialType.Bomb) > UpgradePriority(result.specialToCreate);
                if (upgrade)
                {
                    result.specialToCreate = SpecialType.Bomb;
                    result.specialCol = cx;
                    result.specialRow = cy;
                }
                break;
            }
        }
    }

    private int CountRun(Gem[,] board, int cx, int cy, bool horizontal, int w, int h)
    {
        if (board[cx, cy] == null) return 0;
        GemType t = board[cx, cy].type;
        int count = 1;
        if (horizontal) {
            for (int x = cx - 1; x >= 0 && board[x, cy] != null && board[x, cy].type == t; x--) count++;
            for (int x = cx + 1; x <  w && board[x, cy] != null && board[x, cy].type == t; x++) count++;
        } else {
            for (int y = cy - 1; y >= 0 && board[cx, y] != null && board[cx, y].type == t; y--) count++;
            for (int y = cy + 1; y <  h && board[cx, y] != null && board[cx, y].type == t; y++) count++;
        }
        return count;
    }

    private int UpgradePriority(SpecialType s) => s switch
    {
        SpecialType.None       => 0,
        SpecialType.StripedH   => 1,
        SpecialType.StripedV   => 1,
        SpecialType.Bomb       => 2,
        SpecialType.ColorBomb  => 3,
        _                      => 0
    };

    // ─────────────────────────────────────────────────────────────────────────
    //  No-move check  — used by BoardManager to detect deadlock
    // ─────────────────────────────────────────────────────────────────────────
    public bool HasAnyValidMove(Gem[,] board, int w, int h)
    {
        // Try every horizontal and vertical swap; see if it would create a match
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w - 1; x++)
                if (SwapWouldMatch(board, w, h, x, y, x + 1, y)) return true;

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h - 1; y++)
                if (SwapWouldMatch(board, w, h, x, y, x, y + 1)) return true;

        return false;
    }

    private bool SwapWouldMatch(Gem[,] board, int w, int h,
                                int ax, int ay, int bx, int by)
    {
        if (board[ax, ay] == null || board[bx, by] == null) return false;
        // Temporarily swap
        (board[ax, ay], board[bx, by]) = (board[bx, by], board[ax, ay]);
        var result = FindMatches(board, w, h);
        bool hasMatch = result.matchedGems.Count > 0;
        // Swap back
        (board[ax, ay], board[bx, by]) = (board[bx, by], board[ax, ay]);
        return hasMatch;
    }
}