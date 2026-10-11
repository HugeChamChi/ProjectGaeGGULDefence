using UnityEngine;
using UnityEngine.UI;

/// <summary>완료 상태에 사용하는 버튼 없는 MAX와 날개 장식.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ResearchMaxGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = rectTransform.rect;
        Vector2 Point(float x, float y) => new Vector2(rect.xMin + x / 320f * rect.width, rect.yMin + y / 80f * rect.height);
        void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float opacity = 1f)
        {
            int start = mesh.currentVertCount;
            var tint = color; tint.a *= opacity;
            foreach (var point in new[] { a, b, c, d }) mesh.AddVert(point, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
        void Stroke(float ax, float ay, float bx, float by)
        {
            var a = Point(ax + ay * 0.12f, ay);
            var b = Point(bx + by * 0.12f, by);
            var normal = new Vector2(-(b - a).y, (b - a).x).normalized * rect.height * 0.045f;
            Quad(a + normal, b + normal, b - normal, a - normal);
        }
        // 압축된 기울임 글자. 텍스트 컴포넌트나 버튼을 추가하지 않는다.
        Stroke(110, 25, 110, 55); Stroke(110, 55, 125, 36);
        Stroke(125, 36, 139, 55); Stroke(139, 55, 139, 25);
        Stroke(152, 25, 165, 55); Stroke(165, 55, 179, 25); Stroke(157, 36, 173, 36);
        Stroke(190, 55, 215, 25); Stroke(191, 25, 214, 55);
        for (int side = 0; side < 2; side++)
        {
            Vector2 Wing(float x, float y) => Point(side == 0 ? x * 0.8f : 320f - x * 0.8f, y);
            Quad(Wing(22, 56), Wing(94, 40), Wing(110, 28), Wing(48, 42), 0.65f);
            Quad(Wing(44, 43), Wing(96, 36), Wing(106, 28), Wing(62, 33), 0.5f);
            Quad(Wing(65, 35), Wing(98, 31), Wing(105, 25), Wing(78, 27), 0.4f);
            Quad(Wing(82, 28), Wing(102, 27), Wing(105, 22), Wing(91, 22), 0.3f);
        }
    }
}
