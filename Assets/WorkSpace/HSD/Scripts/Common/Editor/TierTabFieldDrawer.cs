using UnityEditor;
using UnityEngine;

/// <summary>[TierTabGroup] 안의 PerTierFloat/PerTierInt 필드를 TierTabState.Selected가 가리키는
/// 슬롯 하나만 보이도록 그린다. SerializeReference 뒤에 숨은 SkillData/BuffData용 PerTierFloat/PerTierInt는
/// SelectableReferenceDrawer가 직접 처리하므로(PerTierValueDrawer 4컬럼) 이 드로어를 타지 않는다 —
/// 여긴 UnitData처럼 일반 필드로 직접 선언된 경우에만 Alchemy의 타입 기반 드로어 조회로 호출된다.</summary>
[CustomPropertyDrawer(typeof(PerTierFloat))]
[CustomPropertyDrawer(typeof(PerTierInt))]
public class TierTabFieldDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUIUtility.singleLineHeight;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var slot = property.FindPropertyRelative(TierUtil.FieldName(TierTabState.Selected));
        EditorGUI.PropertyField(position, slot, label);
        EditorGUI.EndProperty();
    }
}
