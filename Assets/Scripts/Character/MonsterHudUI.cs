using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum MonsterAIState
{
    Idle = 0,
    Patrol = 1,
    Move = 2,
    Attack = 3,
    Return = 4,
    Dead = 5,
    Chase = 6,
}

public static class MonsterAIStateText
{
    public static string ToDisplayString(MonsterAIState st)
    {
        switch (st)
        {
            case MonsterAIState.Idle: return "standing by";
            case MonsterAIState.Patrol: return "scanning";
            case MonsterAIState.Move: return "closing in!";
            case MonsterAIState.Attack: return "striking!!";
            case MonsterAIState.Return: return "falling back";
            case MonsterAIState.Dead: return "down";
            default: return "";
        }
    }

    public static Color ToColor(MonsterAIState st)
    {
        switch (st)
        {
            case MonsterAIState.Idle: return Color.blue;
            case MonsterAIState.Patrol: return Color.deepPink;
            case MonsterAIState.Move: return Color.blue;
            case MonsterAIState.Attack: return Color.red;
            case MonsterAIState.Return: return Color.red;
            case MonsterAIState.Dead: return Color.red;
            default: return Color.white;
        }
    }
}

public class MonsterHudUI : MonoBehaviour
{
    [Header("UI Refs")]
    public TextMeshProUGUI stateText;
    public Image hpFill;
    public Image spFill;

    int _maxHp = 1;
    int _maxSp = 1;

    public void ApplyStats(int hp, int maxHp, int sp, int maxSp)
    {
        _maxHp = Mathf.Max(1, maxHp);
        _maxSp = Mathf.Max(1, maxSp);

        if (hpFill != null)
        {
            float t = Mathf.Clamp01((float)hp / _maxHp);
            hpFill.fillAmount = t;
        }

        if (spFill != null)
        {
            float t = Mathf.Clamp01((float)sp / _maxSp);
            spFill.fillAmount = t;
        }
    }

    public void SetAIState(MonsterAIState state)
    {
        if (stateText == null) return;

        stateText.text = MonsterAIStateText.ToDisplayString(state);
        stateText.color = MonsterAIStateText.ToColor(state);
    }
}
