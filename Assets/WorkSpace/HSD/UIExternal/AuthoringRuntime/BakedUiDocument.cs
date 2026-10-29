using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GGD.UI.Authoring
{
    /// <summary>Serialized identities and service bindings for a prefab baked from an authored screen. Runtime binding reads no XML.</summary>
    public sealed class BakedUiDocument : MonoBehaviour
    {
        [SerializeField] private string _contentHash;
        [SerializeField] private RectTransform _animationRoot;
        [SerializeField] private Element[] _elements = Array.Empty<Element>();
        [SerializeField] private Control[] _controls = Array.Empty<Control>();

        /// <summary>Publication hash used to reject stale baked assets before a game build.</summary>
        public string ContentHash => _contentHash;
        /// <summary>Serialized host animation target, remapped automatically on Instantiate.</summary>
        public RectTransform AnimationRoot => _animationRoot;
        /// <summary>Number of composed elements represented by the prefab.</summary>
        public int ElementCount => _elements.Length;

        [Serializable] private sealed class Element
        {
            public string Id, Visible, TextState;
            public RectTransform Rect;
            public bool Active;
            public TMP_Text Text;
        }
        [Serializable] private sealed class Control
        {
            public Component Target;
            public string State, Command, Enabled;
            public GameObject On, Off;
            public Slider Visual;
        }

        internal void Bind(IAuthoringBindings bindings, Dictionary<string, RectTransform> elements, List<Action> refresh, List<Action> cleanup)
        {
            foreach (var item in _elements)
            {
                if (item.Rect == null || string.IsNullOrEmpty(item.Id)) throw new FormatException("Baked UI has a missing element reference");
                elements.Add(item.Id, item.Rect);
                if (!string.IsNullOrEmpty(item.Visible)) refresh.Add(() => item.Rect.gameObject.SetActive(item.Active && Convert.ToBoolean(bindings.Read(item.Visible))));
                if (!string.IsNullOrEmpty(item.TextState)) refresh.Add(() => item.Text.text = Convert.ToString(bindings.Read(item.TextState), CultureInfo.InvariantCulture));
            }
            foreach (var item in _controls)
            {
                if (item.Target == null) throw new FormatException("Baked UI has a missing control reference");
                if (item.Target is Selectable selectable && !string.IsNullOrEmpty(item.Enabled))
                    refresh.Add(() => selectable.interactable = Convert.ToBoolean(bindings.Read(item.Enabled)));
                if (item.Target is Button button)
                {
                    if (!string.IsNullOrEmpty(item.Command)) button.onClick.AddListener(() => bindings.Execute(item.Command));
                    cleanup.Add(() => button.onClick.RemoveAllListeners());
                }
                else if (item.Target is Slider slider)
                {
                    if (!string.IsNullOrEmpty(item.State)) refresh.Add(() => slider.SetValueWithoutNotify(Convert.ToSingle(bindings.Read(item.State))));
                    if (!string.IsNullOrEmpty(item.Command)) slider.onValueChanged.AddListener(value => bindings.Execute(item.Command, value));
                    cleanup.Add(() => slider.onValueChanged.RemoveAllListeners());
                }
                else if (item.Target is Toggle toggle)
                {
                    void RefreshToggle()
                    {
                        bool value = string.IsNullOrEmpty(item.State) ? toggle.isOn : Convert.ToBoolean(bindings.Read(item.State));
                        toggle.SetIsOnWithoutNotify(value); SetVisual(item, value);
                    }
                    refresh.Add(RefreshToggle);
                    if (!string.IsNullOrEmpty(item.Command)) toggle.onValueChanged.AddListener(value => { bindings.Execute(item.Command, value); RefreshToggle(); });
                    cleanup.Add(() => toggle.onValueChanged.RemoveAllListeners());
                }
                else if (item.Target is AuthoredSwitch control)
                {
                    refresh.Add(() => SetVisual(item, Convert.ToBoolean(bindings.Read(item.State))));
                    control.OnPressed = () => bindings.Execute(item.Command, !Convert.ToBoolean(bindings.Read(item.State)));
                    cleanup.Add(() => control.OnPressed = null);
                }
                else throw new FormatException("Unsupported baked UI control " + item.Target.GetType().Name);
            }
        }

        private static void SetVisual(Control item, bool value)
        {
            if (item.On != null) item.On.SetActive(value);
            if (item.Off != null) item.Off.SetActive(!value);
            if (item.Visual != null) item.Visual.SetValueWithoutNotify(value ? 1 : 0);
        }

#if UNITY_EDITOR
        /// <summary>Captures already-configured components and typed service contracts during editor baking only.</summary>
        public void Capture(AuthoredUiView view, string contentHash)
        {
            _contentHash = contentHash; _animationRoot = view.AnimationRoot;
            _elements = view.Document.Root.Descendants("Element").Select(node =>
            {
                var rect = view.Elements[AuthoringWorkspace.Required(node, "id")];
                return new Element { Id = (string)node.Attribute("id"), Rect = rect, Active = AuthoredUiView.Boolean(node, "active", true),
                    Visible = (string)node.Attribute("visible"), Text = rect.GetComponent<TMP_Text>(), TextState = (string)node.Element("Text")?.Attribute("bind") };
            }).ToArray();
            var controls = new List<Control>();
            foreach (var node in view.Document.Root.Descendants("Element"))
                foreach (var item in node.Elements().Where(x => x.Name == "Button" || x.Name == "Slider" || x.Name == "Toggle" || x.Name == "Switch"))
                {
                    var rect = view.Elements[(string)node.Attribute("id")];
                    Component target = item.Name.LocalName switch { "Button" => rect.GetComponent<Button>(), "Slider" => rect.GetComponent<Slider>(), "Toggle" => rect.GetComponent<Toggle>(), _ => rect.GetComponent<AuthoredSwitch>() };
                    RectTransform Reference(string key) => string.IsNullOrEmpty((string)item.Attribute(key)) ? null : view.Elements[(string)item.Attribute(key)];
                    controls.Add(new Control { Target = target, State = (string)item.Attribute("bind"), Command = (string)item.Attribute("command"), Enabled = (string)item.Attribute("enabled"),
                        On = Reference("on")?.gameObject, Off = Reference("off")?.gameObject, Visual = Reference("visualSlider")?.GetComponent<Slider>() });
                }
            _controls = controls.ToArray();
            // Runtime listeners close over the temporary bake view and must never survive in a saved prefab.
            foreach (var control in _controls)
            {
                if (control.Target is Button button) button.onClick.RemoveAllListeners();
                else if (control.Target is Slider slider) slider.onValueChanged.RemoveAllListeners();
                else if (control.Target is Toggle toggle) toggle.onValueChanged.RemoveAllListeners();
                else if (control.Target is AuthoredSwitch input) input.OnPressed = null;
            }
        }
#endif
    }
}
