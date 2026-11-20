using System;
using HarmonyLib;
using UnityEngine;

// The game's real control surface becomes a flaperon; no replacement mesh or hinge is invented.
internal sealed class NativeSurfaceFlaps
{
    readonly Wing wing;
    readonly WingVisual visual;
    readonly Transform surface;
    readonly Vector3 edgePoint;
    Quaternion nativePose,appliedPose;
    Vector3 baseEuler;
    float steering;
    bool ownsPose;
    internal NativeSurfaceFlaps(Wing wing,WingVisual visual)
    {
        this.wing=wing;this.visual=visual;surface=visual.controlSurface;nativePose=surface.localRotation;
        // Start order is unspecified; use the same transform source as WingVisual.Start.
        // Its private cached rotation may still be zero on a freshly cloned part.
        baseEuler=surface.localEulerAngles;
        steering=(float)AccessTools.Field(typeof(WingVisual),"angle").GetValue(visual);
        float farthest=0;Vector3 point=Vector3.zero;
        var parent=wing.GetComponentInParent<PlaneContainer>();Vector3 normal=parent?LiftNormal(wing,parent):wing.orientation.up;
        var zero=Quaternion.Euler(baseEuler.x,baseEuler.y,0);var plus=Quaternion.Euler(baseEuler.x,baseEuler.y,1);
        foreach(var filter in surface.GetComponentsInChildren<MeshFilter>(true)){
            var mesh=filter.sharedMesh;if(!mesh || !mesh.isReadable)continue;
            var matrix=surface.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            foreach(var vertex in mesh.vertices){
                Vector3 local=matrix.MultiplyPoint3x4(vertex);
                Vector3 lever=Vector3.Scale(surface.localScale,local),delta=plus*lever-zero*lever;
                if(surface.parent)delta=surface.parent.TransformVector(delta);
                float motion=Mathf.Abs(Vector3.Dot(delta,normal));
                if(motion>farthest){farthest=motion;point=local;}
            }
        }
        if(farthest<1e-8f)throw new ArgumentException("Native control surface has no readable moving edge");
        edgePoint=point;
    }
    internal static Vector3 LiftNormal(Wing wing,PlaneContainer plane)
    {
        Vector3 normal=wing.orientation.up.normalized;
        float alignment=Vector3.Dot(normal,plane.transform.up);
        // Horizontal/canted pairs both bias toward aircraft up; vertical fins keep their own normal.
        return Mathf.Abs(alignment)>.1f?normal*Mathf.Sign(alignment):normal;
    }
    Vector3 WorldPoint(Quaternion rotation)
    {
        Vector3 local=surface.localPosition+rotation*Vector3.Scale(surface.localScale,edgePoint);
        return surface.parent?surface.parent.TransformPoint(local):local;
    }
    internal float DeflectionSign(PlaneContainer plane)
    {
        var zero=Quaternion.Euler(baseEuler.x,baseEuler.y,0);
        var plus=Quaternion.Euler(baseEuler.x,baseEuler.y,1);
        float vertical=Vector3.Dot(WorldPoint(plus)-WorldPoint(zero),LiftNormal(wing,plane));
        if(Mathf.Abs(vertical)<1e-8f)throw new InvalidOperationException("Native hinge cannot deflect toward the wing underside");
        return vertical<0?1:-1;
    }
    internal void NativeUpdated(Vector3 euler,float angle,FlapMarker marker,PlaneContainer plane)
    {
        nativePose=surface.localRotation;ownsPose=false;baseEuler=euler;steering=angle;
        Apply(marker.CanOperate(plane) ? marker.CurrentAngle : 0f, plane);
    }
    internal void Apply(float angle,PlaneContainer plane)
    {
        if(!surface)return;
        if(angle<=0 || !plane){Restore();return;}
        appliedPose=Quaternion.Euler(baseEuler.x,baseEuler.y,steering+DeflectionSign(plane)*angle);
        surface.localRotation=appliedPose;ownsPose=true;
    }
    internal void Restore()
    {
        if(surface && ownsPose && Quaternion.Angle(surface.localRotation,appliedPose)<.01f)surface.localRotation=nativePose;
        ownsPose=false;
    }
    internal float EdgeDisplacement(PlaneContainer plane)=>Vector3.Dot(WorldPoint(surface.localRotation)-WorldPoint(nativePose),LiftNormal(wing,plane));

    internal void ApplyForces(PlaneContainer plane, float angle)
    {
        if (angle <= 0f) return;
        Vector3 point = wing.orientation.position;
        Vector3 velocity = wing.rb.GetPointVelocity(point);
        if (velocity.sqrMagnitude < .01f) return;
        Vector3 direction = velocity.normalized;
        float strength = velocity.sqrMagnitude * wing.GetWingArea() * wing.liftForce *
            plane.GravityMultiplier * plane.liftMultiplier * (angle / 30f);
        float forward=Mathf.Clamp01(Vector3.Dot(direction,wing.orientation.forward));
        float weight=wing.GetWingArea()*Mathf.Max(0f,wing.liftForce);
        float total=WingLoadTotals.ForBody(plane,wing.rb,Time.fixedTimeAsDouble);
        float budget=total>0f?wing.rb.mass*Mathf.Abs(plane.RealGravity)*weight/total:0f;
        float horizontal=Mathf.Abs(Vector3.Dot(wing.orientation.up,plane.transform.up));
        float lift=Mathf.Min(.25f*strength*horizontal,budget*.25f)*forward*forward;
        float drag=Mathf.Min(.18f*strength,budget*.04f)*forward*forward;
        wing.rb.AddForce(Vector3.ProjectOnPlane(plane.transform.up,direction).normalized*lift-direction*drag);
    }
}

[HarmonyPatch(typeof(WingVisual),"Update")]
internal static class ComposeNativeSurface
{
    static void Postfix(WingVisual __instance,Vector3 ___localRotation,float ___angle)
    {
        var marker=__instance.GetComponentInParent<FlapMarker>();
        if (marker && marker.NativeSurface != null)
            marker.NativeSurface.NativeUpdated(___localRotation, ___angle, marker, FlapsPlugin.activePlane);
    }
}
