using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GGD.UI.Authoring
{
    /// <summary>Editable counterpart of the production sliding-toggle click contract, separate from its visual Slider.</summary>
    public sealed class AuthoredSwitch : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>Command callback owned by the rendered screen.</summary>
        public Action OnPressed;
        /// <summary>Whether the authored control accepts input.</summary>
        public bool Interactable = true;
        /// <summary>Dispatches once; presentation is updated silently from the binding service.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Interactable || eventData.button != PointerEventData.InputButton.Left) return;
            foreach (var group in GetComponentsInParent<CanvasGroup>()) if (!group.interactable) return;
            OnPressed?.Invoke();
        }
        private void OnDestroy() { OnPressed = null; }
    }
}
