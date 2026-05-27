using UnityEngine;

namespace GaeGGUL.Tutorial
{
    public class UI_TutorialTarget : MonoBehaviour
    {
        [SerializeField] private string _uiTargetID;
        public string UITargetID => _uiTargetID;

        [Header("Highlight Settings")]
        [SerializeField] private Vector2 _sizeOffset = new Vector2(10, 10);
        public Vector2 SizeOffset => _sizeOffset;

        [SerializeField] private float _softness = 10f;
        public float Softness => _softness;
    }
}
