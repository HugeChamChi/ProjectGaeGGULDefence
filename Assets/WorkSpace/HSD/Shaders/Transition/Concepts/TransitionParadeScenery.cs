using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class TransitionParadeScenery : MaskableGraphic
{
    public float Fill { get; set; }
    public float TravelTime { get; set; }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Mountains(vh, 360, 180, 28, new Color(.86f,.70f,.38f));
        Mountains(vh, 270, 110, 65, new Color(.75f,.58f,.29f));
        Quad(vh,-1300,-85,1300,-81,new Color(.65f,.48f,.24f));
        for(int i=-10;i<11;i++)
        {
            float x=i*135-Mathf.Repeat(TravelTime*160,135);
            Quad(vh,x,-103,x+42,-99,new Color(.74f,.58f,.32f));
        }
        // Capsule track and fill sit below the marching line.
        Capsule(vh,-250,-211,500,20,new Color(.81f,.64f,.34f));
        Capsule(vh,-246,-207,492,12,new Color(.98f,.88f,.62f));
        if(Fill>0) Capsule(vh,-246,-207,Mathf.Max(12,492*Mathf.Clamp01(Fill)),12,new Color(.32f,.23f,.15f));
        for(int i=0;i<3;i++)
        {
            float bounce=Mathf.Sin(TravelTime*4-i*.8f)*2;
            Disc(vh,new Vector2((i-1)*17,-242+bounce),3,new Color(.54f,.39f,.20f));
        }
    }
    private void Mountains(VertexHelper vh,float spacing,float height,float speed,Color tint)
    {
        float shift=Mathf.Repeat(TravelTime*speed,spacing*4);
        for(int i=-7;i<12;i++)
        {
            float x=i*spacing-shift;
            float peak=height*(.75f+.25f*Mathf.Sin(i*Mathf.PI*.5f));
            int n=vh.currentVertCount;
            vh.AddVert(new Vector2(x-spacing*.6f,-80),tint,Vector2.zero);
            vh.AddVert(new Vector2(x+spacing*.12f,peak),tint,Vector2.zero);
            vh.AddVert(new Vector2(x+spacing*.85f,-80),tint,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);
        }
    }
    private static void Quad(VertexHelper vh,float left,float bottom,float right,float top,Color c)
    {
        int n=vh.currentVertCount;
        vh.AddVert(new Vector2(left,bottom),c,Vector2.zero); vh.AddVert(new Vector2(left,top),c,Vector2.zero);
        vh.AddVert(new Vector2(right,top),c,Vector2.zero); vh.AddVert(new Vector2(right,bottom),c,Vector2.zero);
        vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
    }
    private static void Capsule(VertexHelper vh,float x,float y,float width,float height,Color c)
    {
        float r=height*.5f;
        Quad(vh,x+r,y,x+width-r,y+height,c);
        Disc(vh,new Vector2(x+r,y+r),r,c); Disc(vh,new Vector2(x+width-r,y+r),r,c);
    }
    private static void Disc(VertexHelper vh,Vector2 center,float radius,Color c)
    {
        int n=vh.currentVertCount; vh.AddVert(center,c,Vector2.zero);
        for(int i=0;i<=20;i++)
        {
            float a=i*Mathf.PI*.1f;
            vh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,c,Vector2.zero);
            if(i>0) vh.AddTriangle(n,n+i,n+i+1);
        }
    }
    private void Update() { SetVerticesDirty(); }
}
