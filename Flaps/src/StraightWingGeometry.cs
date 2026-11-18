using System;
using System.Collections.Generic;
using UnityEngine;

// Cuts the installed StraightWing mesh; each cut creates two matching closed
// surfaces. The native aileron is a separate child and is never modified.
internal sealed class StraightWingGeometry
{
    internal struct Vertex
    {
        internal Vector3 p,n; internal Vector2 uv;
        internal Vertex(Vector3 p,Vector3 n,Vector2 uv){this.p=p;this.n=n;this.uv=uv;}
        internal static Vertex Mix(Vertex a,Vertex b,float t){return new Vertex(Vector3.Lerp(a.p,b.p,t),Vector3.Lerp(a.n,b.n,t).normalized,Vector2.Lerp(a.uv,b.uv,t));}
    }
    sealed class Face { internal Vertex a,b,c;internal int material;internal bool cap;internal Face(Vertex a,Vertex b,Vertex c,int m,bool cap=false){this.a=a;this.b=b;this.c=c;material=m;this.cap=cap;} }
    internal readonly List<Vertex> vertices=new List<Vertex>();
    internal readonly List<int>[] triangles={new List<int>(),new List<int>()};
    internal readonly List<int> moving=new List<int>();
    internal readonly Vector3[] positions,normals;
    internal readonly float FlapAreaFraction;
    internal const float HingeGap=.006f, EndGap=.004f;
    readonly StraightWingFlapMotion motion,reverseMotion;
    internal static readonly Vector3 HingeRoot=new Vector3(-.85977f,-.0722f,2.85f);
    internal static readonly Vector3 HingeTip=new Vector3(-.50146f,.193f,.18f);
    internal static readonly Vector3 ReverseHingeRoot=new Vector3(-.85977f,.060f,2.85f);
    internal static readonly Vector3 ReverseHingeTip=new Vector3(-.50146f,.294f,.18f);
    internal readonly HingedWingProfile profile;
    internal readonly float slope,offset,tipZ,rootZ;
    internal Vector3 Root(bool inverted){return profile!=null?HingedWingProfile.Hinge(profile.root,inverted):(inverted?ReverseHingeRoot:HingeRoot);}
    internal Vector3 Tip(bool inverted){return profile!=null?HingedWingProfile.Hinge(profile.tip,inverted):(inverted?ReverseHingeTip:HingeTip);}
    internal StraightWingGeometry(StraightWingGeometry source)
    {
        profile=source.profile;slope=source.slope;offset=source.offset;tipZ=source.tipZ;rootZ=source.rootZ;
        vertices=source.vertices;triangles=source.triangles;moving=source.moving;FlapAreaFraction=source.FlapAreaFraction;
        motion=new StraightWingFlapMotion(source.motion);reverseMotion=new StraightWingFlapMotion(source.reverseMotion);
        positions=motion.positions;normals=motion.normals;
    }

    internal StraightWingGeometry(Vector3[] p,Vector3[] n,Vector2[] uv,int[] skin,int[] edge)
    {
        profile=HingedWingProfile.Match(p,skin.Length,edge.Length);
        if(n.Length!=p.Length || uv.Length!=p.Length)throw new ArgumentException("Missing wing vertex attributes");
        if(profile==null && (p.Length!=332 || skin.Length!=702 || edge.Length!=582 ||
            (p[0]-new Vector3(.7865f,-.08661755f,2.9511f)).sqrMagnitude>.000001f))
            throw new ArgumentException("Unrecognized Straight Wing mesh; leaving the stock wing intact");
        var current=new List<Face>();
        for(int m=0;m<2;m++){int[] ids=m==0?skin:edge;for(int i=0;i<ids.Length;i+=3)current.Add(new Face(new Vertex(p[ids[i]],n[ids[i]],uv[ids[i]]),new Vertex(p[ids[i+1]],n[ids[i+1]],uv[ids[i+1]]),new Vertex(p[ids[i+2]],n[ids[i+2]],uv[ids[i+2]]),m));}
        var stationary=new List<Face>();
        // The hinge follows the stock taper. The tip stops just inboard of the
        // native aileron recess; the root retains a small stationary end strip.
        tipZ=Tip(false).z;rootZ=Root(false).z;
        slope=profile!=null?(Tip(false).x-Root(false).x)/(rootZ-tipZ):.1341985f;
        offset=profile!=null?-Tip(false).x-slope*tipZ:.4773043f;
        Vector3[] planes={Vector3.back,Vector3.forward,new Vector3(1f,0f,slope)};
        float[] offsets={tipZ,-rootZ,offset};
        foreach(int i in profile?.code==5?new[]{0,1,2}:profile!=null?new[]{2,0,1}:new[]{0,1,2}){
            List<Face> inside,outside;Split(current,planes[i],offsets[i],out inside,out outside);
            foreach(Face f in outside)if(!f.cap)stationary.Add(f);current=inside;
        }
        foreach(Face f in current)if(f.cap){Vertex a=f.a,b=f.b,c=f.c;a.n=-a.n;b.n=-b.n;c.n=-c.n;stationary.Add(new Face(a,c,b,f.material,true));}
        foreach(Face face in Conform(stationary))Add(face,false);
        // Affinely inset the already closed flap. Recutting thin caps can
        // introduce slivers; this preserves topology and the native UVs.
        float depth=0f;
        foreach(Face face in current)foreach(Vertex v in new[]{face.a,face.b,face.c})
            depth=Mathf.Max(depth,-(v.p.x+slope*v.p.z+offset));
        float normalGap=HingeGap*planes[2].magnitude;
        float chordScale=1f-normalGap/depth,spanScale=1f-2f*EndGap/(rootZ-tipZ);
        if(chordScale<=0f)throw new ArgumentException("Straight Wing flap is too shallow for its seam");
        float stockArea=0f,flapArea=0f;
        for(int i=0;i<skin.Length;i+=3)stockArea+=Math.Abs(Vector3.Cross(p[skin[i+1]]-p[skin[i]],p[skin[i+2]]-p[skin[i]]).y);
        foreach(Face face in current){
            var inset=new Face(Inset(face.a,chordScale,spanScale,normalGap),Inset(face.b,chordScale,spanScale,normalGap),Inset(face.c,chordScale,spanScale,normalGap),face.material,face.cap);
            Add(inset,true);
            if(inset.material==0)flapArea+=Math.Abs(Vector3.Cross(inset.b.p-inset.a.p,inset.c.p-inset.a.p).y);
        }
        FlapAreaFraction=Mathf.Clamp01(flapArea/stockArea);
        var rp=new Vector3[vertices.Count];var rn=new Vector3[vertices.Count];for(int i=0;i<rp.Length;i++){rp[i]=vertices[i].p;rn[i]=vertices[i].n;}
        motion=new StraightWingFlapMotion(rp,rn,moving.ToArray(),Root(false),Tip(false),Vector3.left);
        reverseMotion=new StraightWingFlapMotion(rp,rn,moving.ToArray(),Root(true),Tip(true),Vector3.left);
        positions=motion.positions;normals=motion.normals;
    }
    internal void Pose(float angle,Vector3 down){
        if(Vector3.Dot(down,Vector3.up)>0f){reverseMotion.Pose(angle,down);Array.Copy(reverseMotion.positions,positions,positions.Length);Array.Copy(reverseMotion.normals,normals,normals.Length);}
        else motion.Pose(angle,down);
    }
    Vertex Inset(Vertex v,float chordScale,float spanScale,float gap){
        float distance=v.p.x+slope*v.p.z+offset;
        v.p.z=(rootZ+tipZ)*.5f+(v.p.z-(rootZ+tipZ)*.5f)*spanScale;
        v.p.x=distance*chordScale-gap-slope*v.p.z-offset;
        float shear=slope*(chordScale-spanScale);
        v.n=new Vector3(v.n.x/chordScale,v.n.y,v.n.z/spanScale-shear*v.n.x/(chordScale*spanScale)).normalized;
        return v;
    }
    static List<Face> Conform(List<Face> input)
    {
        // Split the stationary end-strip edges at the final hinge intersection,
        // so matching cap and skin boundaries share the same subdivisions.
        var unique=new Dictionary<string,Vector3>();foreach(Face f in input)foreach(Vertex v in new[]{f.a,f.b,f.c})unique[Key(v.p)]=v.p;
        var result=new List<Face>();
        foreach(Face f in input){var polygon=new List<Vertex>();var vs=new[]{f.a,f.b,f.c};
            for(int i=0;i<3;i++){
                Vertex a=vs[i],b=vs[(i+1)%3];polygon.Add(a);Vector3 delta=b.p-a.p;float length=delta.sqrMagnitude;if(length<1e-12f)continue;
                var points=new List<KeyValuePair<float,Vertex>>();
                foreach(Vector3 q in unique.Values){float t=Vector3.Dot(q-a.p,delta)/length;if(t<=.00001f||t>=.99999f)continue;if((a.p+delta*t-q).sqrMagnitude>1e-12f)continue;Vertex v=Vertex.Mix(a,b,t);v.p=q;points.Add(new KeyValuePair<float,Vertex>(t,v));}
                points.Sort((x,y)=>x.Key.CompareTo(y.Key));foreach(var pair in points)polygon.Add(pair.Value);
            }
            if(polygon.Count==3){result.Add(f);continue;}
            Vector3 p=Vector3.zero,n=Vector3.zero;Vector2 uv=Vector2.zero;foreach(Vertex v in polygon){p+=v.p;n+=v.n;uv+=v.uv;}var center=new Vertex(p/polygon.Count,n.normalized,uv/polygon.Count);
            for(int i=0;i<polygon.Count;i++)result.Add(new Face(center,polygon[i],polygon[(i+1)%polygon.Count],f.material,f.cap));
        }return result;
    }
    void Add(Face f,bool flap){if(Vector3.Cross(f.b.p-f.a.p,f.c.p-f.a.p).sqrMagnitude<1e-16f)return;foreach(Vertex v in new[]{f.a,f.b,f.c}){int id=vertices.Count;vertices.Add(v);triangles[f.material].Add(id);if(flap)moving.Add(id);}}
    static List<Vertex> Clip(Vertex[] input,Vector3 normal,float offset,bool inside)
    {
        var output=new List<Vertex>();
        for(int i=0;i<input.Length;i++){
            Vertex a=input[i],b=input[(i+1)%input.Length];float da=Vector3.Dot(normal,a.p)+offset,db=Vector3.Dot(normal,b.p)+offset;
            bool ia=inside?da<=0f:da>=0f,ib=inside?db<=0f:db>=0f;
            if(ia)output.Add(a);if(ia!=ib)output.Add(Vertex.Mix(a,b,da/(da-db)));
        }return output;
    }
    static void Fan(List<Vertex> poly,int material,List<Face> faces,bool cap){for(int i=1;i+1<poly.Count;i++)faces.Add(new Face(poly[0],poly[i],poly[i+1],material,cap));}
    static string Key(Vector3 p){return Math.Round(p.x*100000)+","+Math.Round(p.y*100000)+","+Math.Round(p.z*100000);}
    static void Split(List<Face> input,Vector3 normal,float offset,out List<Face> inside,out List<Face> outside)
    {
        inside=new List<Face>();outside=new List<Face>();
        var points=new Dictionary<string,Vertex>();var links=new Dictionary<string,List<string>>();
        foreach(Face f in input){
            var vs=new[]{f.a,f.b,f.c};Fan(Clip(vs,normal,offset,true),f.material,inside,f.cap);Fan(Clip(vs,normal,offset,false),f.material,outside,f.cap);
            var cut=new List<Vertex>();
            for(int i=0;i<3;i++){Vertex a=vs[i],b=vs[(i+1)%3];float da=Vector3.Dot(normal,a.p)+offset,db=Vector3.Dot(normal,b.p)+offset;if((da<0f&&db>0f)||(da>0f&&db<0f))cut.Add(Vertex.Mix(a,b,da/(da-db)));}
            if(cut.Count!=2)continue;string ka=Key(cut[0].p),kb=Key(cut[1].p);if(ka==kb)continue;
            points[ka]=cut[0];points[kb]=cut[1];
            if(!links.ContainsKey(ka))links[ka]=new List<string>();if(!links.ContainsKey(kb))links[kb]=new List<string>();
            if(!links[ka].Contains(kb))links[ka].Add(kb);if(!links[kb].Contains(ka))links[kb].Add(ka);
        }
        var visited=new HashSet<string>();
        foreach(string start in links.Keys){
            if(visited.Contains(start))continue;var ring=new List<Vertex>();string previous=null,key=start;
            do{if(links[key].Count!=2)throw new ArgumentException("Straight Wing cut does not form a closed contour");visited.Add(key);ring.Add(points[key]);var next=links[key][0]==previous?links[key][1]:links[key][0];previous=key;key=next;if(ring.Count>links.Count)throw new ArgumentException("Invalid Straight Wing contour");}while(key!=start);
            Cap(ring,normal.normalized,inside);Cap(ring,-normal.normalized,outside);
        }
    }
    static void Cap(List<Vertex> ring,Vector3 outward,List<Face> output)
    {
        var polygon=new List<Vertex>(ring);Vector3 area=Vector3.zero;
        for(int i=0;i<polygon.Count;i++)area+=Vector3.Cross(polygon[i].p,polygon[(i+1)%polygon.Count].p);
        if(Vector3.Dot(area,outward)<0f)polygon.Reverse();
        for(int i=0;i<polygon.Count;i++){Vertex v=polygon[i];v.n=outward;polygon[i]=v;}
        // Preserve every boundary subdivision. A checked interior fan avoids
        // tiny overlapping ears on the almost-collinear lower airfoil skin.
        Vector3 center=Vector3.zero;Vector2 texture=Vector2.zero;
        foreach(Vertex v in polygon){center+=v.p;texture+=v.uv;}
        center/=polygon.Count;texture/=polygon.Count;
        bool inKernel=true;
        for(int i=0;i<polygon.Count;i++)if(Vector3.Dot(Vector3.Cross(polygon[(i+1)%polygon.Count].p-polygon[i].p,center-polygon[i].p),outward)<-1e-8f)inKernel=false;
        if(inKernel){var middle=new Vertex(center,outward,texture);for(int i=0;i<polygon.Count;i++)output.Add(new Face(middle,polygon[i],polygon[(i+1)%polygon.Count],1,true));return;}
        int guard=polygon.Count*polygon.Count;
        while(polygon.Count>3 && guard-->0){bool clipped=false;
            for(int i=0;i<polygon.Count;i++){
                int prev=(i+polygon.Count-1)%polygon.Count,next=(i+1)%polygon.Count;
                Vector3 a=polygon[prev].p,b=polygon[i].p,c=polygon[next].p;
                if(Vector3.Dot(Vector3.Cross(b-a,c-b),outward)<=1e-13f)continue;
                bool contains=false;
                for(int j=0;j<polygon.Count;j++){if(j==prev||j==i||j==next)continue;Vector3 q=polygon[j].p;
                    if(Vector3.Dot(Vector3.Cross(b-a,q-a),outward)>=-1e-13f && Vector3.Dot(Vector3.Cross(c-b,q-b),outward)>=-1e-13f && Vector3.Dot(Vector3.Cross(a-c,q-c),outward)>=-1e-13f){contains=true;break;}}
                if(contains)continue;output.Add(new Face(polygon[prev],polygon[i],polygon[next],1,true));polygon.RemoveAt(i);clipped=true;break;
            }if(!clipped){string detail="";foreach(Vertex v in polygon)detail+=" "+v.p.ToString("F7");throw new ArgumentException("Cannot triangulate Straight Wing cut cap normal="+outward+detail);}
        }
        if(polygon.Count==3)output.Add(new Face(polygon[0],polygon[1],polygon[2],1,true));
    }
}
