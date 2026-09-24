using System;
using UnityEngine;

namespace Bola;

// Cosmetic only. Never used for hit detection or authority.
public sealed class RopeMotion : MonoBehaviour
{
    private const int Segments=8, Sides=6;
    private sealed class Chain
    {
        public readonly Vector3[] Position=new Vector3[Segments+1], Previous=new Vector3[Segments+1];
        public Transform Weight=null!, Rope=null!;
        public Mesh Mesh=null!;
        public float Length;
    }
    private Chain[] chains=Array.Empty<Chain>();
    private float accumulated;
    private Vector3 lastAnchor;
    public float OverheadBlend { get; set; }
    private Vector3 Anchor => transform.TransformPoint(new Vector3(0,-.07f,0));
    private void Start()
    {
        var close=transform.Find("Close");
        if(!close) return;
        chains=new Chain[3];
        for(int i=0;i<3;i++)
        {
            var c=chains[i]=new Chain { Weight=close.Find("Weight"+i),Rope=close.Find("Rope"+i) };
            c.Length=Vector3.Distance(Anchor,c.Weight.position);
            c.Mesh=new Mesh {name="Bola cosmetic rope"}; c.Mesh.MarkDynamic();
            c.Rope.GetComponent<MeshFilter>().mesh=c.Mesh;
        }
        ResetChains();
    }
    private void ResetChains()
    {
        lastAnchor=Anchor; accumulated=0;
        foreach(var c in chains)
            for(int j=0;j<=Segments;j++) c.Previous[j]=c.Position[j]=Vector3.Lerp(Anchor,c.Weight.position,(float)j/Segments);
    }
    private void LateUpdate()
    {
        if(chains.Length==0) return;
        if(Vector3.Distance(lastAnchor,Anchor)>2) ResetChains();
        lastAnchor=Anchor;
        accumulated=Mathf.Min(accumulated+Time.deltaTime,4f/60);
        while(accumulated>=1f/60)
        {
            accumulated-=1f/60;
            for(int i=0;i<chains.Length;i++) Step(chains[i],i);
            // Keep the three weights from occupying the same point when hanging still.
            for(int a=0;a<chains.Length;a++) for(int b=a+1;b<chains.Length;b++)
            {
                var delta=chains[b].Position[Segments]-chains[a].Position[Segments];
                float distance=delta.magnitude;
                if(distance>=.11f) continue;
                var normal=distance>.00001f?delta/distance:transform.right;
                var correction=normal*((.11f-distance)*.5f);
                chains[a].Position[Segments]-=correction; chains[b].Position[Segments]+=correction;
            }
        }
        for(int i=0;i<chains.Length;i++)
        {
            var c=chains[i]; c.Weight.position=c.Position[Segments];
            Draw(c);
            var distant=transform.Find("Distant");
            if(distant)
            {
                distant.Find("Weight"+i).position=c.Weight.position;
                // Share the bounded 96-triangle rope mesh; LOD retains reduced weight geometry.
                distant.Find("Rope"+i).GetComponent<MeshFilter>().sharedMesh=c.Mesh;
            }
        }
    }
    private void Step(Chain c,int branch)
    {
        c.Position[0]=Anchor;
        for(int j=1;j<=Segments;j++)
        {
            var p=c.Position[j];
            c.Position[j]+=(p-c.Previous[j])*.975f+Vector3.down*(9.81f/3600);
            c.Previous[j]=p;
            if(OverheadBlend>0)
            {
                float angle=Time.time*10+branch*Mathf.PI*2/3;
                var offset=new Vector3(Mathf.Cos(angle),.12f,Mathf.Sin(angle))*(c.Length*j/Segments);
                c.Position[j]=Vector3.Lerp(c.Position[j],Anchor+offset,OverheadBlend*.3f);
            }
        }
        for(int iteration=0;iteration<8;iteration++)
        {
            c.Position[0]=Anchor;
            for(int j=0;j<Segments;j++)
            {
                var delta=c.Position[j+1]-c.Position[j]; float distance=delta.magnitude;
                if(distance<.00001f) continue;
                var correction=delta*((distance-c.Length/Segments)/distance);
                if(j==0) c.Position[j+1]-=correction;
                else { c.Position[j]+=correction*.5f; c.Position[j+1]-=correction*.5f; }
            }
        }
    }
    private static void Draw(Chain c)
    {
        var vertices=new Vector3[(Segments+1)*Sides];
        var uv=new Vector2[vertices.Length]; var indices=new int[Segments*Sides*6];
        for(int j=0;j<=Segments;j++)
        {
            var tangent=(c.Position[Math.Min(j+1,Segments)]-c.Position[Math.Max(j-1,0)]).normalized;
            var normal=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.9f?Vector3.right:Vector3.up).normalized;
            var binormal=Vector3.Cross(tangent,normal);
            for(int k=0;k<Sides;k++)
            {
                float a=k*Mathf.PI*2/Sides;
                vertices[j*Sides+k]=c.Rope.InverseTransformPoint(c.Position[j]+.004f*(normal*Mathf.Cos(a)+binormal*Mathf.Sin(a)));
                uv[j*Sides+k]=new Vector2((float)k/Sides,(float)j/Segments);
                if(j==Segments) continue;
                int at=(j*Sides+k)*6, a0=j*Sides+k, b=j*Sides+(k+1)%Sides;
                indices[at]=a0; indices[at+1]=b; indices[at+2]=a0+Sides;
                indices[at+3]=b; indices[at+4]=b+Sides; indices[at+5]=a0+Sides;
            }
        }
        c.Mesh.vertices=vertices; c.Mesh.uv=uv; c.Mesh.triangles=indices; c.Mesh.RecalculateNormals(); c.Mesh.RecalculateBounds();
    }
    private void OnDestroy() { foreach(var c in chains) if(c.Mesh) Destroy(c.Mesh); }
}
