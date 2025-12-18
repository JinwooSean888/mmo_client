using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum MonsterAIState
{
    Idle = 0,
    Patrol = 1,
    Chase = 2,
    Attack = 3,
    Return = 4,
    Dead = 5,
}

public static class MonsterAIStateText
{
    public static string ToDisplayString(MonsterAIState st)
    {
        switch (st)
        {
            case MonsterAIState.Idle: return "대기 중";
            case MonsterAIState.Patrol: return "순찰 중";
            case MonsterAIState.Chase: return "추적 중!";
            case MonsterAIState.Attack: return "공격 중!!";
            case MonsterAIState.Return: return "복귀 중";
            case MonsterAIState.Dead: return "사망";
            default: return "";
        }
    }

    public static Color ToColor(MonsterAIState st)
    {
        switch (st)
        {
            case MonsterAIState.Idle: return Color.gray;
            case MonsterAIState.Patrol: return Color.cyan;
            case MonsterAIState.Chase: return Color.yellow;
            case MonsterAIState.Attack: return Color.red;
            case MonsterAIState.Return: return Color.green;
            case MonsterAIState.Dead: return Color.black;
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

    public void SetMaxStats(int maxHp, int maxSp)
    {
        _maxHp = Mathf.Max(1, maxHp);
        _maxSp = Mathf.Max(1, maxSp);
    }

    public void SetHpSp(int hp, int sp)
    {
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
