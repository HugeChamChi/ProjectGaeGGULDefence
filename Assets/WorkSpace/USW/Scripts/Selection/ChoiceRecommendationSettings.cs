using System;
using UnityEngine;

/// <summary>
/// 선택지 추천 배지의 저작 데이터. 테마 카드를 아직 고르지 않은 런에서 배지를 붙일 입문 추천 카드 ID를 보관한다.
/// 판정 규칙은 <see cref="ChoiceRecommendationRule"/>에 있으며 추첨·효과에는 관여하지 않는다.
/// </summary>
[CreateAssetMenu(fileName = "ChoiceRecommendationSettings", menuName = "Game/ChoiceRecommendationSettings")]
public sealed class ChoiceRecommendationSettings : ScriptableObject
{
    /// <summary>테마 카드 0장일 때 추천 배지를 붙일 고정 선택지 ID. 순서는 의미가 없다.</summary>
    public int[] StarterChooseIds = Array.Empty<int>();

    /// <summary>입문 추천 목록에 있는 ID인지 확인한다.</summary>
    public bool IsStarter(int chooseId) => Array.IndexOf(StarterChooseIds, chooseId) >= 0;
}
