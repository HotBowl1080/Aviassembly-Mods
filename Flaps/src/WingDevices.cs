using System;
using UnityEngine;

// Fixed presets belong to the wing model. More wing models can define their own table.
internal static class BigWingPreset
{
    internal static readonly float[] FlapAngles = { 0f, 10f, 20f, 30f };
    internal static float Angle(int setting) { return FlapAngles[Mathf.Clamp(setting,0,FlapAngles.Length-1)]; }
}

internal sealed class WingDevices
{
    readonly BigWingGeometry mesh;
    readonly Vector3[] shell;
    readonly int[] skin;
    readonly int[] starts = new int[6], ends = new int[6];
    readonly Vector3[] hinges = new Vector3[6];
    readonly Vector3[] axes = new Vector3[6];
    readonly float[] chords = new float[6];
    internal WingDevices(BigWingGeometry mesh, Vector3[] shell, int[] skin)
    {
        this.mesh=mesh; this.shell=shell; this.skin=skin;
        float[,] spans={{-2.90f,-1.65f},{-1.57f,-.25f},{-.17f,1.32f}};
        for(int i=0;i<3;i++)
        {
            starts[i]=mesh.vertices.Count;
            BuildSlat(i,spans[i,0],spans[i,1]);
            ends[i]=mesh.vertices.Count;
            starts[i+3]=mesh.vertices.Count;
            BuildSpoiler(i+3,spans[i,0]+.12f,spans[i,1]-.12f);
            ends[i+3]=mesh.vertices.Count;
        }
    }
    void Section(float station,out float rear,out float front)
    {
        rear=float.PositiveInfinity; front=float.NegativeInfinity;
        for(int k=0;k<skin.Length;k+=3) for(int j=0;j<3;j++)
        {
            Vector3 a=shell[skin[k+j]],b=shell[skin[k+(j+1)%3]];
            if((a.z>station)==(b.z>station)) continue;
            float x=a.x+(station-a.z)*(b.x-a.x)/(b.z-a.z);
            rear=Math.Min(rear,x); front=Math.Max(front,x);
        }
        if(float.IsInfinity(front)) throw new ArgumentException("Missing wing cross section");
    }
    float Surface(float x,float z,bool top)
    {
        float height=top ? float.NegativeInfinity : float.PositiveInfinity;
        for(int k=0;k<skin.Length;k+=3)
        {
            Vector3 a=shell[skin[k]],b=shell[skin[k+1]],c=shell[skin[k+2]];
            float determinant=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
            if(Math.Abs(determinant)<1e-8f) continue;
            float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/determinant;
            float v=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/determinant;
            if(u<-.0001f || v<-.0001f || u+v>1.0001f) continue;
            float y=u*a.y+v*b.y+(1-u-v)*c.y;
            height=top ? Math.Max(height,y) : Math.Min(height,y);
        }
        if(float.IsInfinity(height)) throw new ArgumentException("Missing wing surface x="+x+" z="+z);
        return height;
    }
    void BuildSlat(int i,float first,float last)
    {
        float[] x={0f,.12f,.55f,1f,.6f,.12f};
        float[] y={0f,.36f,.22f,0f,-.20f,-.25f};
        Vector3[,] points=new Vector3[2,6];
        for(int side=0;side<2;side++)
        {
            float z=side==0?first:last,rear,front; Section(z,out rear,out front);
            float chord=Math.Min(.20f,(front-rear)*.18f);
            float nose=front-.035f;
            float center=(Surface(nose,z,true)+Surface(nose,z,false))*.5f;
            float thickness=Math.Min(.038f,Surface(nose,z,true)-Surface(nose,z,false))*.7f;
            for(int k=0;k<6;k++) points[side,k]=new Vector3(nose-x[k]*chord,center+y[k]*thickness,z);
        }
        hinges[i]=(points[0,0]+points[1,0])*.5f;
        axes[i]=(points[1,0]-points[0,0]).normalized;
        chords[i]=Math.Abs(points[0,0].x-points[0,3].x);
        Hull(points,6);
    }
    void BuildSpoiler(int i,float first,float last)
    {
        Vector3[,] points=new Vector3[2,4];
        for(int side=0;side<2;side++)
        {
            float z=side==0?first:last,rear,front; Section(z,out rear,out front);
            float leading=Mathf.Lerp(rear,front,.85f),trailing=Mathf.Lerp(rear,front,.64f);
            float ya=Surface(leading,z,true)+.004f,yb=Surface(trailing,z,true)+.004f;
            points[side,0]=new Vector3(leading,ya,z);
            points[side,1]=new Vector3(trailing,yb,z);
            points[side,2]=points[side,1]+Vector3.down*.012f;
            points[side,3]=points[side,0]+Vector3.down*.012f;
        }
        hinges[i]=points[0,0]; axes[i]=(points[1,0]-points[0,0]).normalized;
        Hull(points,4);
    }
    void Hull(Vector3[,] p,int count)
    {
        Vector3 center=Vector3.zero;
        for(int s=0;s<2;s++) for(int k=0;k<count;k++) center+=p[s,k];
        center/=2*count;
        for(int s=0;s<2;s++)
        {
            Vector3 cap=Vector3.zero;for(int k=0;k<count;k++) cap+=p[s,k];cap/=count;
            for(int k=0;k<count;k++) mesh.DeviceFace(cap,p[s,k],p[s,(k+1)%count],s==0?Vector3.back:Vector3.forward);
        }
        for(int k=0;k<count;k++) {
            int next=(k+1)%count; Vector3 outward=(p[0,k]+p[1,next])*.5f-center;
            mesh.DeviceFace(p[0,k],p[1,k],p[1,next],outward);
            mesh.DeviceFace(p[0,k],p[1,next],p[0,next],outward);
        }
    }
    internal void Pose(float flapAngle,float spoilers,Vector3 down)
    {
        for(int i=0;i<6;i++)
        {
            bool slat=i<3;
            float fraction=Mathf.Clamp01(flapAngle/20f);
            float angle=slat ? 16f*fraction : 55f*spoilers;
            Vector3 offset=slat ? Vector3.right*(chords[i]*.9f*fraction)+Vector3.down*(.045f*fraction) : Vector3.zero;
            // Positive local-Z rotation lowers the nose and raises the spoiler trailing edge.
            Quaternion rotation=Rotation(axes[i],-angle);
            for(int k=starts[i];k<ends[i];k++)
            {
                var v=mesh.vertices[k];
                mesh.positions[k]=hinges[i]+rotation*(v.p-hinges[i])+offset;
                mesh.normals[k]=rotation*v.n;
            }
        }
    }
    static Quaternion Rotation(Vector3 axis,float degrees)
    {
        double a=degrees*Math.PI/360; float s=(float)Math.Sin(a);
        return new Quaternion(axis.x*s,axis.y*s,axis.z*s,(float)Math.Cos(a));
    }
}




