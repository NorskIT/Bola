using System;
using System.Collections.Generic;
using Bola.Core;
using HarmonyLib;
using UnityEngine;

namespace Bola;

public static class BindingService
{
    private static readonly Dictionary<ZDOID,double> immunity=new();
    private static readonly Dictionary<string,CreatureClass> classifications=new(StringComparer.Ordinal);
    // Local authoritative prototype only. Multiplayer is explicitly gated until its coordinator is implemented.
    public static bool IsLocalAuthority => ZNet.instance && ZNet.instance.IsServer() && ZNet.instance.GetPeers().Count==0;
    public static bool TryBind(Character target,Player attacker,Action<Vector3> recover,out string reason)
    {
        reason="";
        var balance=Plugin.Instance.Settings.Current;
        if(!IsLocalAuthority) { reason="Multiplayer coordinator is not enabled in this test build."; return false; }
        if(!target || target.IsDead() || !target.GetBaseAI() || target.IsPlayer()) { reason="Unsupported target"; return false; }
        var view=target.GetComponent<ZNetView>();
        if(target.GetType().Assembly!=typeof(Character).Assembly) { reason="Custom creature motor needs a compatibility adapter"; return false; }
        if(!view || !view.IsOwner() || !target.GetComponent<CapsuleCollider>() || !target.GetComponent<Rigidbody>()) { reason="Unsupported owner motor"; return false; }
        string prefab=Utils.GetPrefabName(target.gameObject);
        if(!classifications.TryGetValue(prefab,out var category))
        {
            var registered=ZNetScene.instance.GetPrefab(prefab);
            var character=registered?registered.GetComponent<Character>():target;
            var ai=registered?registered.GetComponent<BaseAI>():target.GetBaseAI();
            category=Core.Eligibility.Classify(character.m_flying,ai && ai.m_randomFly); classifications[prefab]=category;
        }
        // Explicitly wake wild AI even when the hit is rejected, without replacing an existing target.
        if(!target.IsTamed()) AccessTools.Method(target.GetBaseAI().GetType(),"OnDamaged").Invoke(target.GetBaseAI(),new object[]{0f,attacker});
        if(balance.Overrides.TryGetValue(prefab,out var explicitClass)) category=explicitClass;
        var reject=balance.Eligibility.Check(prefab,true,false,target.IsTamed(),target.IsBoss(),category);
        if(!balance.Enabled) { reason="Bola disabled"; return false; }
        if(reject!=Rejection.None) { reason=reject.ToString(); return false; }
        var active=BindingMotor.Active(target);
        if(active) { active!.State.AdditionalHit(BindingMotor.Now); reason="Already bound"; return false; }
        if(immunity.TryGetValue(target.GetZDOID(),out var until) && BindingMotor.Now<until) { reason="Temporarily immune"; return false; }
        var motor=target.gameObject.AddComponent<BindingMotor>();
        motor.Initialize(target,category==CreatureClass.Flying,balance.Binding,
            ()=> { if(attacker) attacker.RaiseSkill(Skills.SkillType.Bows,balance["Skills.BindExperienceFactor"]); },
            finished=>
            {
                if(finished.Target) immunity[finished.Target.GetZDOID()]=finished.State.ImmuneUntil;
                recover(finished.Target?finished.Target.transform.position:Vector3.zero);
            });
        return true;
    }
    public static void Reset() { immunity.Clear(); classifications.Clear(); }
}
