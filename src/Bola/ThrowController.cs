using System;
using Bola.Core;
using HarmonyLib;
using UnityEngine;
namespace Bola;

[DefaultExecutionOrder(32000)]
public sealed class ThrowController : MonoBehaviour
{
    private Player player=null!;
    private ItemDrop.ItemData? reserved;
    private Charge? charge;
    private Balance? balance;
    private float started,releaseAt,recoveryUntil,releasedFraction;
    private bool requireRelease,charging,pending;
    private Camera? zoomCamera;
    private float originalFov,zoomAmount;
    private Texture2D? circle;
    public bool Charging => charging;
    public bool ConsumeCancel { get; private set; }
    public float MovementMultiplier => balance?["Charge.MovementMultiplier"]??1;
    public float Fraction => charge==null?0:(float)charge.Fraction(Math.Max(0,Time.time-started));
    public static ThrowController For(Player player) => player.GetComponent<ThrowController>() ?? player.gameObject.AddComponent<ThrowController>();
    private void Awake() { player=GetComponent<Player>(); }
    private static bool HasInputFocus() => Application.isFocused;
    private bool Valid()
    {
        return player==Player.m_localPlayer && Plugin.Instance.Settings.Current.Enabled && NoRangedBridge.AllowsNewAction(out _)
            && !player.IsDead() && !player.IsTeleporting() && !player.IsSwimming() && !player.IsRiding() && !player.IsAttached()
            && !player.IsStaggering() && !player.InDodge() && HasInputFocus()
            && !InventoryGui.IsVisible() && !Menu.IsVisible() && !Console.IsVisible() && !TextInput.IsVisible()
            && !(Chat.instance && Chat.instance.HasFocus());
    }
    public void Input(bool held,bool cancel,bool interruption)
    {
        if(!held) requireRelease=false;
        if(!cancel) ConsumeCancel=false;
        if(cancel && (charging||pending)) ConsumeCancel=true;
        if(interruption || cancel || !Valid()) { Cancel(); return; }
        if(pending) return;
        if(!charging)
        {
            if(!held||requireRelease||Time.time<recoveryUntil) return;
            var item=player.GetCurrentWeapon();
            if(item?.m_dropPrefab?.name!=Plugin.ItemName) return;
            balance=Plugin.Instance.Settings.Current;
            var snapshot=balance.Charge(player.GetSkillLevel(Skills.SkillType.Bows));
            if(player.GetStamina()<snapshot.ThrowStamina) { requireRelease=true; return; }
            reserved=item; charge=snapshot; started=Time.time; charging=true;
            if(Plugin.Instance.Settings.AimZoomEnabled.Value && !zoomCamera)
            { zoomCamera=Utils.GetMainCamera(); if(zoomCamera) originalFov=zoomCamera.fieldOfView; }
        }
        else if(!held)
        {
            if(!charge!.CanRelease(Time.time-started,player.GetStamina())) { Cancel(); return; }
            releasedFraction=Fraction; charging=false; pending=true; releaseAt=Time.time+balance!["Charge.ReleaseDelaySeconds"];
        }
    }
    private void Update()
    {
        if(!charging&&!pending) return;
        if(!Valid() || player.GetCurrentWeapon()!=reserved) { Cancel(); return; }
        if(charging)
        {
            float drain=(float)charge!.HoldingCost(Time.deltaTime);
            if(player.GetStamina()<drain) { player.UseStamina(player.GetStamina()); Cancel(); return; }
            player.UseStamina(drain);
        }
        if(pending&&Time.time>=releaseAt) Commit();
    }
    private void Cancel() { charging=pending=false; reserved=null; charge=null; requireRelease=true; }
    private void Commit()
    {
        if(!Valid() || reserved==null || charge==null || !player.GetInventory().ContainsItem(reserved) || player.GetCurrentWeapon()!=reserved || player.GetStamina()<charge.ThrowStamina)
        { Cancel(); return; }
        var camera=Utils.GetMainCamera();
        var direction=camera.transform.forward;
        var source=player.transform.position+Vector3.up*1.5f;
        var launch=source+player.transform.forward*.5f;
        var obstruction=Physics.SphereCastAll(source,.12f,(launch-source).normalized,Vector3.Distance(source,launch),BolaCarrier.CollisionMask,QueryTriggerInteraction.Ignore);
        foreach(var h in obstruction) if(h.collider.GetComponentInParent<Player>()!=player) { Cancel(); return; }
        Vector3 aim=camera.transform.position+direction*50;
        foreach(var h in Physics.RaycastAll(camera.transform.position,direction,100,BolaCarrier.CollisionMask,QueryTriggerInteraction.Ignore))
            if(h.collider.GetComponentInParent<Player>()!=player && Vector3.Distance(camera.transform.position,h.point)<Vector3.Distance(camera.transform.position,aim)) aim=h.point;
        float q=releasedFraction;
        float speed=(float)Ballistics.SpeedForRange(balance!["Projectile.FullChargeRangeTarget"],balance["Projectile.Gravity"])*Mathf.Lerp(balance["Projectile.MinimumSpeedFraction"],1,q);
        var offset=aim-launch; var flat=Vector3.ProjectOnPlane(offset,Vector3.up);
        if(flat.magnitude>.01f && Ballistics.LowArc(flat.magnitude,offset.y,speed,balance["Projectile.Gravity"],out var angle))
            direction=flat.normalized*(float)Math.Cos(angle)+Vector3.up*(float)Math.Sin(angle);
        float spread=(float)Ballistics.Spread(q,balance["Projectile.MaximumSpreadDegrees"]);
        if(spread>0) { var disk=UnityEngine.Random.insideUnitCircle*spread; direction=Quaternion.AngleAxis(disk.x,Vector3.up)*Quaternion.AngleAxis(disk.y,Vector3.Cross(direction,Vector3.up))*direction; }
        var item=reserved;
        item.m_customData["bola.id"]=item.m_customData.TryGetValue("bola.id",out var id)?id:Guid.NewGuid().ToString("N");
        var drop=ItemDrop.DropItem(item,1,launch,Quaternion.identity);
        var carrier=drop.GetComponent<BolaCarrier>() ?? drop.gameObject.AddComponent<BolaCarrier>();
        player.UnequipItem(item,false);
        if(!player.GetInventory().RemoveItem(item)) { ZNetScene.instance.Destroy(drop.gameObject); Cancel(); return; }
        player.UseStamina((float)charge.ThrowStamina);
        carrier.Launch(player,direction.normalized*speed,balance);
        charging=pending=false; charge=null; reserved=null; recoveryUntil=Time.time+balance["Charge.RecoverySeconds"]; requireRelease=true;
    }
    private void OnGUI()
    {
        if(!charging || player!=Player.m_localPlayer) return;
        if(!circle)
        {
            circle=new Texture2D(64,64,TextureFormat.RGBA32,false);
            var pixels=new Color[64*64];
            for(int y=0;y<64;y++) for(int x=0;x<64;x++)
            { float r=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(32,32)); pixels[y*64+x]=new Color(1,1,1,r>27&&r<31?1:0); }
            circle.SetPixels(pixels); circle.Apply();
        }
        float size=Mathf.Lerp(54,12,Fraction); var old=GUI.color;
        GUI.color=Fraction>=1?Color.yellow:Color.white;
        GUI.DrawTexture(new Rect(Screen.width/2f-size/2,Screen.height/2f-size/2,size,size),circle!); GUI.color=old;
    }
    private void LateUpdate()
    {
        if(zoomCamera)
        {
            float desired=(charging||pending)&&Plugin.Instance.Settings.AimZoomEnabled.Value?Plugin.Instance.Settings.AimZoomDegrees.Value:0;
            zoomAmount=Mathf.Lerp(zoomAmount,desired,1-Mathf.Exp(-Time.unscaledDeltaTime*15));
            zoomCamera!.fieldOfView=originalFov-zoomAmount;
            if(!charging&&!pending&&zoomAmount<.05f) { zoomCamera.fieldOfView=originalFov; zoomCamera=null; zoomAmount=0; }
        }
        foreach(var rope in player.GetComponentsInChildren<RopeMotion>())
            rope.OverheadBlend=Mathf.MoveTowards(rope.OverheadBlend,charging?1:0,Time.deltaTime*5);
    }
    private void OnDestroy() { if(zoomCamera) zoomCamera!.fieldOfView=originalFov; if(circle) Destroy(circle); }
}

[HarmonyPatch(typeof(Player),nameof(Player.SetControls))]
internal static class BolaControls
{
    private static void Prefix(Player __instance,ref Vector3 movedir,ref bool attack,ref bool attackHold,ref bool secondaryAttack,ref bool secondaryAttackHold,ref bool block,ref bool blockHold,bool jump,bool run,bool dodge)
    {
        if(__instance!=Player.m_localPlayer || __instance.GetCurrentWeapon()?.m_dropPrefab?.name!=Plugin.ItemName) return;
        var controller=ThrowController.For(__instance);
        bool cancel=block||blockHold;
        controller.Input(attack||attackHold,cancel,jump||run||dodge);
        attack=attackHold=secondaryAttack=secondaryAttackHold=false;
        if(controller.ConsumeCancel) block=blockHold=false;
        if(controller.Charging) movedir*=controller.MovementMultiplier;
    }
}
