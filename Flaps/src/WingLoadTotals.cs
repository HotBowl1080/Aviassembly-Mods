using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

internal sealed class WingLoadTotals
{
    static readonly ConditionalWeakTable<PlaneContainer,WingLoadTotals> aircraft=new ConditionalWeakTable<PlaneContainer,WingLoadTotals>();
    readonly Dictionary<Rigidbody,float> totals=new Dictionary<Rigidbody,float>();
    double lastStep=double.NaN;
    int partCount;
    public WingLoadTotals(){}
    internal static float ForBody(PlaneContainer plane,Rigidbody body,double step)
    {
        var cached=aircraft.GetOrCreateValue(plane);
        if(cached.lastStep!=step || cached.partCount!=plane.planeParts.Count){
            cached.totals.Clear();
            foreach(var part in plane.planeParts){
                var wing=part as Wing;
                if(!wing || !wing.isActiveAndEnabled || !wing.rb)continue;
                float weight=wing.GetWingArea()*Mathf.Max(0f,wing.liftForce);
                float previous;cached.totals.TryGetValue(wing.rb,out previous);
                cached.totals[wing.rb]=previous+weight;
            }
            cached.lastStep=step;cached.partCount=plane.planeParts.Count;
        }
        float result;return cached.totals.TryGetValue(body,out result)?result:0f;
    }
}
