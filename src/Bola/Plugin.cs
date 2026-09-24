using System;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace Bola;

[BepInPlugin(Id,"Bola",Version)]
[BepInDependency(Jotunn.Main.ModGuid,"2.30.2")]
[BepInDependency("norskit_noranged_plugin",BepInDependency.DependencyFlags.SoftDependency)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod,VersionStrictness.Patch)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Id="norskit_bola_plugin", Version="0.2.3", ItemName="NorskIT_Bola";
    public static Plugin Instance { get; private set; } = null!;
    public GameObject Visual { get; private set; } = null!;
    public AnimationClip ThrowClip { get; private set; } = null!;
    public Settings Settings { get; private set; } = null!;
    public AnimationClip Clip(string name) => bundle!.LoadAsset<AnimationClip>("Assets/Generated/"+name+".anim");
    private AssetBundle? bundle;
    private Harmony? harmony;
    private void Awake()
    {
        Instance=this;
        if(!NoRangedBridge.AllowsNewAction(out var reason)) Logger.LogWarning(reason);
        Settings=new Settings(Config,message=>Logger.LogWarning(message));
        Text.Register();
        if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            bundle=AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Info.Location)!,"bola.prototype.assets"));
            if(!bundle) throw new InvalidOperationException("Missing prototype bundle");
            Visual=bundle.LoadAsset<GameObject>("Assets/Generated/BolaVisual.prefab");
            ThrowClip=bundle.LoadAsset<AnimationClip>("Assets/Generated/ThrowSource.anim");
        }
        harmony=new Harmony(Id); harmony.PatchAll();
        PrefabManager.OnVanillaPrefabsAvailable+=Register;
    }
    private void Register()
    {
        var item=new CustomItem(ItemName,"Club",new ItemConfig {
            Name="$bola_name",Description="$bola_description",
            Requirements=new[]{new RequirementConfig("Flint",3),new RequirementConfig("LeatherScraps",2)}
        });
        var shared=item.ItemDrop.m_itemData.m_shared;
        if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            using var stream=typeof(Plugin).Assembly.GetManifestResourceStream("Bola.item_icon.png")
                ?? throw new InvalidOperationException("Missing Bola item icon");
            using var bytes=new MemoryStream(); stream.CopyTo(bytes);
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false) {name="Bola item icon"};
            // Unity 6's image module references netstandard 2.1; resolve at runtime
            // to keep the plugin on BepInEx's net481 target (same approach as Flare).
            var conversion=typeof(Texture2D).Assembly.GetType("UnityEngine.ImageConversion")
                ?? Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule",true)!;
            var load=conversion.GetMethod("LoadImage",new[]{typeof(Texture2D),typeof(byte[]),typeof(bool)})!;
            if(!(bool)load.Invoke(null,new object[]{texture,bytes.ToArray(),true})) throw new InvalidOperationException("Invalid Bola item icon");
            shared.m_icons=new[]{Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f))};
        }
        shared.m_itemType=ItemDrop.ItemData.ItemType.TwoHandedWeapon;
        shared.m_skillType=Skills.SkillType.Bows; shared.m_useDurability=false;
        shared.m_maxQuality=1; shared.m_maxStackSize=1; shared.m_weight=1;
        shared.m_damages=default; shared.m_damagesPerLevel=default;
        shared.m_attack=new Attack(); shared.m_secondaryAttack=new Attack();
        item.ItemDrop.m_autoDestroy=false;
        foreach(var name in new[]{"SE_BolaBound","SE_BolaImmune"})
        {
            var effect=ScriptableObject.CreateInstance<StatusEffect>(); effect.name=name;
            effect.m_name=name=="SE_BolaBound"?"$bola_bound":"$bola_immune";
            effect.m_ttl=0; ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(effect,false));
        }
        var original=item.ItemPrefab.transform.Find("attach");
        // Club also contains particle renderers. Their shader is supported but unsuitable for opaque meshes.
        var donor=Visual?original.GetComponentsInChildren<MeshRenderer>(true).First().sharedMaterial:null;
        foreach(var renderer in Visual?Visual.GetComponentsInChildren<Renderer>(true):Array.Empty<Renderer>())
        {
            var material=new Material(donor!);
            if(material.HasProperty("_Color")) material.SetColor("_Color", renderer.name.StartsWith("Weight")?new Color(.32f,.33f,.3f):new Color(.17f,.10f,.055f));
            renderer.sharedMaterial=material;
        }
        UnityEngine.Object.DestroyImmediate(original.gameObject);
        // The donor's attachment includes its dropped-item collider. Replacing that
        // hierarchy also removes collision, so keep physical collision on the item
        // root, independent of the cosmetic attachment (and headless bundles).
        var groundCollider=item.ItemPrefab.AddComponent<SphereCollider>();
        groundCollider.radius=.15f;
        groundCollider.isTrigger=false;
        if(Visual)
        {
            var attach=UnityEngine.Object.Instantiate(Visual,item.ItemPrefab.transform);
            attach.name="attach"; attach.AddComponent<RopeMotion>();
        }
        else new GameObject("attach").transform.SetParent(item.ItemPrefab.transform,false);
        // Awake caches colliders: add only after the final physical hierarchy exists.
        item.ItemPrefab.AddComponent<BolaCarrier>();
        ItemManager.Instance.AddItem(item);
        PrefabManager.OnVanillaPrefabsAvailable-=Register;
        Logger.LogInfo("Bola registered.");
    }
    private void OnDestroy()
    { BindingService.Reset(); PrefabManager.OnVanillaPrefabsAvailable-=Register; harmony?.UnpatchSelf(); if(bundle) bundle.Unload(false); }
}

[HarmonyPatch(typeof(Humanoid),nameof(Humanoid.StartAttack))]
internal static class PrototypeAttackGuard
{
    private static bool Prefix(Humanoid __instance,ref bool __result)
    {
        if(__instance.GetCurrentWeapon()?.m_dropPrefab?.name!=Plugin.ItemName) return true;
        __result=false; return false;
    }
}
