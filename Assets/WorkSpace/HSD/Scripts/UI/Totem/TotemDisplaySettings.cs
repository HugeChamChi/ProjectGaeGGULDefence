using UnityEngine;

namespace GaeGGUL.UI.Totem
{
    [CreateAssetMenu(fileName = "TotemDisplaySettings", menuName = "UI/TotemDisplaySettings")]
    public class TotemDisplaySettings : ScriptableObject
    {
        [Header("Field")]
        public Sprite fieldSprite;
        public string fieldLabel = "필드";

        [Header("Totem")]
        public Sprite totemSprite;
        public string totemLabel = "토템 위치";

        [Header("Effect")]
        public Sprite effectSprite;
        public string effectLabel = "효과 범위";

        [Header("Debuff")]
        public Sprite debuffSprite;
        public string debuffLabel = "디버프 적용";
    }
}
