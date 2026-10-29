using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GGD.UI.Authoring
{
    /// <summary>Applies the supported visual property deltas without replacing components or their event references.</summary>
    public static class NativeVisualProperty
    {
        /// <summary>Applies one validated text or image property to an existing native component.</summary>
        public static void Apply(RectTransform rect, string type, string key, string value, Func<string, Sprite> sprite)
        {
            float N() => float.Parse(value, CultureInfo.InvariantCulture);
            bool B() => bool.Parse(value);
            Color C() => ColorUtility.TryParseHtmlString(value, out var color) ? color : throw new FormatException("Invalid color");
            if (type == "Image")
            {
                var image = rect.GetComponent<Image>() ?? throw new FormatException("Native Image missing");
                switch (key)
                {
                    case "sprite": image.sprite = string.IsNullOrEmpty(value) ? null : sprite(value); break;
                    case "color": image.color = C(); break; case "type": image.type = (Image.Type)(int)N(); break;
                    case "preserveAspect": image.preserveAspect = B(); break; case "raycast": image.raycastTarget = B(); break;
                    case "fillMethod": image.fillMethod = (Image.FillMethod)(int)N(); break; case "fillAmount": image.fillAmount = N(); break;
                    case "fillOrigin": image.fillOrigin = (int)N(); break; case "clockwise": image.fillClockwise = B(); break;
                    case "ppuMultiplier": image.pixelsPerUnitMultiplier = N(); break;
                    default: throw new FormatException("Unsupported native Image property: " + key);
                }
                return;
            }
            var text = rect.GetComponent<TMP_Text>() ?? throw new FormatException("Native Text missing");
            switch (key)
            {
                case "text": text.text = value; break; case "size": text.fontSize = N(); break; case "color": text.color = C(); break;
                case "alignment": text.alignment = (TextAlignmentOptions)(int)N(); break; case "style": text.fontStyle = (FontStyles)(int)N(); break;
                case "autoSize": text.enableAutoSizing = B(); break; case "minSize": text.fontSizeMin = N(); break; case "maxSize": text.fontSizeMax = N(); break;
                case "wrap": text.textWrappingMode = (TextWrappingModes)(int)N(); break; case "overflow": text.overflowMode = (TextOverflowModes)(int)N(); break;
                case "rich": text.richText = B(); break; case "raycast": text.raycastTarget = B(); break;
                case "marginLeft": text.margin = new Vector4(N(), text.margin.y, text.margin.z, text.margin.w); break;
                case "marginTop": text.margin = new Vector4(text.margin.x, N(), text.margin.z, text.margin.w); break;
                case "marginRight": text.margin = new Vector4(text.margin.x, text.margin.y, N(), text.margin.w); break;
                case "marginBottom": text.margin = new Vector4(text.margin.x, text.margin.y, text.margin.z, N()); break;
                default: throw new FormatException("Unsupported native Text property: " + key);
            }
        }
    }
}
