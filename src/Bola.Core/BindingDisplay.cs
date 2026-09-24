using System;
namespace Bola.Core;

public static class BindingDisplay
{
    // Wire phases: 0 hidden, 1 descending, 2 bound, 3 normally expired.
    public static bool TryGet(int phase,double remaining,double updateAge,bool sameOwner,out string text)
    {
        text="";
        if(!sameOwner || double.IsNaN(remaining) || double.IsNaN(updateAge) || updateAge<-.5 || updateAge>2) return false;
        if(phase==1) return remaining>0;
        if((phase!=2 && phase!=3) || remaining<-.2) return false;
        text=Math.Max(0,(int)Math.Ceiling(remaining))+"s";
        return true;
    }
}
