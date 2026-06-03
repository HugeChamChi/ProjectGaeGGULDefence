using UnityEngine;

namespace HSD.InGameDebug
{
    public interface IDebugInfoPopup
    {
        DebugTabType TabType { get; }
        void OpenPopup();
        void ClosePopup();
    }
}
