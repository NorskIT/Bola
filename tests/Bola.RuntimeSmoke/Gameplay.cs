using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Bola.Core;
using HarmonyLib;
using UnityEngine;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
namespace Bola.RuntimeSmoke;
public sealed partial class SmokePlugin
{
    private static readonly HashSet<ZDOID> attackEvents=new();
    private static void AttackEvent(Attack __instance)
    {
        var character=AccessTools.Field(typeof(Attack),"m_character").GetValue(__instance) as Character;
        if(character && BindingMotor.Active(character)) attackEvents.Add(character.GetZDOID());
    }
    private Character Spawn(string prefab,Vector3 position)
    {
        var go=Instantiate(ZNetScene.instance.GetPrefab(prefab),position,Quaternion.identity);
        return go.GetComponent<Character>();
    }
    private IEnumerator Gameplay()
    {
        var p=Player.m_localPlayer!;
        yield return GroundRecovery();
        if(failed) yield break;
        var attackProbe=new Harmony("norskit_bola_smoke_attack_probe");
        attackProbe.Patch(AccessTools.Method(typeof(Attack),"OnAttackTrigger"),postfix:new HarmonyMethod(typeof(SmokePlugin),nameof(AttackEvent)));
        p.SetGhostMode(false);
        foreach(string prefab in new[]{"Greyling","Wolf"})
        {
            Character? creature=null; BindingMotor? motor=null; int recovered=0;
            var at=stagePosition+Vector3.right*1.5f;
            if(!Try(()=>creature=Spawn(prefab,at))) yield break;
            yield return new WaitForSecondsRealtime(1);
            Vector3 origin=creature!.transform.position; float health=creature.GetHealth();
            var facing=creature.transform.rotation;
            float turned=0;
            if(!Try(()=>
            {
                Check(BindingService.TryBind(creature,p,_=>recovered++,out var reason),prefab+" accepts binding: "+reason);
                motor=creature.GetComponent<BindingMotor>();
                Check(motor.State.Phase==BindingPhase.Bound,prefab+" starts grounded six-second timer");
                Check(creature.GetHealth()==health,prefab+" zero-damage binding preserves health");
                Check(creature.GetBaseAI().IsAlerted(),prefab+" zero-damage hit alerts AI");
                Check(!BindingService.TryBind(creature,p,_=>recovered++,out _),prefab+" rejects second attachment");
            })) yield break;
            float until=Time.time+2;
            while(Time.time<until)
            {
                if(!Try(()=>
                {
                    AccessTools.Field(typeof(Character),"m_moveDir").SetValue(creature,Vector3.right);
                    AccessTools.Field(typeof(Character),"m_rootMotion").SetValue(creature,Vector3.right*.3f);
                    creature.Jump(true);
                    creature.SetLookDir(-(facing*Vector3.forward));
                    turned=Mathf.Max(turned,Quaternion.Angle(facing,creature.transform.rotation));
                })) yield break;
                yield return new WaitForFixedUpdate();
            }
            if(!Try(()=>
            {
                Check(Vector3.ProjectOnPlane(creature.transform.position-origin,Vector3.up).magnitude<.15f,prefab+" resists movement/root-motion/jump displacement");
                Check(creature.GetComponent<Rigidbody>().constraints!=RigidbodyConstraints.FreezeAll,prefab+" motor does not freeze all rigidbody axes");
                Check(turned>1,prefab+" can still rotate while bound");
                creature.SetLookDir(p.transform.position-creature.transform.position);
                var humanoid=(Humanoid)creature;
                Check(humanoid.StartAttack(p,false)||creature.InAttack(),prefab+" native attack can start while bound");
            })) yield break;
            yield return new WaitForSecondsRealtime(4.3f);
            if(!Try(()=>
            {
                Check(recovered==1,prefab+" expiry recovers exactly once");
                Check(attackEvents.Contains(creature.GetZDOID()),prefab+" native attack hit events execute while bound");
                Check(!BindingService.TryBind(creature,p,_=>recovered++,out var why)&&why=="Temporarily immune",prefab+" rejects during immunity");
                ZNetScene.instance.Destroy(creature.gameObject);
            })) yield break;
        }
        foreach(string prefab in new[]{"Deathsquito","Hatchling"})
        {
            Character? creature=null; BindingMotor? motor=null; int recovered=0;
            if(!Try(()=>creature=Spawn(prefab,stagePosition+Vector3.right*2+Vector3.up*4))) yield break;
            yield return new WaitForSecondsRealtime(.3f);
            float initialY=creature!.transform.position.y; float health=creature.GetHealth(); bool flight=creature.m_flying;
            if(!Try(()=>
            {
                Check(BindingService.TryBind(creature,p,_=>recovered++,out var reason),prefab+" accepts descent: "+reason);
                motor=creature.GetComponent<BindingMotor>();
                Check(motor.State.Phase==BindingPhase.Descending,prefab+" starts descending without grounded timer");
                Check(!motor.State.AwardClaimed,prefab+" descent grants no early XP");
            })) yield break;
            float wait=Time.time+7;
            while(motor && motor.State.Phase==BindingPhase.Descending && Time.time<wait) yield return new WaitForFixedUpdate();
            if(!Try(()=>
            {
                Check(motor && motor.State.Phase==BindingPhase.Bound,prefab+" binds after real platform collision and stable contact");
                Check(creature.transform.position.y<initialY-1,prefab+" descends under gravity");
                Check(motor!.State.Deadline-BindingMotor.Now>5.8,prefab+" retains full grounded duration");
                Check(motor.State.AwardClaimed,prefab+" landing awards once");
                Check(creature.GetHealth()==health,prefab+" descent adds no fall damage");
            })) yield break;
            yield return new WaitForSecondsRealtime(6.2f);
            if(!Try(()=>
            {
                Check(recovered==1 && creature.m_flying==flight,prefab+" release recovers once and restores flight");
                ZNetScene.instance.Destroy(creature.gameObject);
            })) yield break;
        }
        yield return TimeoutAndDeath();
        if(failed) yield break;
        yield return Impact();
        if(failed) yield break;
        yield return Throws();
    }
    private IEnumerator GroundRecovery()
    {
        var p=Player.m_localPlayer!;
        foreach(bool terrain in new[]{false,true})
        foreach(bool thrown in new[]{false,true})
        {
            var ray=stagePosition+Vector3.right*(terrain?8:3)+Vector3.up*3;
            RaycastHit support=default;
            if(!Try(()=>Check(Physics.Raycast(ray,Vector3.down,out support,2000,
                LayerMask.GetMask(terrain?"terrain":"piece"),QueryTriggerInteraction.Ignore),
                "Recovery fixture finds "+(terrain?"terrain":"solid floor")))) yield break;
            var data=ObjectDB.instance.GetItemPrefab(Plugin.ItemName).GetComponent<ItemDrop>().m_itemData.Clone();
            var drop=ItemDrop.DropItem(data,1,support.point+Vector3.up*2,Quaternion.identity);
            drop.m_autoPickup=false;
            var carrier=drop.GetComponent<BolaCarrier>();
            var label=(thrown?"Thrown":"Inventory-dropped")+" Bola on "+(terrain?"terrain":"solid floor");
            if(!Try(()=>Check(drop.GetComponentsInChildren<Collider>().Any(c=>c.enabled&&!c.isTrigger),label+" has physical collision"))) yield break;
            if(thrown) carrier.Launch(p,Vector3.down*15,Plugin.Instance.Settings.Current);
            yield return new WaitForSecondsRealtime(2.5f);
            if(!Try(()=>
            {
                File.AppendAllText(Path.Combine(output,"recovery-positions.txt"),label+": support="+support.point+", item="+drop.transform.position+", velocity="+drop.GetComponent<Rigidbody>().linearVelocity+", liquid="+Floating.GetLiquidLevel(drop.transform.position)+"\n");
                Check(carrier.Phase==BolaCarrier.CarrierPhase.Recoverable,label+" becomes recoverable");
                // A sphere can roll down a slope. Compare against ground at its
                // current position, not the higher original impact point.
                if(terrain) Check(Physics.Raycast(drop.transform.position+Vector3.up*2,Vector3.down,out support,5,
                    LayerMask.GetMask("terrain"),QueryTriggerInteraction.Ignore),label+" retains terrain beneath it");
                Check(drop.transform.position.y>support.point.y+.03f && drop.transform.position.y<support.point.y+.4f,
                    label+" rests above support rather than falling through");
                if(!terrain) Check(drop.GetComponent<Rigidbody>().linearVelocity.magnitude<.2f,label+" settles physically");
                ZNetScene.instance.Destroy(drop.gameObject);
            })) yield break;
        }
    }
    private IEnumerator TimeoutAndDeath()
    {
        var p=Player.m_localPlayer!;
        var config=Plugin.Instance.Config;
        var timeout=config[new ConfigDefinition("Flight","MaxDescentSeconds")];
        timeout.BoxedValue=.5f;
        var flyer=Spawn("Hatchling",stagePosition+Vector3.up*20);
        yield return new WaitForSecondsRealtime(.1f);
        int recovered=0;
        BindingMotor? motor=null;
        if(!Try(()=>
        {
            Check(BindingService.TryBind(flyer,p,_=>recovered++,out _),"Timeout fixture binds flyer");
            motor=flyer.GetComponent<BindingMotor>();
        })) yield break;
        var state=motor!.State;
        yield return new WaitForSecondsRealtime(.7f);
        if(!Try(()=>
        {
            Check(recovered==1 && flyer.m_flying,"Descent timeout recovers once and restores flight");
            Check(state.Reason==ReleaseReason.DescentTimeout&&!state.AwardClaimed,"Descent timeout awards no XP");
            timeout.BoxedValue=8f;
            ZNetScene.instance.Destroy(flyer.gameObject);
        })) yield break;
        var creature=Spawn("Greyling",stagePosition+Vector3.right*2);
        yield return new WaitForSecondsRealtime(.1f);
        recovered=0;
        if(!Try(()=>
        {
            Check(BindingService.TryBind(creature,p,_=>recovered++,out _),"Death fixture binds target");
            creature.GetSEMan().RemoveStatusEffect("SE_BolaBound".GetStableHashCode());
        })) yield break;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        if(!Try(()=>
        {
            Check(creature.GetSEMan().HaveStatusEffect("SE_BolaBound".GetStableHashCode()),"Lost bound marker is reapplied without restarting lifecycle");
            var hit=new HitData(); hit.m_damage.m_damage=10000; creature.ApplyDamage(hit,false,false);
        })) yield break;
        yield return new WaitForSecondsRealtime(.5f);
        if(!Try(()=>Check(recovered==1,"Target death recovers exactly once"))) yield break;
        if(creature) ZNetScene.instance.Destroy(creature.gameObject);
        int revision=Plugin.Instance.Settings.Current.Revision;
        var chargeTime=config[new ConfigDefinition("Charge","TimeAtSkill100")];
        chargeTime.BoxedValue=3f;
        if(!Try(()=>Check(Plugin.Instance.Settings.Current.Revision==revision,"Invalid cross-field config revision is rejected atomically"))) yield break;
        chargeTime.BoxedValue=1f;
    }
    private IEnumerator Impact()
    {
        var p=Player.m_localPlayer!;
        var target=Spawn("Greyling",stagePosition+Vector3.right*2);
        yield return new WaitForSecondsRealtime(.15f);
        var position=target.GetComponent<Collider>().bounds.center+Vector3.right*1.5f;
        var item=ObjectDB.instance.GetItemPrefab(Plugin.ItemName).GetComponent<ItemDrop>().m_itemData.Clone();
        var drop=ItemDrop.DropItem(item,1,position,Quaternion.identity);
        var carrier=drop.GetComponent<BolaCarrier>();
        carrier.Launch(p,Vector3.left*15,Plugin.Instance.Settings.Current);
        yield return new WaitForSecondsRealtime(.3f);
        if(!Try(()=>Check(carrier.Phase==BolaCarrier.CarrierPhase.Attached && BindingMotor.Active(target),"Swept projectile impact attaches its carrier and binds target"))) yield break;
        var id=carrier.Identity;
        var hit=new HitData(); hit.m_damage.m_damage=10000; target.ApplyDamage(hit,false,false);
        yield return new WaitForSecondsRealtime(.5f);
        if(!Try(()=>Check(carrier && carrier.Phase==BolaCarrier.CarrierPhase.Recoverable && carrier.Identity==id,"Death transitions the same impact carrier to recoverable"))) yield break;
        ZNetScene.instance.Destroy(carrier.gameObject);
    }
    private IEnumerator Throws()
    {
        var p=Player.m_localPlayer!;
        var input=p.GetComponent<PlayerController>(); if(input) input.enabled=false;
        var controller=ThrowController.For(p);
        var item=p.GetCurrentWeapon();
        float stamina=p.GetStamina();
        if(!Try(()=> { controller.Input(true,false,false); Check(controller.Charging,"Attack starts custom charging"); })) yield break;
        yield return new WaitForSecondsRealtime(.3f);
        if(!Try(()=>
        {
            controller.Input(true,true,false);
            Check(!controller.Charging && p.GetInventory().ContainsItem(item),"Block cancels charging without consuming inventory");
            Check(p.GetStamina()<stamina,"Cancelled charge retains holding stamina cost");
            controller.Input(false,false,false);
            controller.Input(true,false,false);
        })) yield break;
        if(Chainloader.PluginInfos.TryGetValue("norskit_noranged_plugin",out var noRanged))
        {
            var setting=noRanged.Instance.Config[new ConfigDefinition("Compatibility","Allow Bola")];
            setting.BoxedValue=false;
            yield return null;
            if(!Try(()=>Check(!controller.Charging && p.GetInventory().ContainsItem(item),"No Ranged live off cancels uncommitted charge without item consumption"))) yield break;
            setting.BoxedValue=true;
            controller.Input(false,false,false); controller.Input(true,false,false);
        }
        yield return new WaitForSecondsRealtime(1.6f);
        if(!Try(()=>
        {
            Check(controller.Fraction==1,"Full charge reaches one without a vanilla bow draw");
            controller.Input(false,false,false);
        })) yield break;
        yield return new WaitForSecondsRealtime(.25f);
        BolaCarrier? carrier=null;
        if(!Try(()=>
        {
            Check(!p.GetInventory().ContainsItem(item),"Committed throw consumes selected inventory unit once");
            Check(p.GetCurrentWeapon()?.m_dropPrefab?.name!=Plugin.ItemName,"Last throw leaves player unarmed");
            carrier=UnityEngine.Object.FindObjectsByType<BolaCarrier>(FindObjectsSortMode.None).Single(c=>c.Phase!=BolaCarrier.CarrierPhase.Recoverable);
            Check(carrier.Identity==item.m_customData["bola.id"],"World carrier retains the inventory identity");
            Check(!carrier.GetComponent<ItemDrop>().CanPickup(false),"Flight carrier cannot be picked up");
            carrier.Recover(stagePosition+Vector3.forward*2);
            carrier.Recover(stagePosition+Vector3.forward*2);
            Check(carrier.Phase==BolaCarrier.CarrierPhase.Recoverable,"Repeated recovery is idempotent on same carrier");
            Check(!carrier.GetComponent<ItemDrop>().m_autoDestroy,"Recovery has automatic despawn disabled");
        })) yield break;
        yield return new WaitForSecondsRealtime(.6f);
        if(!Try(()=>
        {
            var drop=carrier!.GetComponent<ItemDrop>();
            p.Pickup(drop.gameObject);
            Check(p.GetInventory().GetAllItems().Count(i=>i.m_customData.TryGetValue("bola.id",out var id)&&id==item.m_customData["bola.id"])==1,"Pickup restores exactly one matching identity");
        })) yield break;
        if(input) input.enabled=true;
    }
}
