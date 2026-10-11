using UnityEngine;
using UnityEngine.UI;

/// <summary>종류별 피해 비중을 텍스처 없이 부드러운 링 메시로 표시한다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class DamageContributionRingGraphic : MaskableGraphic
{
    private const int Segments = 120;
    private Color[] _colors;
    private float[] _shares;
    /// <summary>종류별 고정 색 순서를 받는다.</summary>
    public void Configure(Color[] colors)
    {
        _colors = colors; _shares = new float[colors.Length]; raycastTarget = false; SetVerticesDirty();
    }
    /// <summary>보간 중인 비중을 재사용 배열에 복사한다.</summary>
    public void SetShares(float[] shares)
    {
        bool changed = false;
        for(int i=0;i<_shares.Length;i++)
        {
            if(Mathf.Abs(_shares[i]-shares[i])>.0001f) changed=true;
            _shares[i]=shares[i];
        }
        if(changed) SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); Rect rect=GetPixelAdjustedRect(); float size=Mathf.Min(rect.width,rect.height);
        if(size<=0f) return;
        float radius=size*.45f, inner=size*.31f;
        Wedge(vh,rect.center,inner,radius,0f,1f,new Color(.35f,.44f,.50f,.25f));
        if(_shares==null) return;
        float start=0f;
        for(int i=0;i<_shares.Length;i++)
        {
            float end=Mathf.Min(1f,start+Mathf.Max(0f,_shares[i]));
            if(end-start>.001f) Wedge(vh,rect.center,inner,radius,start+.0012f,end-.0012f,_colors[i]);
            start=end;
        }
    }
    private static void Wedge(VertexHelper vh,Vector2 center,float inner,float outer,float from,float to,Color color)
    {
        int steps=Mathf.Max(1,Mathf.CeilToInt((to-from)*Segments));
        for(int i=0;i<steps;i++)
        {
            float a=Mathf.PI*.5f-Mathf.Lerp(from,to,i/(float)steps)*Mathf.PI*2f;
            float b=Mathf.PI*.5f-Mathf.Lerp(from,to,(i+1)/(float)steps)*Mathf.PI*2f;
            Vector2 da=new Vector2(Mathf.Cos(a),Mathf.Sin(a)), db=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
            int n=vh.currentVertCount;
            vh.AddVert(center+da*inner,color,Vector2.zero); vh.AddVert(center+da*outer,color,Vector2.zero);
            vh.AddVert(center+db*outer,color,Vector2.zero); vh.AddVert(center+db*inner,color,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
        }
    }
}
