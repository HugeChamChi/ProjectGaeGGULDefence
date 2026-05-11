using UnityEngine;

namespace GaeGGUL.UI.Totem
{
    [CreateAssetMenu(fileName = "TotemDisplaySettings", menuName = "UI/TotemDisplaySettings")]
    public class TotemDisplaySettings : ScriptableObject
    {
        [Header("Field")]
        public Color fieldColor = new Color(0.7f, 0.7f, 0.7f);
        public string fieldLabel = "필드";

        [Header("Totem")]
        public Color totemColor = new Color(0.5f, 1.0f, 0.5f);
        public string totemLabel = "토템 위치";

        [Header("Effect")]
        public Color effectColor = new Color(1.0f, 0.0f, 0.0f);
        public string effectLabel = "효과 범위";

        [Header("Debuff")]
        public Color debuffColor = new Color(0.1f, 0.1f, 0.1f);
        public string debuffLabel = "디버프 적용";
    }
}
