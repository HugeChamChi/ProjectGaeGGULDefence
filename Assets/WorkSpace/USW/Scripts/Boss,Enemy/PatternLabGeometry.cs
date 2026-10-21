using System.Collections.Generic;
using UnityEngine;

/// <summary>선·사각형·화살표·마름모만으로 기믹을 표시하는 공용 동적 메시. 원형 표시는 사용하지 않는다.</summary>
public sealed class PatternLabGeometry : MonoBehaviour
{
    private const float BorderWidth = .035f;
    private readonly List<Vector3> _vertices = new();
    private readonly List<Color> _colors = new();
    private readonly List<int> _indices = new();
    private Mesh _mesh;
    /// <summary>이 패턴 전용 메시와 렌더러를 초기화한다.</summary>
    public void Configure(Material material)
    {
        if (_mesh != null) return;
        _mesh = new Mesh { name = "Non-circular pattern geometry" };
        _mesh.MarkDynamic();
        gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
        var renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = 18;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }
    /// <summary>이번 프레임에 그릴 도형 목록을 비운다.</summary>
    public void Begin() { _vertices.Clear(); _colors.Clear(); _indices.Clear(); }
    /// <summary>도형 목록을 하나의 렌더 메시로 제출한다.</summary>
    public void Flush()
    {
        if (_mesh == null) return;
        _mesh.Clear();_mesh.SetVertices(_vertices);_mesh.SetColors(_colors);_mesh.SetTriangles(_indices,0);_mesh.RecalculateBounds();
    }
    /// <summary>월드 좌표의 채운 사각형.</summary>
    public void Box(Vector3 center,Vector2 size,Color color)
    {
        Vector3 half = size*.5f;
        Quad(center+new Vector3(-half.x,-half.y),center+new Vector3(half.x,-half.y),center+new Vector3(half.x,half.y),center+new Vector3(-half.x,half.y),color);
    }
    /// <summary>네 변으로 된 안전/위험 경계.</summary>
    public void Frame(Vector3 center,Vector2 size,Color color,float width=BorderWidth)
    {
        Vector3 a=center+new Vector3(-size.x,-size.y)*.5f,b=center+new Vector3(size.x,-size.y)*.5f;
        Vector3 c=center+new Vector3(size.x,size.y)*.5f,d=center+new Vector3(-size.x,size.y)*.5f;
        Line(a,b,width,color);Line(b,c,width,color);Line(c,d,width,color);Line(d,a,width,color);
    }
    /// <summary>두 점을 연결하는 굵은 선.</summary>
    public void Line(Vector3 start,Vector3 end,float width,Color color)
    {
        Vector3 delta=end-start;
        if(delta.sqrMagnitude<.000001f)return;
        Vector3 side=new Vector3(-delta.y,delta.x).normalized*width*.5f;
        Quad(start+side,end+side,end-side,start-side,color);
    }
    /// <summary>유도 방향을 표시하는 화살표.</summary>
    public void Arrow(Vector3 start,Vector3 end,float width,Color color)
    {
        var axis=(end-start).normalized;
        var side=new Vector3(-axis.y,axis.x);
        Line(start,end-axis*width*2f,width*.35f,color);
        Triangle(end,end-axis*width*2.8f+side*width,end-axis*width*2.8f-side*width,color);
    }
    /// <summary>유닛/오브젝트에 붙는 마름모 표식.</summary>
    public void Diamond(Vector3 center,float size,Color color)
    {
        Quad(center+Vector3.up*size,center+Vector3.right*size,center+Vector3.down*size,center+Vector3.left*size,color);
    }
    /// <summary>셀의 사각 외곽과 대각선 경고.</summary>
    public void Tile(GridCell cell,Color color,bool hatch=false)
    {
        if(cell==null)return;
        Bounds bounds=BoundsOf(cell);Vector2 size=new Vector2(bounds.size.x,bounds.size.y)*.87f;
        Box(bounds.center,size,new Color(color.r,color.g,color.b,color.a*.15f));Frame(bounds.center,size,color);
        if(hatch)
        {
            Vector3 half=new Vector3(size.x,size.y)*.5f;
            Line(bounds.center-half,bounds.center+half,.025f,new Color(color.r,color.g,color.b,color.a*.6f));
        }
    }
    /// <summary>마스킹용 실제 타일 경계. 렌더러가 없을 때는 배치 중심의 단위 칸.</summary>
    public static Bounds BoundsOf(GridCell cell)=>cell.TryGetVisualBounds(out var b)?b:new Bounds(cell.transform.position,Vector3.one);
    private void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color) { Triangle(a,b,c,color);Triangle(a,c,d,color); }
    private void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color)
    {
        int i=_vertices.Count;
        _vertices.Add(transform.InverseTransformPoint(a));_vertices.Add(transform.InverseTransformPoint(b));_vertices.Add(transform.InverseTransformPoint(c));
        _colors.Add(color);_colors.Add(color);_colors.Add(color);_indices.Add(i);_indices.Add(i+1);_indices.Add(i+2);
    }
    private void OnDestroy() { if(_mesh!=null)Destroy(_mesh); }
}
