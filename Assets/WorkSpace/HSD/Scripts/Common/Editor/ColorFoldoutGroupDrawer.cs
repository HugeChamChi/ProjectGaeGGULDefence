using System;
using Alchemy.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>[ColorFoldoutGroup]으로 묶인 필드들을 attribute가 지정한 색상의 접이식 박스로 그린다.
/// 헤더 배경/좌측 테두리/제목 글자색에 색을 입혀 섹션을 한눈에 구분하기 위한 용도.</summary>
[CustomGroupDrawer(typeof(ColorFoldoutGroupAttribute))]
public sealed class ColorFoldoutGroupDrawer : AlchemyGroupDrawer
{
    Foldout foldout;
    VisualElement content;
    bool colorApplied;

    public override VisualElement CreateRootElement(string label)
    {
        var configKey = UniqueId + "_ColorFoldoutGroup";
        bool.TryParse(EditorUserSettings.GetConfigValue(configKey), out var expanded);

        var box = new VisualElement
        {
            style =
            {
                width = Length.Percent(100f),
                marginTop = 3f,
                marginBottom = 1f,
                borderTopLeftRadius = 4f,
                borderTopRightRadius = 4f,
                borderBottomLeftRadius = 4f,
                borderBottomRightRadius = 4f,
                overflow = Overflow.Hidden,
            }
        };

        foldout = new Foldout
        {
            text = label,
            value = expanded,
            style = { width = Length.Percent(100f) }
        };
        foldout.RegisterValueChangedCallback(evt =>
            EditorUserSettings.SetConfigValue(configKey, evt.newValue.ToString()));

        content = new VisualElement
        {
            style =
            {
                paddingLeft = 6f,
                paddingRight = 4f,
                paddingTop = 2f,
                paddingBottom = 4f,
            }
        };
        foldout.Add(content);
        box.Add(foldout);

        return box;
    }

    public override VisualElement GetGroupElement(Attribute attribute)
    {
        if (!colorApplied && attribute is ColorFoldoutGroupAttribute colorAttribute &&
            ColorUtility.TryParseHtmlString(colorAttribute.HexColor, out var color))
        {
            colorApplied = true;
            ApplyColor(color);
        }

        return content;
    }

    void ApplyColor(Color color)
    {
        var toggle = foldout.Q<Toggle>();
        var label = toggle?.Q<Label>();
        if (label == null) return;

        // 제목 중 짧은 이름 부분만 등급별값 에디터의 태그 칩처럼 색 배경 배지로 만들고,
        // 나머지 설명(괄호 부분)은 기본 톤 텍스트로 옆에 붙인다.
        string fullText = label.text;
        int splitIndex = fullText.IndexOf(" (", StringComparison.Ordinal);
        string badgeText = splitIndex >= 0 ? fullText.Substring(0, splitIndex) : fullText;
        string restText = splitIndex >= 0 ? fullText.Substring(splitIndex + 1) : "";

        var badge = new Label(badgeText)
        {
            style =
            {
                backgroundColor = color,
                color = Color.white,
                unityFontStyleAndWeight = FontStyle.Bold,
                fontSize = 11f,
                paddingLeft = 5f,
                paddingRight = 5f,
                paddingTop = 1f,
                paddingBottom = 1f,
                marginRight = 4f,
                borderTopLeftRadius = 3f,
                borderTopRightRadius = 3f,
                borderBottomLeftRadius = 3f,
                borderBottomRightRadius = 3f,
            }
        };

        label.text = restText;
        label.style.fontSize = 11f;
        label.style.opacity = 0.75f;

        var parent = label.parent;
        parent.Insert(parent.IndexOf(label), badge);
    }
}
