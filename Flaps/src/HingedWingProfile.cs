using System;
using UnityEngine;

internal sealed class HingedWingProfile
{
    internal readonly string prefab,id,label;
    internal readonly byte code;
    readonly int count,skin,edge;
    readonly Vector3 first;
    internal readonly Vector4 root,tip;
    HingedWingProfile(string prefab,string id,string label,byte code,int count,int skin,int edge,Vector3 first,Vector4 root,Vector4 tip)
    {this.prefab=prefab;this.id=id;this.label=label;this.code=code;this.count=count;this.skin=skin;this.edge=edge;this.first=first;this.root=root;this.tip=tip;}
    static readonly HingedWingProfile[] profiles={
        new HingedWingProfile("SmallRecreationalWing","small-recreational-wing","Small Recreational Wing",3,371,972,312,new Vector3(-.419032f,-.015807f,-.003767f),new Vector4(1.86f,-.462f,-.052f,.052f),new Vector4(.095f,-.345f,-.044f,.044f)),
        new HingedWingProfile("GliderWing","glider-wing","Glider Wing",4,593,1236,576,new Vector3(-.561424f,.328892f,-5.482132f),new Vector4(-.12f,-.30448f,-.057f,.058f),new Vector4(-3.18567f,-.38765f,.130f,.182f)),
        new HingedWingProfile("SmallJetWing","small-jet-wing","Small Jet Wing",5,453,840,654,new Vector3(2.19185f,-.43747f,2.355683f),new Vector4(2.20868f,.321f,-.38f,-.15f),new Vector4(.92919f,-.1234f,-.231f,-.005f)),
        new HingedWingProfile("ElectronWing","electron-wing","Electron Wing",6,353,486,840,new Vector3(-1.012295f,-.263458f,2.428339f),new Vector4(2.30834f-6*.009f,-.50852f-2*.003f,-.389f,-.13f),new Vector4(.12941f+3*.007f,-.35198f-2*.003f,-.130f,.062f)),
        new HingedWingProfile("DeltaWing02","delta-wing-02","Delta Wing 02",7,304,636,396,new Vector3(-1.292452f,.013262f,.100130f),new Vector4(-.01987f,-.77571f,-.1f,.098f),new Vector4(-.97666f,-1.07143f,-.081f,.078f)),
        new HingedWingProfile("Wing","wing","Wing",8,280,1098,0,new Vector3(-1.055110f,-.129772f,1.958387f),new Vector4(1.89267f-7*.009f,-.54226f-.003f,-.132f,.129f),new Vector4(.11975f+2*.007f,-.35656f,-.097f,.088f)),
    };
    internal static HingedWingProfile Match(Vector3[] positions,int skin,int edge){
        foreach(var profile in profiles)if(profile.count==positions.Length && profile.skin==skin && profile.edge==edge && (positions[0]-profile.first).sqrMagnitude<.000001f)return profile;
        return null;
    }
    internal static byte Code(string name){
        if(name.StartsWith("BigWing",StringComparison.Ordinal))return 1;
        if(name.StartsWith("StraightWing",StringComparison.Ordinal))return 2;
        foreach(var profile in profiles)if(name==profile.prefab || name.StartsWith(profile.prefab+"(",StringComparison.Ordinal))return profile.code;
        return 0;
    }
    internal static Vector3 Hinge(Vector4 range,bool inverted){return new Vector3(range.y,inverted?range.w:range.z,range.x);}
}
