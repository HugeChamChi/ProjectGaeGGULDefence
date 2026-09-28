using TMPro;
using UnityEngine;

/// <summary>데미지 표시 방식 하나 (모비노기형, 쿠키런형). DamageStyleLab이 만들고 매 프레임 Tick을 부른다.</summary>
public interface IDamageStyleView
{
    /// <summary>숫자를 띄울 기준 보스. null이면 화면 위쪽 가운데를 기준으로 한다.</summary>
    void SetAnchor(Transform boss);

    /// <summary>모든 숫자의 폰트와 머티리얼을 바꾼다. 떠 있는 숫자에도 바로 적용된다.</summary>
    void SetFont(TMP_FontAsset font, Material material);

    /// <summary>피해 한 건. 모비노기형은 즉시 개별 표시하고, 다른 방식은 각 표시 정책을 따른다.</summary>
    void Add(decimal amount, BossDamageKind kind);

    /// <summary>실제 시간 기준 갱신.</summary>
    void Tick(float now, float deltaTime);

    /// <summary>떠 있는 숫자와 쌓인 값을 모두 지운다.</summary>
    void Clear();
}
