using UnityEngine;
using TMPro;
using DG.Tweening;

// ─────────────────────────────────────────────────────────────────────────────
//  ScoreManager
//  Tracks score, combo multipliers, and drives score-related UI.
// ─────────────────────────────────────────────────────────────────────────────
public class ScoreManager : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("UI")]
    public TMP_Text scoreText;
    public TMP_Text comboText;          // shows "x2 COMBO!" etc.

    [Header("Score")]
    public int basePointsPerGem  = 10;
    public int comboMultiplier   = 0;   // increments each cascade; reset on player turn

    // ── Runtime ───────────────────────────────────────────────────────────────
    private int   score;
    private int   combo;

    // ─────────────────────────────────────────────────────────────────────────
    public void AddScore(int rawAmount)
    {
        combo++;
        int earned = rawAmount * combo;
        score += earned;

        UpdateScoreText(score);
        ShowComboPopup(combo);

        // Feed back to LevelGoalManager for score goals
        var goalMgr = FindObjectOfType<LevelGoalManager>();
        if (goalMgr != null && goalMgr.currentLevel != null)
        {
            foreach (var g in goalMgr.currentLevel.goals)
                if (g.type == GoalType.ReachScore && score >= g.targetCount)
                    goalMgr.CheckLevelComplete();
        }
    }

    public void ResetCombo() => combo = 0;

    public int Score  => score;
    public int Combo  => combo;

    // ─────────────────────────────────────────────────────────────────────────
    void UpdateScoreText(int newScore)
    {
        if (scoreText == null) return;
        scoreText.text = $"Score\n{newScore:N0}";

        scoreText.transform
            .DOPunchScale(Vector3.one * 0.15f, 0.2f, 5, 0.5f)
            .SetEase(Ease.OutElastic);
    }

    void ShowComboPopup(int c)
    {
        if (comboText == null || c < 2) return;

        comboText.text  = $"x{c} COMBO!";
        comboText.alpha = 1f;

        comboText.transform.localScale = Vector3.one;
        DOTween.Sequence()
            .Append(comboText.transform.DOScale(1.3f, 0.15f))
            .Append(comboText.transform.DOScale(1f, 0.1f))
            .Append(comboText.DOFade(0f, 0.6f).SetDelay(0.4f));
    }
}