using Alchemy.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;

/// <summary>
/// Alchemy 인스펙터는 [SerializeReference] 필드를 만나면 CustomPropertyDrawer 여부와 무관하게
/// 항상 자체 SerializeReferenceField(원문 타입명 드롭다운)로 그린다 (AlchemyPropertyField.cs 참고).
/// 그래서 SelectableReferenceDrawer(우리 DisplayName 드롭다운 + PerTier 색상 UI)가 SkillData/BuffData
/// 등 ScriptableObject 인스펙터(Alchemy fallback editor)에서는 전혀 호출되지 않았다.
/// 이 드로어가 Alchemy의 기본 요소를 걷어내고 Unity 표준 UI Toolkit PropertyField로 교체해,
/// 클래식 ScriptAttributeUtility 경로(=SelectableReferenceDrawer)가 실제로 타도록 강제한다.
/// </summary>
[CustomAttributeDrawer(typeof(SelectableReferenceAttribute))]
public class SelectableReferenceAlchemyDrawer : AlchemyAttributeDrawer
{
    public override void OnCreateElement()
    {
        if (SerializedProperty == null) return;

        TargetElement.Clear();
        TargetElement.Add(new PropertyField(SerializedProperty, ObjectNames.NicifyVariableName(SerializedProperty.displayName)));
    }
}
