using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace GGD.UI.Authoring
{
    /// <summary>Non-blocking authoring diagnostics shared by portable publication and the game bridge.</summary>
    public static class UiPublishLint
    {
        /// <summary>Explicit input flags win; otherwise only images on input controls receive touches.</summary>
        public static bool ImageRaycastTarget(XElement image) => image.Attribute("raycast") != null
            ? bool.Parse((string)image.Attribute("raycast")) : IsControl(image.Parent);

        /// <summary>Whether an element owns a supported input component.</summary>
        public static bool IsControl(XElement element) => element != null && element.Elements().Any(x =>
            x.Name == "Button" || x.Name == "Toggle" || x.Name == "Slider" || x.Name == "Switch");

        /// <summary>Inspects composed screens, including nested prefabs, without changing source or blocking publication.</summary>
        public static IReadOnlyList<string> Inspect(AuthoringWorkspace workspace)
        {
            var messages = new List<string>();
            var images = workspace.Documents["images.xml"].Root.Elements("Image").ToDictionary(x => AuthoringWorkspace.Required(x, "key"));
            foreach (var pair in workspace.Documents.Where(x => x.Value.Root.Name == "Screen"))
            {
                var root = UiComposer.Resolve(workspace, pair.Key).Root;
                string screen = pair.Key;
                var graphics = root.Descendants("Image").ToArray();
                var blockers = graphics.Where(x => ImageRaycastTarget(x) && !IsControl(x.Parent)).ToArray();
                if (blockers.Length > 0) messages.Add($"{screen} [RAYCAST] 입력 부품이 없는 터치 그림 {blockers.Length}개. 클릭 차단용인지 확인하세요: " + string.Join(", ", blockers.Select(x => (string)x.Parent.Attribute("id"))));
                var used = graphics.Select(x => (string)x.Attribute("sprite")).Where(x => !string.IsNullOrEmpty(x)).Distinct().Select(x => images[x]).ToArray();
                messages.Add($"{screen} [TEXTURES] 원본 텍스처 {used.Select(x => (string)x.Attribute("file")).Distinct().Count()}개. 실제 패킹 텍스처 수는 게임 설치 시 검사합니다.");
                int layouts = root.Descendants().Count(x => x.Name == "Layout" || x.Name == "SizeFitter");
                if (layouts > 0) messages.Add($"{screen} [LAYOUT] 실행 중 레이아웃 계산 부품 {layouts}개.");
                int outlines = root.Descendants("Text").Where(x => AuthoringWorkspace.Number(x, "outlineWidth") > 0)
                    .Select(x => ((string)x.Attribute("font"), AuthoringWorkspace.Number(x, "outlineWidth"), ((string)x.Attribute("outlineColor") ?? "#000000FF").ToUpperInvariant().PadRight(9, 'F'))).Distinct().Count();
                messages.Add($"{screen} [OUTLINES] 폰트와 외곽선 조합 {outlines}종.");
                var groups = used.Select(x => (string)x.Attribute("atlas") ?? "").Distinct().ToArray();
                if (used.Length > 0)
                    messages.Add($"{screen} [ATLAS] " + (groups.Contains("") ? "아틀라스가 지정되지 않은 그림이 있습니다." : groups.Length == 1 ? "모든 그림이 같은 아틀라스로 지정되어 있습니다: " + groups[0] : "그림이 여러 아틀라스로 나뉩니다: " + string.Join(", ", groups)) + " 실제 포함 여부는 Unity 설치 시 검사합니다.");
            }
            return messages;
        }
    }
}
