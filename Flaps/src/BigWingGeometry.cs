using System;
using System.Collections.Generic;
using UnityEngine;

// Local X runs along the chord, Y is vertical, and Z runs along the span.
// Mesh groups: two flap skins, three guide rails, three rigid fairing tails, two vanes.
internal sealed class BigWingGeometry
{
    internal struct Vertex
    {
        internal Vector3 p, n;
        internal Vector2 uv;
        internal Vertex(Vector3 position, Vector3 normal, Vector2 texture) { p=position; n=normal; uv=texture; }
        internal static Vertex Blend(Vertex a, Vertex b, float t)
        {
            return new Vertex(Vector3.Lerp(a.p,b.p,t),Vector3.Lerp(a.n,b.n,t).normalized,Vector2.Lerp(a.uv,b.uv,t));
        }
    }
    internal readonly List<Vertex> vertices = new List<Vertex>();
    internal readonly List<int>[] triangles = { new List<int>(), new List<int>() };
    readonly List<int>[] moving = new List<int>[10];
    internal readonly Vector3[] origin=new Vector3[2], axis=new Vector3[2];
    Quaternion pitch;
    Vector3 travel;
    readonly Vector3[] pin=new Vector3[3], socket=new Vector3[3];
    readonly List<Vector3>[] rings={new List<Vector3>(),new List<Vector3>(),new List<Vector3>()};
    readonly Vector2 edgeUV;
    static readonly int[,] ends={{122,125,127,129},{124,123,126,128},{136,134,130,131},{137,135,133,132}};
    static readonly Vector3 aft=Vector3.left;
    const float VaneChord=.090f;
    static readonly Vector3 vanePinOffset=Vector3.left*.014f+Vector3.down*.080f;
    internal readonly Vector3[] positions,normals;
    int Add(Vertex v,int group)
    {
        int i=vertices.Count;
        vertices.Add(v);
        if(group>=0) moving[group].Add(i);
        return i;
    }
    internal BigWingGeometry(Vector3[] p,Vector3[] n,Vector2[] uv,int[] skin,int[] edge)
    {
        if (p.Length!=586 || n.Length!=586 || uv.Length!=586 || skin.Length!=324 || edge.Length!=1098 ||
            (p[122]-new Vector3(-.410793f,.122444f,.125264f)).sqrMagnitude>1e-7f ||
            (p[137]-new Vector3(-.549265f,.170759f,-.327072f)).sqrMagnitude>1e-7f)
            throw new ArgumentException("Unrecognized Big Wing geometry");
        edgeUV=uv[122];
        for(int i=0;i<moving.Length;i++) moving[i]=new List<int>();
        for(int f=0;f<2;f++)
        {
            origin[f]=p[ends[f*2,0]];
            axis[f]=(p[ends[f*2+1,0]]-origin[f]).normalized;
        }
        for(int i=0;i<p.Length;i++) Add(new Vertex(p[i],n[i],uv[i]), i>=122 && i<138 ? (i-122)/8 : -1);
        triangles[0].AddRange(skin);
        // The three fairings occupy distinct span bands. Slice each at the flap's leading edge.
        for(int i=0;i<edge.Length;i+=3)
        {
            int a=edge[i], b=edge[i+1], c=edge[i+2];
            if(a<138 || a>521)
            {
                triangles[1].Add(a); triangles[1].Add(b); triangles[1].Add(c);
                continue;
            }
            float z=p[a].z;
            int track=z>0 ? 0 : z> -1 ? 1 : 2;
            float station=track==0 ? .79f : track==1 ? -.47f : -1.54f;
            int f=track==0 ? 0 : 1;
            float cut=origin[f].x + (station-origin[f].z)*axis[f].x/axis[f].z;
            Vertex[] face={vertices[a],vertices[b],vertices[c]};
            Clip(face,cut,false,track,-1);
            Clip(face,cut,true,track,5+track);
        }
        for(int f=0;f<2;f++) {
            ClosePocket(p,f,true);
            ClosePocket(p,f,false);
            BuildVane(p,f);
        }
        for(int t=0;t<3;t++) FinishTrack(t);
        positions=new Vector3[vertices.Count]; normals=new Vector3[vertices.Count];
        for(int i=0;i<vertices.Count;i++) { positions[i]=vertices[i].p; normals[i]=vertices[i].n; }
    }
    void Clip(Vertex[] face,float cut,bool rear,int track,int group)
    {
        List<Vertex> polygon=new List<Vertex>(4);
        for(int i=0;i<3;i++)
        {
            Vertex a=face[i],b=face[(i+1)%3];
            bool aInside=rear ? a.p.x<cut : a.p.x>=cut;
            bool bInside=rear ? b.p.x<cut : b.p.x>=cut;
            if(aInside) polygon.Add(a);
            if(aInside==bInside) continue;
            Vertex cross=Vertex.Blend(a,b,(cut-a.p.x)/(b.p.x-a.p.x));
            polygon.Add(cross);
            if(rear && !rings[track].Exists(q=>(q-cross.p).sqrMagnitude<1e-10f)) rings[track].Add(cross.p);
        }
        if(polygon.Count<3) return;
        int first=Add(polygon[0],group), prev=Add(polygon[1],group);
        for(int i=2;i<polygon.Count;i++) {
            int next=Add(polygon[i],group); triangles[1].Add(first); triangles[1].Add(prev); triangles[1].Add(next); prev=next;
        }
    }
    void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 outward,int group)
    {
        Vector3 normal=Vector3.Cross(b-a,c-a);
        if(normal.sqrMagnitude<1e-14f) return;
        if(Vector3.Dot(normal,outward)<0) { Vector3 tmp=b; b=c; c=tmp; normal=-normal; }
        normal=normal.normalized;
        triangles[1].Add(Add(new Vertex(a,normal,edgeUV),group));
        triangles[1].Add(Add(new Vertex(b,normal,edgeUV),group));
        triangles[1].Add(Add(new Vertex(c,normal,edgeUV),group));
    }
    void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 outward,int group)
    {
        Face(a,b,c,outward,group); Face(a,c,d,outward,group);
    }
    void ClosePocket(Vector3[] p,int flap,bool fixedWall)
    {
        // The stock shell ends at these edges. Close the fixed cut face and the
        // moving panel's nose independently. The opening between them stays clear.
        int group=fixedWall ? -1 : flap;
        const int steps=6;
        Vector3[,] curve=new Vector3[2,steps+1];
        for(int s=0;s<2;s++)
        {
            int e=2*flap+s;
            Vector3 top=p[ends[e,0]], bottom=p[ends[e,3]];
            Vector3 forward=Vector3.ProjectOnPlane(-aft,axis[flap]).normalized;
            List<Vector3> rim=new List<Vector3>();
            rim.Add(p[ends[e,1]]); rim.Add(p[ends[e,2]]);
            for(int k=0;k<=steps;k++)
            {
                float theta=(float)Math.PI*k/steps;
                Vector3 point=Vector3.Lerp(top,bottom,.5f-.5f*(float)Math.Cos(theta))+
                    forward*((fixedWall ? .315f : .050f)*(float)Math.Sin(theta));
                curve[s,k]=point;
            }
            for(int k=steps;k>=0;k--) rim.Add(curve[s,k]);
            Vector3 center=Vector3.zero;
            foreach(Vector3 q in rim) center+=q;
            center/=rim.Count;
            Vector3 outward=axis[flap]*(s==0 ? -1f : 1f)*(fixedWall ? -1f : 1f);
            for(int k=0;k<rim.Count;k++) Face(center,rim[k],rim[(k+1)%rim.Count],outward,group);
        }
        for(int k=0;k<steps;k++)
        {
            Vector3 tangent=curve[0,k+1]-curve[0,k];
            Vector3 outward=Vector3.Cross(axis[flap],tangent);
            if(Vector3.Dot(outward,aft)<0) outward=-outward;
            if(!fixedWall) outward=-outward;
            Quad(curve[0,k],curve[1,k],curve[1,k+1],curve[0,k+1],outward,group);
        }
    }
    void BuildVane(Vector3[] p,int flap)
    {
        // Rounded leading edge and a tapered trailing edge. The vane nests inside the cove.
        float[] chord={0f,.05f,.24f,.62f,1f,.68f,.25f,.05f};
        float[] height={0f,.007f,.012f,.009f,.0015f,-.005f,-.008f,-.005f};
        Vector3[,] profile=new Vector3[2,chord.Length];
        int group=8+flap;
        float first=p[ends[2*flap,0]].z+.075f;
        float last=p[ends[2*flap+1,0]].z-.075f;
        for(int side=0;side<2;side++) {
            Vector3 nose=HingeAt(flap,side==0 ? first : last)-aft*.180f+Vector3.down*.045f;
            for(int k=0;k<chord.Length;k++)
                profile[side,k]=nose+aft*(chord[k]*VaneChord)+Vector3.up*height[k];
            Vector3 center=nose+aft*(VaneChord*.43f);
            for(int k=0;k<chord.Length;k++)
                Face(center,profile[side,k],profile[side,(k+1)%chord.Length],side==0 ? Vector3.back : Vector3.forward,group);
        }
        Vector3 sectionCenter=HingeAt(flap,first)+aft*(-.180f+VaneChord*.43f)+Vector3.down*.045f;
        for(int k=0;k<chord.Length;k++) {
            int next=(k+1)%chord.Length;
            Vector3 outward=(profile[0,k]+profile[0,next])*.5f-sectionCenter;
            Quad(profile[0,k],profile[1,k],profile[1,next],profile[0,next],outward,group);
        }
    }
    void VaneBracket(int flap,float station)
    {
        // A narrow bracket rotates with the vane about a pin carried by the main flap.
        Vector3 hinge=HingeAt(flap,station);
        Vector3 pivot=hinge+vanePinOffset;
        Vector3[] corner={hinge-aft*.151f+Vector3.down*.045f,hinge-aft*.120f+Vector3.down*.046f,pivot};
        Vector3 dz=Vector3.forward*.012f;
        int group=8+flap;
        Face(corner[0]-dz,corner[1]-dz,corner[2]-dz,-dz,group);
        Face(corner[0]+dz,corner[1]+dz,corner[2]+dz,dz,group);
        Vector3 center=(corner[0]+corner[1]+corner[2])/3f;
        for(int k=0;k<3;k++) {
            Vector3 a=corner[k],b=corner[(k+1)%3];
            Quad(a-dz,b-dz,b+dz,a+dz,(a+b)*.5f-center,group);
        }
    }
    void FinishTrack(int track)
    {
        List<Vector3> ring=rings[track];
        if(ring.Count<3) throw new ArgumentException("Missing fairing section");
        Vector3 low=ring[0], high=ring[0];
        foreach(Vector3 p in ring) { low=Vector3.Min(low,p); high=Vector3.Max(high,p); }
        Vector3 center=(low+high)*.5f;
        VaneBracket(track==0 ? 0 : 1,center.z);
        ring.Sort((a,b)=>Math.Atan2(a.z-center.z,a.y-center.y).CompareTo(Math.Atan2(b.z-center.z,b.y-center.y)));
        for(int i=0;i<ring.Count;i++) {
            Vector3 a=ring[i], b=ring[(i+1)%ring.Count];
            Face(center,a,b,aft,-1); Face(center,b,a,-aft,5+track);
        }
        // Fixed pin inside the forward housing. The rail pivots about it and
        // slides through the socket in the moving tail, with constant length.
        pin[track]=center-aft*.09f; socket[track]=center+aft*.07f;
        float radius=Math.Min(high.y-low.y,high.z-low.z)*.17f;
        for(int j=0;j<8;j++) {
            float a=(float)(j*Math.PI/4), b=(float)((j+1)*Math.PI/4);
            Vector3 u=new Vector3(0,(float)Math.Cos(a),(float)Math.Sin(a))*radius;
            Vector3 v=new Vector3(0,(float)Math.Cos(b),(float)Math.Sin(b))*radius;
            Vector3 start=pin[track], end=start+aft*.515f;
            Quad(start+u,start+v,end+v,end+u,u+v,track+2);
        }
    }
    static Quaternion AxisAngle(Vector3 unit,float angle)
    {
        double half=angle*Math.PI/360;
        float s=(float)Math.Sin(half);
        return new Quaternion(unit.x*s,unit.y*s,unit.z*s,(float)Math.Cos(half));
    }
    Vector3 HingeAt(int flap,float station)
    {
        return origin[flap]+axis[flap]*((station-origin[flap].z)/axis[flap].z);
    }
    Vector3 TransformPoint(int flap,Vector3 point)
    {
        Vector3 hinge=HingeAt(flap,point.z);
        return hinge+travel+pitch*(point-hinge);
    }
    internal void Pose(float degrees,Vector3 down)
    {
        // Match the motion of the chord at each span station along the swept cutout.
        // A rotation around the diagonal cut edge also swings the trailing edge sideways.
        float sign=down.y>0f ? -1f : 1f;
        pitch=AxisAngle(Vector3.forward,sign*degrees);
        Vector3 sectionDown=new Vector3(down.x,down.y,0f).normalized;
        float tStage=degrees/30f;
        travel=aft*(.22f*tStage*(1.22f-.22f*tStage))+sectionDown*(.072f*tStage*(.75f+.25f*tStage));
        Quaternion vanePitch=AxisAngle(Vector3.forward,sign*degrees*.45f);
        for(int f=0;f<2;f++)
        {
            Vector3 slope=axis[f]/axis[f].z;
            Vector3 spanCorrection=slope-pitch*slope;
            foreach(int i in moving[f])
            {
                Vertex rest=vertices[i];
                positions[i]=TransformPoint(f,rest.p);
                // Inverse-transpose for the swept, section-wise surface mapping.
                Vector3 normal=pitch*rest.n;
                normal.z-=normal.x*spanCorrection.x+normal.y*spanCorrection.y;
                normals[i]=normal.normalized;
            }
            Vector3 vaneSpanCorrection=slope-vanePitch*slope;
            foreach(int i in moving[8+f])
            {
                Vertex rest=vertices[i];
                Vector3 pinRest=HingeAt(f,rest.p.z)+vanePinOffset;
                positions[i]=TransformPoint(f,pinRest)+vanePitch*(rest.p-pinRest);
                Vector3 normal=vanePitch*rest.n;
                normal.z-=normal.x*vaneSpanCorrection.x+normal.y*vaneSpanCorrection.y;
                normals[i]=normal.normalized;
            }
        }
        for(int t=0;t<3;t++)
        {
            int flap=t==0 ? 0 : 1;
            Vector3 hinge=HingeAt(flap,socket[t].z);
            foreach(int i in moving[5+t])
            {
                positions[i]=hinge+travel+pitch*(vertices[i].p-hinge);
                normals[i]=pitch*vertices[i].n;
            }
            Vector3 direction=(TransformPoint(flap,socket[t])-pin[t]).normalized;
            Vector3 cross=Vector3.Cross(aft,direction);
            // Quaternion between two nearly parallel unit vectors.
            float w=1f+Vector3.Dot(aft,direction);
            float inv=1f/(float)Math.Sqrt(cross.sqrMagnitude+w*w);
            Quaternion q=new Quaternion(cross.x*inv,cross.y*inv,cross.z*inv,w*inv);
            foreach(int i in moving[t+2]) { positions[i]=pin[t]+q*(vertices[i].p-pin[t]); normals[i]=q*vertices[i].n; }
        }
    }
}


