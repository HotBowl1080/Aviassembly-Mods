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
    // Short facets keep the game's low-poly style; shared normals round the lighting.
    static readonly double[] NoseAngles={0.0,30.0,60.0,90.0,120.0,150.0,180.0};
    const int SlatFaces=3;
    const float KruegerChord=.12f;
    sealed class Device
    {
        internal float first,last,cutFirst,cutLast,frontFirst,frontLast,lowerFirst,lowerLast;
        internal bool spoiler,leadingFlap,fixedNose,rootNose;
        internal float hingeYFirst,hingeYLast;
        internal readonly List<int> indices=new List<int>();
        internal readonly Dictionary<float,Vector3> hingeSections=new Dictionary<float,Vector3>();
        internal readonly Dictionary<float,Vector3> slopeSections=new Dictionary<float,Vector3>();
        internal readonly Dictionary<int,Vector3> openPositions=new Dictionary<int,Vector3>();
        internal readonly Dictionary<int,Vector3> openNormals=new Dictionary<int,Vector3>();
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
        AddLeading(-3.65f,-2.525f,false);
        AddLeading(-2.517f,-1.16f,false);
        AddLeading(-1.152f,.20f,false);
        AddLeading(.22f,1.46f,true);
        AddSpoiler(-1.66f,-.43f);
        AddSpoiler(.20f,1.13f);
        // The stock tip starts bending upward at about -3.70. Never replace it
        // with a straight spanwise nose loft: that slices through the winglet.
        AddFixedNose(-3.70f,-3.65f);
        AddFixedNose(-2.525f,-2.517f);
        AddFixedNose(-1.16f,-1.152f);
        AddFixedNose(.20f,1.7210f);
        fixedNoses[fixedNoses.Count-1].rootNose=true;
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
        if(plane==1)return (d.rootNose?1.73f:d.last)-p.z;
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
        n=Unit(n);
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
        const int stations=7,points=13;
        var outer=new Vector3[stations,points];
        var inner=new Vector3[stations,points];
        float[] hingeX=new float[stations],hingeY=new float[stations];
        float rearA,frontA,rearB,frontB;
        Section(d.first,out rearA,out frontA);Section(d.last,out rearB,out frontB);
        d.frontFirst=frontA-.055f;d.frontLast=frontB-.055f;
        for(int s=0;s<stations;s++)
        {
            float z=Mathf.Lerp(d.first,d.last,s/(float)(stations-1));
            float x=d.Front(z);
            float y=StowedBottom(x,z)+.014f;
            hingeX[s]=x;hingeY[s]=y;
            for(int k=0;k<points;k++)
            {
                float t=k/(float)(points-1);
                // Closed underside door. Its open shape is a short drooped
                // panel with a returning bullnose, not another slat strip.
                outer[s,k]=new Vector3(x-KruegerChord*t,StowedBottom(x-KruegerChord*t,z)+.014f,z);
                inner[s,k]=outer[s,k]+Vector3.up*.003f;
            }
        }
        d.frontFirst=hingeX[0];d.frontLast=hingeX[stations-1];
        d.hingeYFirst=hingeY[0];d.hingeYLast=hingeY[stations-1];
        SkinLoft(outer,inner,d);
        // Two compact hinge webs, folded inside the wing when retracted.
        foreach(float span in new[]{.24f,.76f})
        {
            float z=Mathf.Lerp(d.first,d.last,span);
            var web=new Vector3[2,3];
            for(int side=0;side<2;side++)
            {
                float zs=z+(side==0?-.004f:.004f);
                float xs=d.Front(zs);
                web[side,0]=new Vector3(xs,StowedBottom(xs,zs)+.018f,zs);
                web[side,1]=new Vector3(xs-KruegerChord*.57f,StowedBottom(xs-KruegerChord*.57f,zs)+.018f,zs);
                web[side,2]=new Vector3(xs-KruegerChord*.24f,StowedBottom(xs-KruegerChord*.24f,zs)+.036f,zs);
                Face(web[side,0],web[side,1],web[side,2],side==0?Vector3.back:Vector3.forward,d);
            }
            for(int k=0;k<3;k++)
            {
                int next=(k+1)%3;
                Vector3 normal=Vector3.Cross(web[1,k]-web[0,k],web[0,next]-web[0,k]);
                Quad(web[0,k],web[1,k],web[1,next],web[0,next],normal,d);
            }
        }
        foreach(int id in d.indices)
        {
            float z=mesh.vertices[id].p.z;
            if(d.hingeSections.ContainsKey(z))continue;
            float z0=Math.Max(d.first,z-.0005f),z1=Math.Min(d.last,z+.0005f);
            d.slopeSections.Add(z,(Hinge(d,z1)-Hinge(d,z0))/(z1-z0));
            d.hingeSections.Add(z,Hinge(d,z));
        }
        // Cache the deployed profile and its inverse-transpose normals once.
        // Deploying then only blends cached geometry during the folding motion.
        const float step=.0001f;
        foreach(int id in d.indices)
        {
            var v=mesh.vertices[id];
            Vector3 point=OpenKruegerPoint(d,v.p);
            Vector3 dx=(OpenKruegerPoint(d,v.p+Vector3.right*step)-OpenKruegerPoint(d,v.p-Vector3.right*step))/(2f*step);
            Vector3 dy=(OpenKruegerPoint(d,v.p+Vector3.up*step)-OpenKruegerPoint(d,v.p-Vector3.up*step))/(2f*step);
            Vector3 dz=(OpenKruegerPoint(d,v.p+Vector3.forward*step)-OpenKruegerPoint(d,v.p-Vector3.forward*step))/(2f*step);
            Vector3 normal=Vector3.Cross(dy,dz)*v.n.x+Vector3.Cross(dz,dx)*v.n.y+Vector3.Cross(dx,dy)*v.n.z;
            if(Vector3.Dot(dx,Vector3.Cross(dy,dz))<0f)normal=-normal;
            d.openPositions.Add(id,point);d.openNormals.Add(id,normal.normalized);
        }
    }
    // Forward/down panel followed by a compact curled lip. The last part
    // returns toward the wing instead of projecting as a flat shelf.
    static Vector2 KruegerProfile(float t,out Vector2 tangent)
    {
        if(t<=.60f)
        {
            tangent=new Vector2(.057f,-.063f)/.60f;
            return tangent*t;
        }
        float angle=(-48f+195f*(t-.60f)/.40f)*(float)Math.PI/180f;
        float start=-48f*(float)Math.PI/180f,radius=.022f;
        tangent=new Vector2((float)Math.Cos(angle),(float)Math.Sin(angle))*(radius*195f/.40f*(float)Math.PI/180f);
        return new Vector2(.057f+radius*((float)Math.Sin(angle)-(float)Math.Sin(start)),
            -.063f+radius*((float)Math.Cos(start)-(float)Math.Cos(angle)));
    }
    Vector3 OpenKruegerPoint(Device d,Vector3 p)
    {
        Vector3 hinge=Hinge(d,p.z);
        float t=(hinge.x-p.x)/KruegerChord;
        Vector2 tangent,profile=KruegerProfile(t,out tangent);
        Vector2 normal=new Vector2(-tangent.y,tangent.x).normalized;
        float thickness=p.y-(StowedBottom(p.x,p.z)+.014f);
        return hinge+new Vector3(profile.x-normal.x*thickness,profile.y-normal.y*thickness,0f);
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
        double[] angles=NoseAngles;
        float result=float.PositiveInfinity;
        for(int i=0;i<angles.Length-1;i++)
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

    List<float> LeadingStations(Device d)
    {
        var candidates=new List<float>{d.first,d.last};
        for(int s=1;s<4;s++)candidates.Add(Mathf.Lerp(d.first,d.last,s/4f));
        // Retain every stock skin break along the cut. Uniform stations miss
        // the root's lower-surface bends and leave openings between the loft
        // and the retained wing. Include cut/triangle-edge crossings as well.
        for(int t=0;t<surface.Length;t+=3)for(int e=0;e<3;e++)
        {
            Vector3 a=shell[surface[t+e]],b=shell[surface[t+(e+1)%3]];
            if(a.z>d.first && a.z<d.last)candidates.Add(a.z);
            float da=a.x-d.Cut(a.z),db=b.x-d.Cut(b.z);
            if((da>0f)==(db>0f)||Math.Abs(da-db)<1e-8f)continue;
            float z=Mathf.LerpUnclamped(a.z,b.z,da/(da-db));
            if(z>d.first && z<d.last)candidates.Add(z);
        }
        candidates.Sort();var stations=new List<float>();
        foreach(float z in candidates)
            if(stations.Count==0||z-stations[stations.Count-1]>.000001f)stations.Add(z);
        if(stations[stations.Count-1]!=d.last)stations[stations.Count-1]=d.last;
        return stations;
    }
    Vector3 LeadingPoint(Device d,float z,double theta,float inset)
    {
        float x=d.Cut(z),rear,front;Section(z,out rear,out front);
        float top=Surface(x,z,true),bottom=Surface(x,z,false);
        return new Vector3(x+Math.Max(front-x-inset,.008f)*(float)Math.Sin(theta),
            (top+bottom)*.5f+(top-bottom)*.5f*(float)Math.Cos(theta),z);
    }
    Vector3 RootEndPoint(Device d,Vector3 point,double theta,float inset)
    {
        // The stock root cap is slightly tilted. Continue the last linear
        // section onto that plane, including its actual top/bottom cut edges.
        Vector3 normal=Vector3.Cross(shell[118]-shell[116],shell[119]-shell[116]);
        Vector3 previous=LeadingPoint(d,d.last-.001f,theta,inset);
        Vector3 along=(point-previous)/.001f;
        return point+along*(Vector3.Dot(normal,shell[116]-point)/Vector3.Dot(normal,along));
    }
    Vector3 OnRootPlane(Vector3 point)
    {
        Vector3 normal=Vector3.Cross(shell[118]-shell[116],shell[119]-shell[116]);
        point.z+=(Vector3.Dot(normal,shell[116]-point)/normal.z);
        return point;
    }

    void CloseLeadingSkin(Device d)
    {
        d.indices.Clear();
        var levels=LeadingStations(d);int stations=levels.Count;
        double[] angles=NoseAngles;
        var outer=new Vector3[stations,SlatFaces+1];
        var inner=new Vector3[stations,SlatFaces+1];
        var core=new Vector3[stations,angles.Length];
        var shadingSkin=new Vector3[stations,angles.Length];
        var fixedSkin=new Vector3[stations,angles.Length-SlatFaces];
        var fixedInner=new Vector3[stations,angles.Length-SlatFaces];
        for(int s=0;s<stations;s++)
        {
            float z=levels[s];
            float x=d.Cut(z),rear,front;Section(z,out rear,out front);
            float top=Surface(x,z,true),bottom=Surface(x,z,false);
            float cy=(top+bottom)*.5f,height=(top-bottom)*.5f,depth=front-x;
            for(int k=0;k<angles.Length;k++)
            {
                double theta=angles[k]*Math.PI/180.0;
                float sin=(float)Math.Sin(theta),cos=(float)Math.Cos(theta);
                Vector3 normal=new Vector3(sin/Math.Max(depth,.001f),cos/Math.Max(height,.001f),0f).normalized;
                Vector3 skinPoint=new Vector3(x+depth*sin,cy+height*cos,z);
                Vector3 corePoint=new Vector3(x+Math.Max(depth-.012f,.008f)*sin,cy+height*cos,z);
                if(d.rootNose && s==stations-1)
                {
                    skinPoint=RootEndPoint(d,skinPoint,theta,0f);
                    corePoint=RootEndPoint(d,corePoint,theta,.012f);
                }
                Vector3 innerPoint=skinPoint-normal*.003f;
                if(d.rootNose && s==stations-1)innerPoint=OnRootPlane(innerPoint);
                shadingSkin[s,k]=skinPoint;
                if(k<=SlatFaces)
                {
                    outer[s,k]=skinPoint;
                    inner[s,k]=innerPoint;
                }
                if(k>=SlatFaces)
                {
                    fixedSkin[s,k-SlatFaces]=skinPoint;
                    fixedInner[s,k-SlatFaces]=innerPoint;
                }
                core[s,k]=corePoint;
            }
        }
        // The upper nose and forward wrap move together; the underside and
        // closed inner nose remain, so the deployed slot cannot expose a hole.
        SkinLoft(outer,inner,d.fixedNose?null:d,shadingSkin,0);
        SkinLoft(fixedSkin,fixedInner,null,shadingSkin,SlatFaces);
        for(int s=0;s<stations-1;s++)for(int k=0;k<angles.Length-1;k++)
        {
            Vector3 normal=Vector3.Cross(core[s+1,k]-core[s,k],core[s,k+1]-core[s,k]);
            SmoothQuad(core,s,k,null,0,false);
        }
        for(int side=0;side<2;side++)
        {
            int s=side==0?0:stations-1;
            // Fixed collars need a solid outer cap, not only the inset core.
            // That closes the annular opening at the wing root and slat ends.
            var cap=d.fixedNose?shadingSkin:core;
            Vector3 center=(cap[s,0]+cap[s,angles.Length-1])*.5f;
            Vector3 outward=side==0?Vector3.back:Vector3.forward;
            if(d.rootNose && side==1)outward=Vector3.Cross(shell[118]-shell[116],shell[119]-shell[116]);
            if(outward.z<0f && side==1)outward=-outward;
            for(int k=0;k<angles.Length-1;k++)
                Face(center,cap[s,k],cap[s,k+1],outward,null,d.fixedNose?1:0);
        }
        for(int s=0;s<stations-1;s++)
            Quad(core[s,0],core[s,angles.Length-1],core[s+1,angles.Length-1],core[s+1,0],Vector3.left,null,0);
    }

    Vector3 ShadingNormal(Vector3[,] grid,int s,int k)
    {
        int lastSpan=grid.GetLength(0)-1,lastCurve=grid.GetLength(1)-1;
        Vector3 span=grid[Math.Min(s+1,lastSpan),k]-grid[Math.Max(s-1,0),k];
        Vector3 curve=grid[s,Math.Min(k+1,lastCurve)]-grid[s,Math.Max(k-1,0)];
        if(lastCurve>=2 && k==0)curve=(grid[s,1]-grid[s,0])*2f-(grid[s,2]-grid[s,0])*.5f;
        if(lastCurve>=2 && k==lastCurve)curve=(grid[s,lastCurve]-grid[s,lastCurve-1])*2f-(grid[s,lastCurve]-grid[s,lastCurve-2])*.5f;
        // Some stock seams are only micrometres apart. Normalize the tangent
        // directions before crossing so Unity's small-vector cutoff cannot
        // turn their perfectly valid shading normals into zero vectors.
        return Unit(Vector3.Cross(Unit(span),Unit(curve)));
    }
    static Vector3 Unit(Vector3 value)
    {
        float length=(float)Math.Sqrt((double)value.x*value.x+(double)value.y*value.y+(double)value.z*value.z);
        return length>1e-20f?value/length:Vector3.up;
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
            float angle=(d.spoiler?55f:d.leadingFlap?-110f:12f)*fraction*direction;
            // Section-wise transforms preserve span position on swept wings.
            Quaternion rotation=Rotation(Vector3.forward,angle);
            Vector3 slope=Hinge(d,d.last)-Hinge(d,d.first);slope/=(d.last-d.first);
            Vector3 correction=slope-rotation*slope;
            Vector3 travel=d.spoiler?Vector3.zero:d.leadingFlap?Vector3.zero:
                (Vector3.right*.055f+new Vector3(down.x,down.y,0f).normalized*.018f)*fraction;
            foreach(int i in d.indices)
            {
                var v=mesh.vertices[i];Vector3 hinge=Hinge(d,v.p.z);
                if(d.leadingFlap)
                {
                    slope=d.slopeSections[v.p.z];
                    correction=slope-rotation*slope;
                }
                Vector3 local=v.p-hinge,n=rotation*v.n;
                mesh.positions[i]=hinge+travel+rotation*local;
                n.z-=n.x*correction.x+n.y*correction.y;
                n=n.normalized;
                if(d.leadingFlap)
                {
                    Vector3 target=d.openPositions[i],targetNormal=d.openNormals[i];
                    if(direction>0f){target.y=2f*hinge.y-target.y;targetNormal.y=-targetNormal.y;}
                    Quaternion fullyOpen=Rotation(Vector3.forward,-110f*direction);
                    float roll=fraction*fraction*(3f-2f*fraction);
                    mesh.positions[i]+=(target-(hinge+fullyOpen*local))*roll;
                    n=Vector3.LerpUnclamped(n,targetNormal,roll).normalized;
                }
                mesh.normals[i]=n;
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
        Vector3 cached;
        if(d.leadingFlap && d.hingeSections.TryGetValue(z,out cached))return cached;
        float x=d.spoiler||d.leadingFlap?d.Front(z):d.Cut(z);
        float u=(z-d.first)/(d.last-d.first);
        float y=d.leadingFlap?StowedBottom(x,z)+.014f:Mathf.LerpUnclamped(d.hingeYFirst,d.hingeYLast,u);
        return new Vector3(x,y,z);
    }
    static Quaternion Rotation(Vector3 axis,float degrees)
    {
        double a=degrees*Math.PI/360;float s=(float)Math.Sin(a);
        return new Quaternion(axis.x*s,axis.y*s,axis.z*s,(float)Math.Cos(a));
    }
}





