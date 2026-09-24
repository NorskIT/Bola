using System;
using Bola.Core;

namespace Bola;

// Presentation only: these fields never start or extend gameplay binding.
public static class BindingPresentation
{
    private const string Prefix="norskit_bola_hud_v1_";
    public static void Write(ZDO zdo,Binding state,double localNow)
    {
        double now=ZNet.instance.GetTimeSeconds();
        int phase=state.Phase==BindingPhase.Descending?1:state.Phase==BindingPhase.Bound?2:
            state.Reason==ReleaseReason.Expired?3:0;
        zdo.Set(Prefix+"owner",zdo.GetOwner());
        zdo.Set(Prefix+"deadline",(long)((now+state.Deadline-localNow)*1000));
        zdo.Set(Prefix+"updated",(long)Math.Round(now*1000));
        zdo.Set(Prefix+"phase",phase);
    }
    public static bool Read(ZDO zdo,out string seconds)
    {
        seconds="";
        if(!ZNet.instance) return false;
        double now=ZNet.instance.GetTimeSeconds();
        double age=now-zdo.GetLong(Prefix+"updated",-100000)/1000d;
        int phase=zdo.GetInt(Prefix+"phase",0);
        double left=zdo.GetLong(Prefix+"deadline",0)/1000d-now;
        return BindingDisplay.TryGet(phase,left,age,zdo.GetLong(Prefix+"owner",0)==zdo.GetOwner(),out seconds);
    }
}
