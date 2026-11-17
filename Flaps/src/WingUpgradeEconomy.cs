using System;

[Flags]
internal enum WingUpgradeOptions : byte { None=0, Flaps=1, Spoilers=2 }

internal static class WingUpgradeEconomy
{
    internal const float PriceFraction=.25f;
    internal struct Plan { internal float price, cashChange; }
    internal struct Request { internal float basePrice;internal WingUpgradeOptions before,after;internal bool spoilersSupported,placed; }
    internal static bool Finite(float n) { return !float.IsNaN(n) && !float.IsInfinity(n); }
    internal static int Count(WingUpgradeOptions options) { return ((options&WingUpgradeOptions.Flaps)!=0?1:0)+((options&WingUpgradeOptions.Spoilers)!=0?1:0); }
    internal static float UnitPrice(float basePrice) { return basePrice*PriceFraction; }
    internal static float TotalPrice(float basePrice,WingUpgradeOptions options) { return basePrice+UnitPrice(basePrice)*Count(options); }
    internal static bool TryPlan(float basePrice,WingUpgradeOptions before,WingUpgradeOptions after,
        bool spoilersSupported,bool placed,bool creative,float cash,out Plan plan,out string error)
    {
        plan=new Plan();error=null;
        if((before&~(WingUpgradeOptions.Flaps|WingUpgradeOptions.Spoilers))!=0 ||
           (after&~(WingUpgradeOptions.Flaps|WingUpgradeOptions.Spoilers))!=0 ||
           (!spoilersSupported && (after&WingUpgradeOptions.Spoilers)!=0) ||
           !Finite(basePrice) || basePrice<0f || !Finite(cash) || !Finite(TotalPrice(basePrice,after)))
        { error="Upgrade is unavailable";return false; }
        float delta=UnitPrice(basePrice)*(Count(after)-Count(before));
        if(placed && !creative && delta>0f && cash<delta) { error="Not enough money";return false; }
        plan.price=TotalPrice(basePrice,after);
        plan.cashChange=placed && !creative?-delta:0f;
        return true;
    }
    internal static bool TryGroupPlan(Request[] requests,bool creative,float cash,out Plan[] plans,out float cashChange,out string error)
    {
        plans=new Plan[requests.Length];cashChange=0f;error=null;
        if(!Finite(cash)){error="Upgrade is unavailable";return false;}
        for(int i=0;i<requests.Length;i++){
            var r=requests[i];
            if(!TryPlan(r.basePrice,r.before,r.after,r.spoilersSupported,r.placed,creative,float.MaxValue,out plans[i],out error))return false;
            cashChange+=plans[i].cashChange;
        }
        if(!Finite(cashChange)){error="Upgrade is unavailable";return false;}
        if(cashChange<0f && cash < -cashChange){error="Not enough money for both wings";return false;}
        return true;
    }
}
