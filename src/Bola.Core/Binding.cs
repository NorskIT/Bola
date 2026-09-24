using System;
namespace Bola.Core;

public enum BindingPhase { Descending, Bound, Released }
public enum ReleaseReason { None, Expired, DescentTimeout, Water, Death, Damage, Unsupported }
public sealed class BindingRules
{
    public double Duration { get; }
    public double Immunity { get; }
    public double DescentTimeout { get; }
    public double ContactSeconds { get; }
    public bool DamageBreaks { get; }
    public bool Refresh { get; }
    public BindingRules(double duration=6,double immunity=8,double descentTimeout=8,double contactSeconds=.15,bool damageBreaks=false,bool refresh=false)
    {
        Charge.Range(duration,.1,120,nameof(duration)); Charge.Range(immunity,0,600,nameof(immunity));
        Charge.Range(descentTimeout,.5,60,nameof(descentTimeout)); Charge.Range(contactSeconds,.02,1,nameof(contactSeconds));
        Duration=duration; Immunity=immunity; DescentTimeout=descentTimeout; ContactSeconds=contactSeconds; DamageBreaks=damageBreaks; Refresh=refresh;
    }
}

// Authority-neutral deterministic lifecycle. One accepted binding owns one carrier.
// Runtime coordinator supplies an absolute monotonic world clock and owner contact evidence.
public sealed class Binding
{
    public BindingRules Rules { get; }
    public BindingPhase Phase { get; private set; }
    public ReleaseReason Reason { get; private set; }
    public double Deadline { get; private set; }
    public double ReleasedAt { get; private set; }
    public double ImmuneUntil => Reason==ReleaseReason.Death?ReleasedAt:ReleasedAt+Rules.Immunity;
    public bool AwardDue { get; private set; }
    public bool AwardClaimed { get; private set; }
    private double? contactSince;
    private double lastTime;
    public Binding(double now,bool flying,BindingRules rules)
    {
        Charge.Range(now,0,double.MaxValue,nameof(now)); Rules=rules??throw new ArgumentNullException(nameof(rules)); lastTime=now;
        Phase=flying?BindingPhase.Descending:BindingPhase.Bound;
        Deadline=now+(flying?rules.DescentTimeout:rules.Duration); AwardDue=!flying;
    }
    public void Tick(double now,bool contact,bool deepWater=false,bool alive=true)
    {
        Clock(now);
        if(Phase==BindingPhase.Released) return;
        if(!alive) { Release(now,ReleaseReason.Death); return; }
        if(now>=Deadline) { Release(Deadline,Phase==BindingPhase.Bound?ReleaseReason.Expired:ReleaseReason.DescentTimeout); return; }
        if(Phase!=BindingPhase.Descending) return;
        if(deepWater&&!contact) { Release(now,ReleaseReason.Water); return; }
        if(!contact) { contactSince=null; return; }
        contactSince ??= now;
        if(now-contactSince.Value+1e-9<Rules.ContactSeconds) return;
        Phase=BindingPhase.Bound; Deadline=now+Rules.Duration; AwardDue=true;
    }
    public bool ClaimAward()
    {
        if(!AwardDue||AwardClaimed||Phase==BindingPhase.Released) return false;
        AwardClaimed=true; return true;
    }
    public bool AdditionalHit(double now)
    {
        Clock(now);
        if(Phase!=BindingPhase.Released && now>=Deadline) Release(Deadline,Phase==BindingPhase.Bound?ReleaseReason.Expired:ReleaseReason.DescentTimeout);
        if(Phase!=BindingPhase.Bound||!Rules.Refresh) return false;
        Deadline=now+Rules.Duration; return true;
    }
    public void Damage(double now,double amount)
    { Clock(now); if(Phase!=BindingPhase.Released && now>=Deadline) Release(Deadline,Phase==BindingPhase.Bound?ReleaseReason.Expired:ReleaseReason.DescentTimeout); if(amount>0&&Rules.DamageBreaks&&Phase!=BindingPhase.Released) Release(now,ReleaseReason.Damage); }
    public void Abort(double now) { Clock(now); if(Phase!=BindingPhase.Released) Release(now,ReleaseReason.Unsupported); }
    private void Release(double now,ReleaseReason reason) { Phase=BindingPhase.Released; ReleasedAt=now; Reason=reason; }
    private void Clock(double now) { Charge.Range(now,lastTime,double.MaxValue,nameof(now)); lastTime=now; }
}
