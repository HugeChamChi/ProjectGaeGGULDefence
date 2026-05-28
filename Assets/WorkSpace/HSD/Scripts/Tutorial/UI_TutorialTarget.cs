using UnityEngine;

namespace GaeGGUL.Tutorial
{
    public class UI_TutorialTarget : MonoBehaviour
    {
        [SerializeField] private string _uiTargetID;
        public string UITargetID => _uiTargetID;
    }
}
