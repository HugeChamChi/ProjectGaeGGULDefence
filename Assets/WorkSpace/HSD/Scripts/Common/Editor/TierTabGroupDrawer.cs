using System;
using Alchemy.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>[TierTabGroup]으로 묶인 필드들 위에 노말/레어/에픽/전설 탭 툴바를 그린다.
/// 실제 값 표시는 TierTabFieldDrawer(PerTierFloat/PerTierInt 전용)가 TierTabState.Selected를 읽어서 처리한다.</summary>
[CustomGroupDrawer(typeof(TierTabGroupAttribute))]
public sealed class TierTabGroupDrawer : AlchemyGroupDrawer
{
    private static readonly string[] Labels = { "노말", "레어", "에픽", "전설" };

    public override VisualElement CreateRootElement(string label)
    {
        var root = new VisualElement();

        var toolbar = new IMGUIContainer(() =>
        {
            var rect = EditorGUILayout.GetControlRect();
            int currentIndex = Array.IndexOf(TierUtil.All, TierTabState.Selected);
            int newIndex = GUI.Toolbar(rect, currentIndex, Labels);
            TierTabState.Selected = TierUtil.All[newIndex];
        });
        root.Add(toolbar);

        return root;
    }
}
