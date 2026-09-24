using System;
using System.Collections;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
namespace Bola.RuntimeSmoke;

[BepInPlugin("norskit_bola_smoke","Bola isolated smoke","0.1.0")]
[BepInDependency(Plugin.Id)]
public sealed partial class SmokePlugin : BaseUnityPlugin
{
    private string output="";
    private float deadline;
    private bool failed;
    private bool stagePlayer;
    private Vector3 stagePosition;
    private static string fixtureSaves="";
    private static bool NoCloud(ref bool __result) { __result=false; return false; }
    private static bool SavePath(ref string __result) { __result=fixtureSaves; return false; }
    private void Awake()
    {
        output=Environment.GetEnvironmentVariable("BOLA_SMOKE_OUTPUT")??"";
        if(output=="") { enabled=false; return; }
        var args=Environment.GetCommandLineArgs();
        int at=Array.IndexOf(args,"-savedir");
        if(at<0 || at+1>=args.Length || !string.Equals(Path.GetFullPath(args[at+1]),Path.GetFullPath(Path.Combine(output,"saves")),StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Fixture requires its own explicit -savedir");
        fixtureSaves=Path.GetFullPath(Path.Combine(output,"saves"));
        var isolation=new Harmony("norskit_bola_smoke_isolation");
        isolation.Patch(AccessTools.PropertyGetter(typeof(FileHelpers),"CloudStorageSupportedAndEnabled"),prefix:new HarmonyMethod(typeof(SmokePlugin),nameof(NoCloud)));
        isolation.Patch(AccessTools.Method(typeof(Utils),"GetSaveDataPath"),prefix:new HarmonyMethod(typeof(SmokePlugin),nameof(SavePath)));
        if(Environment.GetEnvironmentVariable("BOLA_SMOKE_GAMEPLAY")=="1")
            isolation.Patch(AccessTools.Method(typeof(ThrowController),"HasInputFocus"),prefix:new HarmonyMethod(typeof(SmokePlugin),nameof(FixtureFocus)));
        Utils.SetSaveDataPath(fixtureSaves);
        deadline=Time.realtimeSinceStartup+240;
        if(Environment.GetEnvironmentVariable("BOLA_INTERACTIVE")=="1") deadline=float.PositiveInfinity;
    }
    private static bool FixtureFocus(ref bool __result) { __result=true; return false; }
    private void Update()
    {
        if(stagePlayer && Player.m_localPlayer)
        { Player.m_localPlayer!.transform.SetPositionAndRotation(stagePosition,Quaternion.identity); }
        if(output!="" && Time.realtimeSinceStartup>deadline) { File.WriteAllText(Path.Combine(output,"failure.txt"),"Runtime fixture timeout"); Application.Quit(); }
    }
    private void Check(bool condition,string message)
    { if(!condition) throw new Exception(message); File.AppendAllText(Path.Combine(output,"checks.txt"),"PASS: "+message+"\n"); }
    private bool Try(Action action)
    {
        try { action(); return true; }
        catch(Exception e) { failed=true; File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString()); Logger.LogError(e); Application.Quit(); return false; }
    }
    private IEnumerator Start()
    {
        if(output=="") yield break;
        yield return new WaitForSecondsRealtime(15);
        if(!Try(()=>
        {
            Check(Plugin.Instance.Visual && Plugin.Instance.ThrowClip && Plugin.Instance.ThrowClip.humanMotion,"Model and humanoid throw load in actual Valheim");
            var actual=Path.GetFullPath(Utils.GetSaveDataPath(FileHelpers.FileSource.Local)).TrimEnd('\\','/');
            var expected=Path.GetFullPath(Path.Combine(output,"saves")).TrimEnd('\\','/');
            File.WriteAllText(Path.Combine(output,"save-paths.txt"),"Actual: "+actual+"\nExpected: "+expected);
            Check(string.Equals(actual,expected,StringComparison.OrdinalIgnoreCase),"Save APIs use isolated fixture only");
            var profile=new PlayerProfile("BolaSmoke",FileHelpers.FileSource.Local); profile.SetName("BolaSmoke"); profile.Save();
            var world=new World("BolaSmoke","bola-prototype") {m_fileSource=FileHelpers.FileSource.Local}; world.SaveWorldFWLData(DateTime.Now);
            Game.SetProfile("BolaSmoke",FileHelpers.FileSource.Local);
            ZNet.SetServer(true,false,false,world.m_name,"",world); ZNet.ResetServerHost();
            AccessTools.Method(typeof(FejdStartup),"LoadMainScene").Invoke(UnityEngine.Object.FindFirstObjectByType<FejdStartup>(),null);
        })) yield break;
        float wait=Time.realtimeSinceStartup+160;
        while(!Player.m_localPlayer && Time.realtimeSinceStartup<wait) yield return new WaitForSecondsRealtime(1);
        if(!Try(()=>Check(Player.m_localPlayer,"Isolated player loaded"))) yield break;
        yield return new WaitForSecondsRealtime(12);
        if(Environment.GetEnvironmentVariable("BOLA_INTERACTIVE")=="1")
        {
            Try(()=>
            {
                var p=Player.m_localPlayer!; p.SetGodMode(true); p.SetGhostMode(false); p.SetIntro(false);
                var valkyrie=UnityEngine.Object.FindFirstObjectByType<Valkyrie>(); if(valkyrie) valkyrie.DropPlayer(true);
                for(int i=0;i<3;i++) p.GetInventory().AddItem(ObjectDB.instance.GetItemPrefab(Plugin.ItemName).GetComponent<ItemDrop>().m_itemData.Clone());
                p.EquipItem(p.GetInventory().GetAllItems().First(i=>i.m_dropPrefab.name==Plugin.ItemName),false);
                p.Message(MessageHud.MessageType.Center,"Bola local test: 3 bolas, invulnerability enabled. This disposable world uses isolated saves.");
                File.WriteAllText(Path.Combine(output,"playtest-ready.txt"),"Interactive isolated local test. No automatic test verdict or multiplayer claim.");
            });
            yield break;
        }
        if(!Try(()=>
        {
            var p=Player.m_localPlayer!; p.SetGodMode(true); p.SetGhostMode(true); p.SetIntro(false);
            var valkyrie=UnityEngine.Object.FindFirstObjectByType<Valkyrie>();
            if(valkyrie) valkyrie.DropPlayer(true);
            p.transform.rotation=Quaternion.identity;
            // Stage the fixture above the terrain so the player and ropes remain visible.
            var stage=GameObject.CreatePrimitive(PrimitiveType.Cube);
            stage.name="BolaFixturePlatform";
            stage.layer=LayerMask.NameToLayer("piece");
            stage.transform.position=p.transform.position+Vector3.up*30;
            stage.transform.localScale=new Vector3(10,1,10);
            p.transform.position=stage.transform.position+Vector3.up*1.6f;
            p.GetComponent<Rigidbody>().linearVelocity=Vector3.zero;
            // Pose inspection fixture only: this is never used as the Bola gameplay motor.
            p.GetComponent<Rigidbody>().constraints=RigidbodyConstraints.FreezeAll;
            stagePosition=p.transform.position; stagePlayer=true;
            p.GetInventory().AddItem(ObjectDB.instance.GetItemPrefab(Plugin.ItemName).GetComponent<ItemDrop>().m_itemData.Clone());
            var item=p.GetInventory().GetAllItems().Single(i=>i.m_dropPrefab.name==Plugin.ItemName);
            Check(p.EquipItem(item,false),"Bola equips using ordinary inventory equipment path");
            Check(!p.StartAttack(null,false),"Prototype cannot launch an unsafe gameplay throw");
            CheckNoRanged(p,item);
        })) yield break;
        yield return new WaitForSecondsRealtime(4);
        if(Environment.GetEnvironmentVariable("BOLA_SMOKE_GAMEPLAY")=="1")
        {
            yield return Gameplay();
            if(!failed) File.WriteAllText(Path.Combine(output,"verified.txt"),"Actual-game local ground/flying binding smoke completed. See checks.txt. No multiplayer validation claim.");
            Application.Quit(); yield break;
        }
        PoseOverlay? overlay=null;
        if(!Try(()=>
        {
            var p=Player.m_localPlayer!;
            var animator=p.GetComponentsInChildren<Animator>(true).First(a=>a.avatar && a.avatar.isHuman);
            File.WriteAllLines(Path.Combine(output,"animators.txt"),p.GetComponentsInChildren<Animator>(true).Select(a=>$"{a.name}: active={a.gameObject.activeInHierarchy} humanoid={a.isHuman}"));
            var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            var ropes=p.GetComponentsInChildren<RopeMotion>();
            Check(ropes.Length==1,"Exactly one visible held Bola instance");
            Check(Vector3.Distance(ropes[0].transform.position,hand.position)<.3f,"Grip attaches within 30 cm of mapped right hand");
            Check(ropes[0].GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterial && r.sharedMaterial.shader.isSupported),"Cloned installed-game materials have supported shaders");
            File.WriteAllLines(Path.Combine(output,"renderers.txt"),ropes[0].GetComponentsInChildren<Renderer>().Select(r=>$"{r.name} enabled={r.enabled} active={r.gameObject.activeInHierarchy} layer={r.gameObject.layer} bounds={r.bounds} scale={r.transform.lossyScale} shader={r.sharedMaterial.shader.name}"));
            Capture("carrying",p);
            overlay=p.gameObject.AddComponent<PoseOverlay>(); overlay.Initialize(animator,Plugin.Instance.Clip("ChargeLoop"));
            Check(overlay.GetComponentsInChildren<Character>().Length==1,"Overlay adds no gameplay character clone");
        })) yield break;
        for(int phase=0;phase<8;phase++)
        {
            if(!Try(()=> { overlay!.ClipTime=Plugin.Instance.Clip("ChargeLoop").length*phase/8f; overlay.Blend=1; })) yield break;
            yield return new WaitForSecondsRealtime(.1f);
            int sample=phase;
            if(!Try(()=> {
                overlay!.Sample();
                File.AppendAllText(Path.Combine(output,"pose.txt"),$"{sample}: hidden hand {overlay.SampledHand}\n");
                var animator=Player.m_localPlayer!.GetComponentsInChildren<Animator>(true).First(a=>a.avatar && a.avatar.isHuman);
                var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                File.AppendAllText(Path.Combine(output,"pose.txt"),$"visible hand {hand.position}; skin references "+string.Join(",",Player.m_localPlayer.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>r.name+":"+r.bones.Contains(hand)))+"\n");
                Capture("charge-sample-"+sample,Player.m_localPlayer!);
            })) yield break;
        }
        if(!Try(()=> { overlay!.SetClip(Plugin.Instance.Clip("ThrowRelease")); overlay.ClipTime=.12f; })) yield break;
        yield return new WaitForSecondsRealtime(.15f);
        if(!Try(()=> { overlay!.Sample(); Capture("release-marker",Player.m_localPlayer!); })) yield break;
        if(!Try(()=> { overlay!.Bake(Plugin.Instance.Clip("ChargeLoop")); overlay.ClipTime=.3f; })) yield break;
        yield return new WaitForSecondsRealtime(.15f);
        if(!Try(()=>
        {
            overlay!.Sample(); Capture("baked-charge-fallback",Player.m_localPlayer!);
            var animator=Player.m_localPlayer!.GetComponentsInChildren<Animator>(true).First(a=>a.avatar && a.avatar.isHuman);
            var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            var head=animator.GetBoneTransform(HumanBodyBones.Head);
            float clearance=hand.position.y-head.position.y;
            File.WriteAllText(Path.Combine(output,"presentation-gate.txt"),
                $"Baked fallback right-hand clearance over head: {clearance:R} metres.\n"+
                (clearance>=.1f?"Overhead height check passed; transition/locomotion and clipping acceptance still pending.":"FAIL: Charge pose is not overhead. Do not advance the presentation gate."));
        })) yield break;
        if(!Try(()=>
        {
            UnityEngine.Object.Destroy(overlay);
            File.WriteAllText(Path.Combine(output,"verified.txt"),"Actual Valheim asset/equipment/retarget sampling smoke passed. Eight static adapted charge poses and one release-marker pose were sampled. This is NOT transition or locomotion validation. Presentation gate remains open.");
        })) yield break;
        if(!failed) Application.Quit();
    }
    private void CheckNoRanged(Player player,ItemDrop.ItemData item)
    {
        if(!Chainloader.PluginInfos.TryGetValue("norskit_noranged_plugin",out var info)) return;
        var setting=info.Instance.Config[new ConfigDefinition("Compatibility","Allow Bola")];
        var recipe=ObjectDB.instance.m_recipes.Single(r=>r && r.m_item && r.m_item.name==Plugin.ItemName);
        var disabled=ScriptableObject.CreateInstance<Recipe>(); disabled.name="BolaDisabledFixture";
        disabled.m_item=recipe.m_item; disabled.m_enabled=false; ObjectDB.instance.m_recipes.Add(disabled);
        try
        {
            Check(NoRangedBridge.AllowsNewAction(out _),"No Ranged default permits exact Bola");
            setting.BoxedValue=false;
            Check(!NoRangedBridge.AllowsNewAction(out _) && !recipe.m_enabled,"No Ranged live off blocks Bola bridge and disables recipe");
            player.UnequipItem(item,false);
            Check(!player.EquipItem(item,false),"No Ranged live off blocks equipment");
            setting.BoxedValue=true;
            Check(NoRangedBridge.AllowsNewAction(out _) && recipe.m_enabled,"No Ranged live on restores its own recipe");
            Check(!disabled.m_enabled,"No Ranged does not enable an already-disabled recipe");
            Check(player.EquipItem(item,false),"No Ranged live on permits equipment again");
        }
        finally { setting.BoxedValue=true; ObjectDB.instance.m_recipes.Remove(disabled); Destroy(disabled); }
    }
    private void Capture(string name,Player player)
    {
        var go=new GameObject("BolaSmokeCamera"); var camera=go.AddComponent<Camera>();
        var center=player.transform.position+Vector3.up;
        camera.transform.position=center+new Vector3(2,0,3);
        camera.transform.LookAt(center); camera.orthographic=true; camera.orthographicSize=1.5f;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.12f,.15f,.17f);
        var transforms=player.GetComponentsInChildren<Transform>(true);
        var skins=player.GetComponentsInChildren<SkinnedMeshRenderer>();
        var force=skins.Select(s=>s.forceMatrixRecalculationPerRender).ToArray();
        foreach(var skin in skins) skin.forceMatrixRecalculationPerRender=true;
        var particles=player.GetComponentsInChildren<ParticleSystemRenderer>();
        var particleEnabled=particles.Select(p=>p.enabled).ToArray();
        foreach(var particle in particles) particle.enabled=false;
        var layers=transforms.Select(t=>t.gameObject.layer).ToArray();
        foreach(var t in transforms) t.gameObject.layer=31;
        camera.cullingMask=1<<31;
        var lightObject=new GameObject("BolaFixtureLight"); var light=lightObject.AddComponent<Light>();
        light.type=LightType.Directional; light.intensity=1.4f; light.cullingMask=1<<31;
        light.transform.rotation=Quaternion.Euler(35,210,0);
        var rt=new RenderTexture(1000,1000,24); var image=new Texture2D(1000,1000,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try
        {
            camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,1000,1000),0,0); image.Apply();
            // Reflection avoids the installed image module's netstandard facade at compile time.
            var encoder=Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule",true)!.GetMethod("EncodeToPNG");
            File.WriteAllBytes(Path.Combine(output,name+".png"),(byte[])encoder!.Invoke(null,new object[]{image}));
        }
        finally
        {
            for(int i=0;i<transforms.Length;i++) transforms[i].gameObject.layer=layers[i];
            for(int i=0;i<skins.Length;i++) skins[i].forceMatrixRecalculationPerRender=force[i];
            for(int i=0;i<particles.Length;i++) particles[i].enabled=particleEnabled[i];
            RenderTexture.active=previous; rt.Release(); Destroy(go); Destroy(image); Destroy(rt); Destroy(lightObject);
        }
    }
}
