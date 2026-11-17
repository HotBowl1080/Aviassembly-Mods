using System;
using UnityEngine;

// Straight Wing increments use the native coefficient units and animated
// deployment. They do not change the stock wing, aileron or Big Wing model.
internal static class StraightWingAerodynamics
{
    internal struct Forces { internal Vector3 lift,drag; }
    internal static Forces Evaluate(Vector3 velocity,Vector3 forward,Vector3 span,
        float mirrorSign,float angle,float flapAreaFraction,float wingArea,
        float liftCoefficient,float dragCoefficient,float gravityMultiplier,
        float liftMultiplier,float mass,float gravity,float supportShare)
    {
        var result=new Forces();
        float speed=velocity.magnitude;
        if(speed<.1f || angle<=0f || wingArea<=0f || mass<=0f)return result;
        Vector3 flow=velocity/speed;
        float alignment=Mathf.Clamp01(Vector3.Dot(flow,forward.normalized));
        // Fade camber benefit in sideways, vertical or reverse flow.
        float coverage=Mathf.Clamp01(flapAreaFraction)*alignment*alignment;
        float radians=Mathf.Clamp(angle,0f,30f)*(float)(Math.PI/180.0);
        float pressure=speed*speed*wingArea;
        float lift=pressure*Mathf.Max(0f,liftCoefficient)*Mathf.Max(0f,gravityMultiplier)*
            Mathf.Max(0f,liftMultiplier)*coverage*.5f*(float)Math.Sin(2f*radians);
        // Native Wing.ApplyDrag multiplies the squared-speed magnitude by
        // velocity again. Use its drag coefficient, not the lift coefficient.
        float sine=(float)Math.Sin(radians);
        float drag=pressure*speed*Mathf.Max(0f,dragCoefficient)*coverage*sine*sine;
        // Allocate conservative whole-aircraft limits across ALL supporting
        // wings so adding more fitted parts cannot multiply the force budget.
        float weight=mass*Math.Abs(gravity)*Mathf.Clamp01(supportShare);
        lift=Mathf.Min(lift,weight*.25f);
        drag=Mathf.Min(drag,weight*.04f);
        result.lift=Vector3.Cross(flow,span.normalized)*(mirrorSign<0f?-1f:1f)*lift;
        result.drag=-flow*drag;
        return result;
    }
}
