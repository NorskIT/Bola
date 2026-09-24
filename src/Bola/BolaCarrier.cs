using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
namespace Bola;

public sealed class BolaCarrier : MonoBehaviour
{
    public enum CarrierPhase { Flight, Attached, Recoverable }
    public CarrierPhase Phase { get; private set; }=CarrierPhase.Recoverable;
    public string Identity { get; private set; }="";
    private ItemDrop item=null!;
    private Rigidbody body=null!;
    private Collider[] colliders=Array.Empty<Collider>();
    private Vector3 velocity,lastSafe;
    private Player attacker=null!;
    private Character? target;
    private float age;
    private Balance balance=null!;
    public static int CollisionMask => LayerMask.GetMask("Default","static_solid","Default_small","piece","terrain","character","character_net","character_ghost","hitbox","vehicle");
    private void Awake()
    {
        item=GetComponent<ItemDrop>(); body=GetComponent<Rigidbody>();
        colliders=GetComponentsInChildren<Collider>();
        item.m_autoDestroy=false;
    }
    public void Launch(Player owner,Vector3 initialVelocity,Balance snapshot)
    {
        balance=snapshot;
        attacker=owner; velocity=initialVelocity; age=0; lastSafe=transform.position;
        if(!item.m_itemData.m_customData.TryGetValue("bola.id",out var id)) item.m_itemData.m_customData["bola.id"]=id=Guid.NewGuid().ToString("N");
        Identity=id; Phase=CarrierPhase.Flight; body.isKinematic=true; item.m_autoPickup=false;
        foreach(var c in colliders) c.enabled=false;
        // Persist the carrier's identity using vanilla ItemDrop serialization.
        AccessTools.Method(typeof(ItemDrop),"Save").Invoke(item,null);
    }
    private void FixedUpdate()
    {
        if(Phase==CarrierPhase.Recoverable)
        {
            // Recoverable items float; the source club body otherwise sinks.
            float liquid=Floating.GetLiquidLevel(transform.position);
            if(liquid>transform.position.y && !body.isKinematic)
                body.AddForce(Vector3.up*(Mathf.Clamp((liquid-transform.position.y)*20,0,20)+9.81f),ForceMode.Acceleration);
            return;
        }
        if(!BindingService.IsLocalAuthority)
        {
            if(target && BindingMotor.Active(target!)) BindingMotor.Active(target!)!.State.Abort(BindingMotor.Now);
            Recover(lastSafe); return;
        }
        if(Phase==CarrierPhase.Attached)
        {
            if(!target) { Recover(lastSafe); return; }
            transform.position=target!.GetComponent<Collider>().bounds.center;
            lastSafe=transform.position; return;
        }
        float dt=Time.fixedDeltaTime; age+=dt;
        if(age>balance["Projectile.MaxFlightSeconds"]) { Recover(lastSafe); return; }
        var from=transform.position;
        var delta=velocity*dt+Vector3.down*(balance["Projectile.Gravity"]*dt*dt*.5f);
        var hits=Physics.SphereCastAll(from,balance["Projectile.CollisionRadius"],delta.normalized,delta.magnitude,CollisionMask,QueryTriggerInteraction.Ignore)
            .Where(h=>h.collider && h.collider.GetComponentInParent<Player>()!=attacker && h.collider.transform.root!=transform.root)
            .OrderBy(h=>h.distance).ToArray();
        if(hits.Length>0)
        {
            var hit=hits[0]; transform.position=hit.point+hit.normal*.15f; lastSafe=transform.position;
            var character=hit.collider.GetComponentInParent<Character>();
            if(character && !character.IsPlayer() && !character.IsTamed() && balance["Binding.HitDamage"]>0)
            {
                var damage=new HitData {m_point=hit.point,m_dir=velocity.normalized}; damage.m_damage.m_blunt=balance["Binding.HitDamage"];
                damage.SetAttacker(attacker); character.Damage(damage);
            }
            if(character && BindingService.TryBind(character,attacker,Recover,out _))
            { target=character; Phase=CarrierPhase.Attached; }
            else Recover(lastSafe);
            return;
        }
        velocity+=Vector3.down*(balance["Projectile.Gravity"]*dt); transform.position=from+delta;
        if(float.IsNaN(transform.position.x) || transform.position.y< -1000 || transform.position.magnitude>20000) Recover(lastSafe);
        else lastSafe=transform.position;
    }
    public void Recover(Vector3 position)
    {
        if(Phase==CarrierPhase.Recoverable) return;
        if(position==Vector3.zero) position=lastSafe;
        Phase=CarrierPhase.Recoverable; target=null;
        transform.position=position+Vector3.up*.15f;
        body.isKinematic=false; body.useGravity=true; body.linearVelocity=Vector3.zero;
        foreach(var c in colliders) c.enabled=true;
        item.m_autoPickup=true;
    }
}

[HarmonyPatch(typeof(ItemDrop),"SlowUpdate")]
internal static class CarrierSlowUpdate
{ private static bool Prefix(ItemDrop __instance) => !__instance.GetComponent<BolaCarrier>() || __instance.GetComponent<BolaCarrier>().Phase==BolaCarrier.CarrierPhase.Recoverable; }
[HarmonyPatch(typeof(ItemDrop),nameof(ItemDrop.CanPickup))]
internal static class CarrierPickup
{
    private static bool Prefix(ItemDrop __instance,ref bool __result)
    {
        var carrier=__instance.GetComponent<BolaCarrier>();
        if(!carrier || carrier.Phase==BolaCarrier.CarrierPhase.Recoverable) return true;
        __result=false; return false;
    }
}
