using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace GGD.UI.Authoring
{
    /// <summary>Limits the first native prefab round trip to visual property edits, preserving native structure and behaviour.</summary>
    public static class NativePrefabContract
    {
        /// <summary>Portable source identity and baseline document.</summary>
        public const string Path = "NativePrefab.xml";
        /// <summary>Returns the optional native source contract.</summary>
        public static XElement Read(AuthoringWorkspace workspace) => workspace.Documents.TryGetValue(Path, out var value) ? value.Root : null;
        /// <summary>Validates every change before preview, publication or application to the Unity original.</summary>
        public static void Validate(AuthoringWorkspace workspace, ResolvedUiDocument resolved)
        {
            var contract = Read(workspace); if (contract == null) return;
            var baseline = contract.Element("Baseline")?.Element("Resolved") ?? throw new FormatException("원본 프리팹 기준 문서가 없습니다.");
            Compare(baseline, resolved.Root, true);
        }
        private static void Compare(XElement before, XElement after, bool root = false)
        {
            if (before.Name != after.Name) throw new FormatException("원본 프리팹 모드에서는 구조 변경을 지원하지 않습니다.");
            string allowed = before.Name.LocalName switch
            {
                "Element" => " name x y width height anchorMinX anchorMinY anchorMaxX anchorMaxY pivotX pivotY scaleX scaleY rotation active ",
                "Text" => " text size color alignment style autoSize minSize maxSize wrap overflow rich raycast marginLeft marginTop marginRight marginBottom ",
                "Image" => " sprite color type preserveAspect raycast fillMethod fillAmount fillOrigin clockwise ppuMultiplier ",
                _ => " "
            };
            foreach (string key in before.Attributes().Select(a => a.Name.LocalName).Union(after.Attributes().Select(a => a.Name.LocalName)))
            {
                if (before.Attribute(key) != null && after.Attribute(key) == null) throw new FormatException("원본 프리팹 속성 삭제는 지원하지 않습니다: " + key);
                if ((string)before.Attribute(key) != (string)after.Attribute(key) && !allowed.Contains(" " + key + " "))
                    throw new FormatException("원본 프리팹에서 지원하지 않는 변경: " + before.Name + "." + key);
            }
            var a = before.Elements().ToArray(); var b = after.Elements().ToArray();
            if (a.Length != b.Length) throw new FormatException("원본 프리팹 모드에서는 요소/컴포넌트 추가·삭제를 지원하지 않습니다.");
            for (int i = 0; i < a.Length; i++) Compare(a[i], b[i]);
        }
    }
}
