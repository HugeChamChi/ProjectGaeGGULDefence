using System;
using TMPro;
using UnityEngine;

namespace GGD.UI.Authoring.Studio
{
    /// <summary>Renders the actual selected TMP font to a small reusable preview, outside the authoring canvas.</summary>
    public sealed class UiLabFontPreview : IDisposable
    {
        private readonly GameObject _root;
        private readonly Camera _camera;
        private readonly TextMeshProUGUI _text;
        private TMP_FontAsset _font;
        /// <summary>Rendered font sample owned by this preview.</summary>
        public RenderTexture Texture { get; }
        /// <summary>Creates a camera-isolated sample with no game scene dependency.</summary>
        public UiLabFontPreview()
        {
            _root = new GameObject("Lab font specimen", typeof(RectTransform), typeof(Canvas)) { layer = 31 };
            _root.transform.position = new Vector3(10000, 0, 0); _root.GetComponent<RectTransform>().sizeDelta = new Vector2(512, 140);
            var canvas = _root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var cameraObject = new GameObject("Font specimen camera", typeof(Camera)); cameraObject.transform.SetParent(_root.transform, false); cameraObject.transform.localPosition = new Vector3(0, 0, -100);
            _camera = cameraObject.GetComponent<Camera>(); _camera.enabled = false; _camera.orthographic = true; _camera.orthographicSize = 70; _camera.cullingMask = 1 << 31; _camera.clearFlags = CameraClearFlags.SolidColor; _camera.backgroundColor = new Color(.055f, .071f, .098f);
            Texture = new RenderTexture(512, 140, 16) { name = "Selected font sample" }; _camera.targetTexture = Texture;
            var sample = new GameObject("Specimen", typeof(RectTransform), typeof(TextMeshProUGUI)) { layer = 31 }; sample.transform.SetParent(_root.transform, false);
            _text = sample.GetComponent<TextMeshProUGUI>(); _text.rectTransform.sizeDelta = new Vector2(480, 120); _text.alignment = TextAlignmentOptions.MidlineLeft; _text.fontSize = 34; _text.color = new Color(.88f, .92f, .97f); _text.raycastTarget = false;
            _text.text = "다람쥐 헌 쳇바퀴\nAa Bb 0123456789";
        }
        /// <summary>Updates the render only when the chosen font changes.</summary>
        public void Render(TMP_FontAsset font)
        {
            font ??= TMP_Settings.defaultFontAsset; if (_font == font && Texture.IsCreated()) return;
            _font = font; _text.font = font; _text.ForceMeshUpdate(); Canvas.ForceUpdateCanvases(); _camera.Render();
        }
        /// <summary>Releases the preview without destroying the caller-owned font.</summary>
        public void Dispose() { Texture.Release(); UnityEngine.Object.Destroy(Texture); UnityEngine.Object.Destroy(_root); }
    }
}
