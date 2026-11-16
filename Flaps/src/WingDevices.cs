using System;
using System.Collections.Generic;
using UnityEngine;

internal static class BigWingPreset
{
    internal static readonly float[] FlapAngles = {0f,10f,20f,30f};
    internal static float Angle(int stage) { return FlapAngles[Mathf.Clamp(stage,0,FlapAngles.Length-1)]; }
    internal static float SlatExtension(float angle) { return Mathf.Clamp01((angle-10f)/10f); }
    internal static float LeadingEdgeExtension(float angle) { return Mathf.Clamp01((angle-20f)/10f); }
}

// Devices are cut from the stock skin. Every original triangle is partitioned
// once, retaining its material, UVs and surface shape.
internal sealed class WingDevices
{
    sealed class Device
    {
        internal float first,last,cutFirst,cutLast,frontFirst,frontLast,lowerFirst,lowerLast;
        internal bool spoiler,leadingFlap,fixedNose;
        internal float hingeYFirst,hingeYLast;
        internal readonly List<int> indices=new List<int>();
        internal float Cut(float z) { return Lerp(cutFirst,cutLast,z); }
        internal float Front(float z) { return Lerp(frontFirst,frontLast,z); }
        internal float Lower(float z) { return Lerp(lowerFirst,lowerLast,z); }
        float Lerp(float a,float b,float z) { return a+(b-a)*(z-first)/(last-first); }
    }
    readonly BigWingGeometry mesh;
    readonly Vector3[] shell;
    readonly int[] skin;
    readonly int[] surface;
    readonly List<Device> devices=new List<Device>();
    readonly List<Device> fixedNoses=new List<Device>();
    readonly List<int> cutPaint=new List<int>();
    readonly List<int[]> leadingCaps=new List<int[]>();
    internal readonly int[] skinPartition;
    internal readonly int StockVertexCount;
    internal WingDevices(BigWingGeometry mesh,Vector3[] shell,int[] skin)
    {
        this.mesh=mesh; this.shell=shell; this.skin=skin;
        var completeSurface=new List<int>(skin);
        var edges=mesh.triangles[1];
        for(int i=0;i<edges.Count;i+=3)
        {
            int a=edges[i],b=edges[i+1],c=edges[i+2];
            if(a<122&&b<122&&c<122 || a>=522&&a<586&&b>=522&&b<586&&c>=522&&c<586)
            {completeSurface.Add(a);completeSurface.Add(b);completeSurface.Add(c);}
        }
        surface=completeSurface.ToArray();
        StockVertexCount=mesh.vertices.Count;
        AddLeading(-3.82f,-2.525f,false);
        AddLeading(-2.517f,-1.16f,false);
        AddLeading(-1.152f,.20f,false);
        AddLeading(.22f,1.46f,true);
        AddSpoiler(-1.66f,-.43f);
        AddSpoiler(.20f,1.13f);
        AddFixedNose(-4.08f,-3.82f);
        AddFixedNose(-2.525f,-2.517f);
        AddFixedNose(-1.16f,-1.152f);
        AddFixedNose(.20f,1.68f);
        var cuts=new List<Device>(devices);
        cuts.AddRange(fixedNoses);
        for(int sub=0;sub<2;sub++)
        {
            int[] original=mesh.triangles[sub].ToArray();
            mesh.triangles[sub].Clear();
            for(int t=0;t<original.Length;t+=3)
            {
                int a=original[t],b=original[t+1],c=original[t+2];
                // Preserve existing flap/track geometry, including its moving groups.
                bool nativeShell=a<122 && b<122 && c<122 ||
                    a>=522 && a<586 && b>=522 && b<586 && c>=522 && c<586;
                if(!nativeShell) { mesh.triangles[sub].Add(a);mesh.triangles[sub].Add(b);mesh.triangles[sub].Add(c);continue; }
                var fragments=new List<List<BigWingGeometry.Vertex>>();
                fragments.Add(new List<BigWingGeometry.Vertex>{mesh.vertices[a],mesh.vertices[b],mesh.vertices[c]});
                foreach(Device d in cuts)
                {
                    if(d.leadingFlap)continue;
                    var remainder=new List<List<BigWingGeometry.Vertex>>();
                    foreach(var polygon in fragments)
                    {
                        var inside=polygon;
                        int planes=d.spoiler?5:d.leadingFlap?4:3;
                        for(int plane=0;plane<planes && inside.Count>=3;plane++)
                        {
                            List<BigWingGeometry.Vertex> kept,outside;
                            Split(inside,d,plane,out kept,out outside);
                            if(outside.Count>=3) remainder.Add(outside);
                            inside=kept;
                        }
                        if(inside.Count>=3)
                        {
                            if(d.spoiler) Emit(inside,sub,d);
                            else
                            {
                                int start=d.indices.Count;
                                Emit(inside,-1,d);
                                if(sub==0)for(int j=start;j<d.indices.Count;j++)cutPaint.Add(d.indices[j]);
                            }
                        }
                    }
                    fragments=remainder;
                }
                foreach(var polygon in fragments) Emit(polygon,sub,null);
            }
        }
        var originalPartition=new List<int>(mesh.triangles[0]);
        originalPartition.AddRange(cutPaint);
        skinPartition=originalPartition.ToArray();
        foreach(Device d in devices)
        {
            CloseDevice(d);
        }
        foreach(Device d in fixedNoses) CloseLeadingSkin(d);
    }
    static float Signed(Vector3 p,Device d,int plane)
    {
        if(plane==0)return p.z-d.first;
        if(plane==1)return d.last-p.z;
        if(plane==2)return p.x-d.Cut(p.z);
        if(plane==3)return d.leadingFlap?d.Lower(p.z)-p.y:d.Front(p.z)-p.x;
        return p.y-d.Lower(p.z);
    }
    static void Split(List<BigWingGeometry.Vertex> polygon,Device d,int plane,
        out List<BigWingGeometry.Vertex> inside,out List<BigWingGeometry.Vertex> outside)
    {
        inside=new List<BigWingGeometry.Vertex>();outside=new List<BigWingGeometry.Vertex>();
        for(int i=0;i<polygon.Count;i++)
        {
            var a=polygon[i];var b=polygon[(i+1)%polygon.Count];
            float da=Signed(a.p,d,plane),db=Signed(b.p,d,plane);
            bool ia=da>=0f,ib=db>=0f;
            (ia?inside:outside).Add(a);
            if(ia==ib)continue;
            var cross=BigWingGeometry.Vertex.Blend(a,b,da/(da-db));
            inside.Add(cross);outside.Add(cross);
        }
    }
    void Emit(List<BigWingGeometry.Vertex> polygon,int sub,Device d)
    {
        for(int i=1;i<polygon.Count-1;i++)
        {
            var a=polygon[0];var b=polygon[i];var c=polygon[i+1];
            if(Vector3.Cross(b.p-a.p,c.p-a.p).sqrMagnitude<1e-14f)continue;
            foreach(var vertex in new[]{a,b,c})
            {
                int id=mesh.vertices.Count;mesh.vertices.Add(vertex);
                if(sub>=0)mesh.triangles[sub].Add(id);
                if(d!=null)d.indices.Add(id);
            }
        }
    }
    void Section(float z,out float rear,out float front)
    {
        rear=float.PositiveInfinity;front=float.NegativeInfinity;
        for(int i=0;i<surface.Length;i+=3)for(int e=0;e<3;e++)
        {
            Vector3 a=shell[surface[i+e]],b=shell[surface[i+(e+1)%3]];
            if((a.z>z)==(b.z>z))continue;
            float x=a.x+(z-a.z)*(b.x-a.x)/(b.z-a.z);
            rear=Math.Min(rear,x);front=Math.Max(front,x);
        }
        if(float.IsInfinity(front))throw new ArgumentException("Missing leading edge section");
    }
    float Surface(float x,float z,bool top)
    {
        float y=top?float.NegativeInfinity:float.PositiveInfinity;
        for(int i=0;i<surface.Length;i+=3)
        {
            Vector3 a=shell[surface[i]],b=shell[surface[i+1]],c=shell[surface[i+2]];
            float det=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
            if(Math.Abs(det)<1e-8f)continue;
            float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/det;
            float v=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/det;
            if(u<-.0001f||v<-.0001f||u+v>1.0001f)continue;
            float value=u*a.y+v*b.y+(1-u-v)*c.y;
            y=top?Math.Max(y,value):Math.Min(y,value);
        }
        if(float.IsInfinity(y))throw new ArgumentException("Missing wing surface "+x+","+z);
        return y;
    }
    void AddLeading(float first,float last,bool flap)
    {
        float rearA,frontA,rearB,frontB;Section(first,out rearA,out frontA);Section(last,out rearB,out frontB);
        var d=new Device{first=first,last=last,leadingFlap=flap,
            cutFirst=frontA-Math.Min(flap?.15f:.10f,(frontA-rearA)*.075f),
            cutLast=frontB-Math.Min(flap?.15f:.10f,(frontB-rearB)*.075f)};
        d.hingeYFirst=(Surface(d.cutFirst,first,true)+Surface(d.cutFirst,first,false))*.5f;
        d.hingeYLast=(Surface(d.cutLast,last,true)+Surface(d.cutLast,last,false))*.5f;
        if(flap)
        {
            d.lowerFirst=d.hingeYFirst;d.lowerLast=d.hingeYLast;
            d.frontFirst=frontA-.008f;d.frontLast=frontB-.008f;
            d.hingeYFirst=Surface(d.frontFirst,first,false);
            d.hingeYLast=Surface(d.frontLast,last,false);
        }
        devices.Add(d);
    }
    void AddFixedNose(float first,float last)
    {
        AddLeading(first,last,false);
        Device d=devices[devices.Count-1];
        devices.RemoveAt(devices.Count-1);
        d.fixedNose=true;fixedNoses.Add(d);
    }
    void AddSpoiler(float first,float last)
    {
        // Both panels follow the sweep of the existing flap leading edges.
        int f=first<0?1:0;
        Vector3 start=mesh.origin[f],axis=mesh.axis[f];
        float aftA=start.x+(first-start.z)*axis.x/axis.z+.045f;
        float aftB=start.x+(last-start.z)*axis.x/axis.z+.045f;
        var d=new Device{first=first,last=last,spoiler=true,cutFirst=aftA,cutLast=aftB,
            frontFirst=aftA+.24f,frontLast=aftB+.24f};
        d.lowerFirst=(Surface(aftA+.12f,first,true)+Surface(aftA+.12f,first,false))*.5f;
        d.lowerLast=(Surface(aftB+.12f,last,true)+Surface(aftB+.12f,last,false))*.5f;
        d.hingeYFirst=Surface(d.frontFirst,first,true);
        d.hingeYLast=Surface(d.frontLast,last,true);
        devices.Add(d);
    }
    void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 outward,Device owner,int material=1)
    {
        Vector3 n=Vector3.Cross(b-a,c-a);
        if(n.sqrMagnitude<1e-14f)return;
        if(Vector3.Dot(n,outward)<0){Vector3 tmp=b;b=c;c=tmp;n=-n;}
        n=n.normalized;
        Vector2 uv=mesh.vertices[122].uv;
        Emit(new List<BigWingGeometry.Vertex>{
            new BigWingGeometry.Vertex(a,n,uv),new BigWingGeometry.Vertex(b,n,uv),new BigWingGeometry.Vertex(c,n,uv)},material,owner);
    }
    void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal,Device owner,int material=1)
    {Face(a,b,c,normal,owner,material);Face(a,c,d,normal,owner,material);}
    void CloseDevice(Device d)
    {
        if(d.leadingFlap) { BuildKrueger(d); return; }
        if(d.indices.Count<6)throw new ArgumentException("Device cut produced no surface");
        if(d.spoiler)
        {
            // Thin rigid panel with a shallow fixed well under it.
            var boundary=new[]{d.first,d.last};
            Vector3[,] p=new Vector3[2,4];
            for(int s=0;s<2;s++)
            {
                float z=boundary[s],rear=d.Cut(z),front=d.Front(z);
                p[s,0]=new Vector3(front,Surface(front,z,true),z);
                p[s,1]=new Vector3(rear,Surface(rear,z,true),z);
                p[s,2]=p[s,1]+Vector3.down*.012f;p[s,3]=p[s,0]+Vector3.down*.012f;
                Quad(p[s,0],p[s,1],p[s,2],p[s,3],s==0?Vector3.back:Vector3.forward,d);
            }
            Quad(p[0,2],p[1,2],p[1,3],p[0,3],Vector3.down,d);
            Quad(p[0,1],p[1,1],p[1,2],p[0,2],Vector3.left,d);
            Quad(p[0,3],p[1,3],p[1,0],p[0,0],Vector3.right,d);
            Vector3 depth=Vector3.down*.024f;
            Quad(p[0,0]+depth,p[1,0]+depth,p[1,1]+depth,p[0,1]+depth,Vector3.up,null);
            return;
        }
        CloseLeadingSkin(d);
    }
    void BuildKrueger(Device d)
    {
        d.indices.Clear();
        const int stations=9,points=13;
        var outer=new Vector3[stations,points];
        var inner=new Vector3[stations,points];
        float[] hingeX=new float[stations],hingeY=new float[stations];
        for(int s=0;s<stations;s++)
        {
            float z=Mathf.Lerp(d.first,d.last,s/(float)(stations-1));
            float rear,front;Section(z,out rear,out front);
            float x=front-.055f;
            float y=StowedBottom(x,z)+.014f;
            hingeX[s]=x;hingeY[s]=y;
            for(int k=0;k<points;k++)
            {
                float t=k/(float)(points-1);
                // Curved underside door with a rolled lip, stowed inside the nose.
                outer[s,k]=new Vector3(x-.09f*t,StowedBottom(x-.09f*t,z)+.014f+.004f*(float)Math.Sin(t*Math.PI),z);
                inner[s,k]=outer[s,k]+Vector3.up*.003f;
            }
        }
        d.frontFirst=hingeX[0];d.frontLast=hingeX[stations-1];
        d.hingeYFirst=hingeY[0];d.hingeYLast=hingeY[stations-1];
        SkinLoft(outer,inner,d);
    }
    float StowedBottom(float x,float z)
    {
        Device nose=null;
        foreach(Device part in fixedNoses)
            if(z>=part.first && z<=part.last && x>=part.Cut(z))nose=part;
        foreach(Device part in devices)
            if(!part.spoiler && !part.leadingFlap && z>=part.first && z<=part.last && x>=part.Cut(z))nose=part;
        if(nose==null)return Surface(x,z,false);
        float rear,front;Section(z,out rear,out front);
        float cut=nose.Cut(z),top=Surface(cut,z,true),bottom=Surface(cut,z,false);
        float center=(top+bottom)*.5f,height=(top-bottom)*.5f,depth=front-cut;
        double[] angles={0.0,70.0,125.0,180.0};
        float result=float.PositiveInfinity;
        for(int i=0;i<3;i++)
        {
            double a=angles[i]*Math.PI/180.0,b=angles[i+1]*Math.PI/180.0;
            float ax=cut+depth*(float)Math.Sin(a),bx=cut+depth*(float)Math.Sin(b);
            if(x<Math.Min(ax,bx)-.00001f || x>Math.Max(ax,bx)+.00001f)continue;
            float t=(x-ax)/(bx-ax);
            float y=Mathf.LerpUnclamped(center+height*(float)Math.Cos(a),center+height*(float)Math.Cos(b),t);
            result=Math.Min(result,y);
        }
        if(float.IsInfinity(result))throw new ArgumentException("LE flap lies outside rounded wing nose");
        return result;
    }

    void CloseLeadingSkin(Device d)
    {
        d.indices.Clear();
        const int stations=5;
        double[] angles={0.0,70.0,125.0,180.0};
        var outer=new Vector3[stations,3];
        var inner=new Vector3[stations,3];
        var core=new Vector3[stations,4];
        var shadingSkin=new Vector3[stations,4];
        var fixedSkin=new Vector3[stations,2];
        var fixedInner=new Vector3[stations,2];
        for(int s=0;s<stations;s++)
        {
            float z=Mathf.Lerp(d.first,d.last,s/(float)(stations-1));
            float x=d.Cut(z),rear,front;Section(z,out rear,out front);
            float top=Surface(x,z,true),bottom=Surface(x,z,false);
            float cy=(top+bottom)*.5f,height=(top-bottom)*.5f,depth=front-x;
            for(int k=0;k<angles.Length;k++)
            {
                double theta=angles[k]*Math.PI/180.0;
                float sin=(float)Math.Sin(theta),cos=(float)Math.Cos(theta);
                Vector3 normal=new Vector3(sin/Math.Max(depth,.001f),cos/Math.Max(height,.001f),0f).normalized;
                Vector3 skinPoint=new Vector3(x+depth*sin,cy+height*cos,z);
                shadingSkin[s,k]=skinPoint;
                if(k<3)
                {
                    outer[s,k]=skinPoint;
                    inner[s,k]=skinPoint-normal*.003f;
                }
                if(k>1)
                {
                    fixedSkin[s,k-2]=skinPoint;
                    fixedInner[s,k-2]=skinPoint-normal*.003f;
                }
                core[s,k]=new Vector3(x+Math.Max(depth-.012f,.008f)*sin,cy+height*cos,z);
            }
        }
        // Two facets move as the slat. Fixed nose sections fill the full span.
        SkinLoft(outer,inner,d.fixedNose?null:d,shadingSkin,0);
        SkinLoft(fixedSkin,fixedInner,null,shadingSkin,2);
        for(int s=0;s<stations-1;s++)for(int k=0;k<3;k++)
        {
            Vector3 normal=Vector3.Cross(core[s+1,k]-core[s,k],core[s,k+1]-core[s,k]);
            SmoothQuad(core,s,k,null,0,false);
        }
        for(int side=0;side<2;side++)
        {
            int s=side==0?0:stations-1;
            Vector3 center=(core[s,0]+core[s,3])*.5f;
            for(int k=0;k<3;k++)
                Face(center,core[s,k],core[s,k+1],side==0?Vector3.back:Vector3.forward,null,0);
        }
        for(int s=0;s<stations-1;s++)
            Quad(core[s,0],core[s,3],core[s+1,3],core[s+1,0],Vector3.left,null,0);
    }

    Vector3 ShadingNormal(Vector3[,] grid,int s,int k)
    {
        int lastSpan=grid.GetLength(0)-1,lastCurve=grid.GetLength(1)-1;
        Vector3 span=grid[Math.Min(s+1,lastSpan),k]-grid[Math.Max(s-1,0),k];
        Vector3 curve=grid[s,Math.Min(k+1,lastCurve)]-grid[s,Math.Max(k-1,0)];
        if(lastCurve>=2 && k==0)curve=(grid[s,1]-grid[s,0])*2f-(grid[s,2]-grid[s,0])*.5f;
        if(lastCurve>=2 && k==lastCurve)curve=(grid[s,lastCurve]-grid[s,lastCurve-1])*2f-(grid[s,lastCurve]-grid[s,lastCurve-2])*.5f;
        return Vector3.Cross(span,curve).normalized;
    }
    void SmoothQuad(Vector3[,] grid,int s,int k,Device owner,int material,bool reverse,Vector3[,] reference=null,int offset=0)
    {
        var shading=reference??grid;
        Vector2 uv=mesh.vertices[122].uv;
        var a=new BigWingGeometry.Vertex(grid[s,k],ShadingNormal(shading,s,k+offset),uv);
        var b=new BigWingGeometry.Vertex(grid[s+1,k],ShadingNormal(shading,s+1,k+offset),uv);
        var c=new BigWingGeometry.Vertex(grid[s+1,k+1],ShadingNormal(shading,s+1,k+1+offset),uv);
        var d=new BigWingGeometry.Vertex(grid[s,k+1],ShadingNormal(shading,s,k+1+offset),uv);
        if(reverse)
        {
            a.n=-a.n;b.n=-b.n;c.n=-c.n;d.n=-d.n;
            Emit(new List<BigWingGeometry.Vertex>{a,c,b},material,owner);
            Emit(new List<BigWingGeometry.Vertex>{a,d,c},material,owner);
        }
        else
        {
            Emit(new List<BigWingGeometry.Vertex>{a,b,c},material,owner);
            Emit(new List<BigWingGeometry.Vertex>{a,c,d},material,owner);
        }
    }
    void SkinLoft(Vector3[,] outer,Vector3[,] inner,Device owner,Vector3[,] reference=null,int offset=0)
    {
        int stations=outer.GetLength(0),points=outer.GetLength(1);
        for(int s=0;s<stations-1;s++)for(int k=0;k<points-1;k++)
        {
            SmoothQuad(outer,s,k,owner,1,false,reference,offset);
            SmoothQuad(inner,s,k,owner,1,true,reference,offset);
        }
        for(int side=0;side<2;side++)
        {
            int s=side==0?0:stations-1;
            for(int k=0;k<points-1;k++)
                Quad(outer[s,k],outer[s,k+1],inner[s,k+1],inner[s,k],side==0?Vector3.back:Vector3.forward,owner);
        }
        for(int s=0;s<stations-1;s++)foreach(int k in new[]{0,points-1})
        {
            Vector3 tangent=outer[s+1,k]-outer[s,k];
            Vector3 normal=Vector3.Cross(tangent,inner[s,k]-outer[s,k])*(k==0?-1f:1f);
            Quad(outer[s,k],inner[s,k],inner[s+1,k],outer[s+1,k],normal,owner);
        }
    }

    internal void Pose(float flapAngle,float spoilers,Vector3 down)
    {
        foreach(int[] cap in leadingCaps) for(int i=cap[0];i<cap[1];i++)
        {
            mesh.positions[i]=mesh.vertices[i].p;
            mesh.normals[i]=mesh.vertices[i].n;
        }
        foreach(Device d in devices)
        {
            float fraction=d.spoiler?spoilers:d.leadingFlap?
                BigWingPreset.LeadingEdgeExtension(flapAngle):BigWingPreset.SlatExtension(flapAngle);
            float direction=down.y<0f?-1f:1f;
            float angle=(d.spoiler?55f:d.leadingFlap?-105f:12f)*fraction*direction;
            // Section-wise transforms preserve span position on swept wings.
            Quaternion rotation=Rotation(Vector3.forward,angle);
            Vector3 slope=Hinge(d,d.last)-Hinge(d,d.first);slope/=(d.last-d.first);
            Vector3 correction=slope-rotation*slope;
            Vector3 travel=d.spoiler?Vector3.zero:d.leadingFlap?Vector3.zero:
                (Vector3.right*.055f+new Vector3(down.x,down.y,0f).normalized*.018f)*fraction;
            foreach(int i in d.indices)
            {
                var v=mesh.vertices[i];Vector3 hinge=Hinge(d,v.p.z);
                mesh.positions[i]=hinge+travel+rotation*(v.p-hinge);
                Vector3 n=rotation*v.n;
                n.z-=n.x*correction.x+n.y*correction.y;
                mesh.normals[i]=n.normalized;
            }
        }
        // Stowed devices already meet exactly along the original shell. Internal
        // end caps would overlap there when only the spoilers switch this mesh on.
        int capGroup=0;
        foreach(int[] cap in leadingCaps)
        {
            bool stowed=(capGroup==3?BigWingPreset.LeadingEdgeExtension(flapAngle):BigWingPreset.SlatExtension(flapAngle))<=.0001f;
            capGroup++;
            if(!stowed) continue;
            for(int i=cap[0];i<cap[1];i+=3)
                mesh.positions[i+1]=mesh.positions[i+2]=mesh.positions[i];
        }
    }
    Vector3 Hinge(Device d,float z)
    {
        float x=d.spoiler||d.leadingFlap?d.Front(z):d.Cut(z);
        float u=(z-d.first)/(d.last-d.first);
        return new Vector3(x,Mathf.LerpUnclamped(d.hingeYFirst,d.hingeYLast,u),z);
    }
    static Quaternion Rotation(Vector3 axis,float degrees)
    {
        double a=degrees*Math.PI/360;float s=(float)Math.Sin(a);
        return new Quaternion(axis.x*s,axis.y*s,axis.z*s,(float)Math.Cos(a));
    }
}





