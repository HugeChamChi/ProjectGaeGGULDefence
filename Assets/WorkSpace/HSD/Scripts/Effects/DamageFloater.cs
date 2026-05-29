using UnityEngine;

/// <summary>
/// 데미지 수치 표시를 담당하는 플로터.
/// </summary>
public class DamageFloater : FloaterBase
{
    public void SetupAndPlay(string text, DamageFloaterStyle style = null, bool isCritical = false)
    {
        if (style == null)
        {
            style = ScriptableObject.CreateInstance<DamageFloaterStyle>();
            if (isCritical)
            {
                style.textColor = Color.red;
                style.fontSize = 60f;
                style.isBold = true;
            }
        }

        SetupBase(text, style);
        PlayAnimation(style);
    }
}
