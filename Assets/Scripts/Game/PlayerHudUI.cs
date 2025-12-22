using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHudUI : MonoBehaviour
{
    [Header("UI Refs")]
    public TextMeshProUGUI stateText;
    public Image hpFill;
    public Image spFill;

    int _maxHp = 50;
    int _maxSp = 50;

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

}