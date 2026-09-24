using System;
using Bola.Core;
using HarmonyLib;
using UnityEngine;

namespace Bola;

// The current character owner applies only movement overrides. No Rigidbody axes are frozen.
[DefaultExecutionOrder(31000)]
public sealed class BindingMotor : MonoBehaviour
{
    public Binding State { get; private set; }=null!;
    public Character Target { get; private set; }=null!;
    public bool Enforcing => State!=null && State.Phase!=BindingPhase.Released && view && view.IsValid() && view.IsOwner();
    private Rigidbody body=null!;
    private ZNetView view=null!;
    private bool originalFlying,originalGravity,restored;
    private Vector3 anchor,supportLocal;
    private Transform? support;
    private double contactAt=-100;
    private Collider? contactCollider;
    private float downSpeed;
    private Balance balance=null!;
    private bool applyingLandingDamage;
    private Action<BindingMotor>? released;
    private Action? award;
    private static readonly AccessTools.FieldRef<Character,Vector3> RootMotion=AccessTools.FieldRefAccess<Character,Vector3>("m_rootMotion");
    private static readonly AccessTools.FieldRef<Character,Vector3> CurrentVelocity=AccessTools.FieldRefAccess<Character,Vector3>("m_currentVel");
    private static readonly AccessTools.FieldRef<Character,Vector3> Push=AccessTools.FieldRefAccess<Character,Vector3>("m_pushForce");
    public static double Now => Time.timeAsDouble;
    public void Initialize(Character target,bool flying,BindingRules rules,Action awardXp,Action<BindingMotor> recover)
    {
        Target=target; body=target.GetComponent<Rigidbody>(); view=target.GetComponent<ZNetView>();
        balance=Plugin.Instance.Settings.Current;
        if(!body||!view||!view.IsOwner()) throw new InvalidOperationException("Binding requires a supported owner motor");
        originalFlying=target.m_flying; originalGravity=body.useGravity; anchor=body.position;
        State=new Binding(Now,flying,rules); released=recover; award=awardXp;
        target.GetSEMan().AddStatusEffect("SE_BolaBound".GetStableHashCode());
        target.m_flying=false; body.useGravity=true; downSpeed=Mathf.Min(0,body.linearVelocity.y);
        Award();
    }
    private void OnCollisionStay(Collision collision)
    {
        if(!Enforcing || collision.collider.isTrigger || collision.collider.GetComponentInParent<Character>()) return;
        var capsule=Target.GetComponent<CapsuleCollider>();
        if(!capsule) return;
        foreach(var contact in collision.contacts)
        {
            if(contact.normal.y<Mathf.Cos(balance["Flight.MaxGroundSlopeDegrees"]*Mathf.Deg2Rad) || contact.point.y>capsule.bounds.min.y+balance["Flight.GroundProbeDistance"]) continue;
            contactAt=Now; contactCollider=collision.collider;
            if(support!=collision.collider.transform)
            { support=collision.collider.transform; supportLocal=support.InverseTransformPoint(body.position); }
            break;
        }
    }
    public void BeforeMotion()
    {
        if(!Enforcing) return;
        Target.m_flying=false;
        if(!Target.GetSEMan().HaveStatusEffect("SE_BolaBound".GetStableHashCode())) Target.GetSEMan().AddStatusEffect("SE_BolaBound".GetStableHashCode());
        bool contact=contactCollider && support && Now-contactAt<=Time.fixedDeltaTime*2.5;
        bool deep=Target.GetLiquidLevel()-Target.transform.position.y>Mathf.Max(.5f,Target.m_swimDepth-.4f);
        State.Tick(Now,contact,deep,!Target.IsDead()); Award();
        if(State.Phase==BindingPhase.Released) { Finish(); return; }
        if(contact && support) anchor=support.TransformPoint(supportLocal);
        else support=null;
        Guard(); RootMotion(Target)=Vector3.zero; CurrentVelocity(Target)=Vector3.zero; Push(Target)=Vector3.zero;
    }
    public void AfterMotion(float dt)
    {
        if(!Enforcing) return;
        var velocity=body.linearVelocity;
        velocity.x=velocity.z=0;
        if(State.Phase==BindingPhase.Descending)
        {
            downSpeed=Mathf.Max(-balance["Flight.MaxDescentSpeed"],Mathf.Min(0,downSpeed)-balance["Flight.DescentAcceleration"]*dt);
            velocity.y=downSpeed;
            // Controlled acceleration already applied above; re-enable vanilla gravity on release.
            body.useGravity=false;
        }
        else { velocity.y=Mathf.Min(0,velocity.y); body.useGravity=true; }
        body.linearVelocity=velocity;
        RootMotion(Target)=Vector3.zero; CurrentVelocity(Target)=Vector3.zero; Push(Target)=Vector3.zero;
        Guard();
    }
    private void LateUpdate()
    {
        if(State==null || restored) return;
        if(!Enforcing) { State.Abort(Now); Finish(); return; }
        if(Target.IsDead()) { State.Tick(Now,false,alive:false); Finish(); return; }
        Guard();
    }
    private void Guard()
    {
        var position=body.position;
        if(support && Now-contactAt<.1) anchor=support.TransformPoint(supportLocal);
        else anchor.y=Mathf.Min(anchor.y,position.y);
        position.x=anchor.x; position.z=anchor.z;
        position.y=Mathf.Min(position.y,anchor.y+.05f);
        body.position=position; Target.transform.position=position;
    }
    private void Award()
    {
        if(!State.ClaimAward()) return;
        award?.Invoke();
        if(originalFlying && balance["Flight.LandingDamage"]>0)
        {
            var hit=new HitData(); hit.m_damage.m_damage=balance["Flight.LandingDamage"];
            applyingLandingDamage=true;
            try { Target.ApplyDamage(hit,false,true); } finally { applyingLandingDamage=false; }
        }
    }
    public void BreakFromDamage(float amount)
    { if(!Enforcing||applyingLandingDamage) return; State.Damage(Now,amount); if(State.Phase==BindingPhase.Released) Finish(); }
    private void Finish()
    {
        if(restored) return; restored=true;
        if(Target)
        {
            Target.m_flying=originalFlying;
            Target.GetSEMan().RemoveStatusEffect("SE_BolaBound".GetStableHashCode());
            if(!Target.IsDead())
            {
                var immunity=Target.GetSEMan().AddStatusEffect("SE_BolaImmune".GetStableHashCode(),true);
                if(immunity) immunity.m_ttl=(float)Math.Max(.001,State.ImmuneUntil-Now);
            }
        }
        if(body) body.useGravity=originalGravity;
        released?.Invoke(this); Destroy(this);
    }
    private void OnDestroy()
    { if(State!=null&&!restored) { if(Target && Target.IsDead()) State.Tick(Now,false,alive:false); else State.Abort(Now); Finish(); } }
    public static BindingMotor? Active(Character character)
    { var motor=character.GetComponent<BindingMotor>(); return motor&&motor.Enforcing?motor:null; }
}

[HarmonyPatch(typeof(Character),"UpdateMotion")]
internal static class BoundMotion
{
    private static void Prefix(Character __instance) => BindingMotor.Active(__instance)?.BeforeMotion();
    private static void Postfix(Character __instance,float dt) => BindingMotor.Active(__instance)?.AfterMotion(dt);
}
[HarmonyPatch(typeof(Character),"ApplyRootMotion")]
internal static class BoundRootMotion { private static bool Prefix(Character __instance) => BindingMotor.Active(__instance)==null; }
[HarmonyPatch(typeof(Character),nameof(Character.Jump))]
internal static class BoundJump { private static bool Prefix(Character __instance) => BindingMotor.Active(__instance)==null; }
[HarmonyPatch(typeof(Character),nameof(Character.TakeOff))]
internal static class BoundTakeoff { private static bool Prefix(Character __instance) => BindingMotor.Active(__instance)==null; }
[HarmonyPatch(typeof(Character),nameof(Character.ForceJump))]
internal static class BoundForceJump { private static bool Prefix(Character __instance) => BindingMotor.Active(__instance)==null; }
[HarmonyPatch(typeof(Character),nameof(Character.OnAutoJump))]
internal static class BoundAutoJump { private static bool Prefix(Character __instance) => BindingMotor.Active(__instance)==null; }
[HarmonyPatch(typeof(BaseAI),"UpdateTakeoffLanding")]
internal static class BoundFlightAI { private static bool Prefix(BaseAI __instance) => BindingMotor.Active(__instance.GetComponent<Character>())==null; }
[HarmonyPatch(typeof(Character),nameof(Character.ApplyDamage))]
internal static class BoundDamage
{
    private static bool Prefix(Character __instance,HitData hit)
    { return BindingMotor.Active(__instance)==null || hit.m_hitType!=HitData.HitType.Fall; }
    private static void Postfix(Character __instance,HitData hit)
    { if(hit.m_hitType!=HitData.HitType.Fall) BindingMotor.Active(__instance)?.BreakFromDamage(hit.GetTotalDamage()); }
}
