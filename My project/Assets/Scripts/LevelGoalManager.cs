using UnityEngine;
using UnityEngine.Events;
using TMPro;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
//  LevelGoalManager
//  Reads goal data from the active LevelData asset and tracks progress.
//  Fires UnityEvents on win and loss so UI / sound can react.
// ─────────────────────────────────────────────────────────────────────────────
public class LevelGoalManager : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Level Data")]
    public LevelData currentLevel;

    [Header("UI")]
    public TMP_Text movesText;
    public TMP_Text[] goalTexts;         // one per GoalEntry

    [Header("Events")]
    public UnityEvent onLevelComplete;
    public UnityEvent onLevelFailed;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private int   movesRemaining;
    private int[] goalProgress;          // cleared count per goal entry
    private bool  levelEnded;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        if (currentLevel == null) return;

        movesRemaining = currentLevel.movesAllowed;
        goalProgress   = new int[currentLevel.goals.Length];
        RefreshUI();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Called by BoardManager after each match resolution
    // ─────────────────────────────────────────────────────────────────────────
    public void RegisterMatches(List<Gem> clearedGems)
    {
        if (currentLevel == null || levelEnded) return;

        foreach (var gem in clearedGems)
        {
            for (int i = 0; i < currentLevel.goals.Length; i++)
            {
                var goal = currentLevel.goals[i];
                if (goal.type == GoalType.ClearGemColor && (GemType)goal.targetGemType == gem.type)
                    goalProgress[i]++;
                else if (goal.type == GoalType.ClearObstacle && gem.obstacle != ObstacleType.None)
                    goalProgress[i]++;
            }
        }

        RefreshUI();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Called by BoardManager at the end of each player turn
    // ─────────────────────────────────────────────────────────────────────────
    public void SpendMove()
    {
        if (levelEnded) return;
        movesRemaining = Mathf.Max(0, movesRemaining - 1);
        RefreshUI();
        CheckLevelComplete();
    }

    // ─────────────────────────────────────────────────────────────────────────
    public void CheckLevelComplete()
    {
        if (levelEnded || currentLevel == null) return;

        // ── Win? ─────────────────────────────────────────────────────────────
        bool allGoalsMet = true;
        for (int i = 0; i < currentLevel.goals.Length; i++)
            if (goalProgress[i] < currentLevel.goals[i].targetCount)
            { allGoalsMet = false; break; }

        if (allGoalsMet)
        {
            levelEnded = true;
            onLevelComplete.Invoke();
            return;
        }

        // ── Fail? ─────────────────────────────────────────────────────────────
        if (movesRemaining <= 0)
        {
            levelEnded = true;
            onLevelFailed.Invoke();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    void RefreshUI()
    {
        if (movesText != null)
            movesText.text = $"Moves: {movesRemaining}";

        if (goalTexts == null || currentLevel == null) return;
        for (int i = 0; i < goalTexts.Length && i < currentLevel.goals.Length; i++)
        {
            var goal = currentLevel.goals[i];
            int prog = goalProgress != null ? goalProgress[i] : 0;
            goalTexts[i].text = $"{goal.label}: {prog}/{goal.targetCount}";
            goalTexts[i].color = prog >= goal.targetCount ? Color.green : Color.white;
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Goal data structures
// ─────────────────────────────────────────────────────────────────────────────
public enum GoalType { ClearGemColor, ClearObstacle, ReachScore }

[System.Serializable]
public class GoalEntry
{
    public GoalType type          = GoalType.ClearGemColor;
    public int      targetGemType = 0;    // cast to GemType
    public int      targetCount   = 30;
    public string   label         = "Clear Red";
}