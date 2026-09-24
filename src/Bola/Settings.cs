using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Bola.Core;
using Jotunn.Utils;
namespace Bola;

public sealed class Balance
{
    private readonly Dictionary<string,float> values;
    public int Revision { get; }
    public Eligibility Eligibility { get; }
    public IReadOnlyDictionary<string,CreatureClass> Overrides { get; }
    public bool Enabled { get; }
    public BindingRules Binding { get; }
    public float this[string name] => values[name];
    internal Balance(int revision,Dictionary<string,float> values,bool enabled,Eligibility eligibility,Dictionary<string,CreatureClass> overrides,bool breaks,bool refresh)
    {
        foreach(var v in values) if(float.IsNaN(v.Value)||float.IsInfinity(v.Value)) throw new ArgumentException("Non-finite setting: "+v.Key);
        this.values=values; Revision=revision; Enabled=enabled; Eligibility=eligibility; Overrides=overrides;
        Binding=new BindingRules(values["Binding.DurationSeconds"],values["Binding.ImmunitySeconds"],values["Flight.MaxDescentSeconds"],values["Flight.ContactConfirmationSeconds"],breaks,refresh);
        Charge(0); // Validate cross-field relationships atomically before accepting the revision.
    }
    public Charge Charge(float skill) => new(skill,this["Charge.TimeAtSkill0"],this["Charge.TimeAtSkill100"],this["Charge.ThrowStaminaAtSkill0"],this["Charge.ThrowStaminaAtSkill100"],this["Charge.HoldStaminaPerSecond"],this["Charge.SkillScalingExponent"],this["Charge.MinimumChargeFraction"]);
}

public sealed class Settings
{
    private readonly Dictionary<string,ConfigEntry<float>> floats=new();
    private readonly ConfigEntry<bool> enabled,ground,flying,bosses,tamed,breaks;
    private readonly ConfigEntry<string> exclusions,overrides,additional;
    private readonly Action<string> warning;
    private int revision;
    public Balance Current { get; private set; }=null!;
    public ConfigEntry<bool> AimZoomEnabled { get; }
    public ConfigEntry<float> AimZoomDegrees { get; }
    public Settings(ConfigFile config,Action<string> warning)
    {
        this.warning=warning;
        AimZoomEnabled=config.Bind("Local","AimZoomEnabled",true,"Enable a small local aim zoom while charging.");
        AimZoomDegrees=config.Bind("Local","AimZoomDegrees",5f,new ConfigDescription("Local aim field-of-view reduction.",new AcceptableValueRange<float>(0,15)));
        ConfigDescription Description(string text,AcceptableValueBase? range=null) => new(text,range,new ConfigurationManagerAttributes { IsAdminOnly=true });
        ConfigEntry<bool> B(string section,string name,bool value) => config.Bind(section,name,value,Description("Server controls new Bola actions."));
        void F(string section,string name,float value,float min,float max)
        { floats.Add(section+"."+name,config.Bind(section,name,value,Description("Captured for each new action; existing actions retain their values.",new AcceptableValueRange<float>(min,max)))); }
        enabled=B("General","Enabled",true); ground=B("Eligibility","AffectGroundCreatures",true); flying=B("Eligibility","AffectFlyingCreatures",true);
        bosses=B("Eligibility","AffectBosses",false); tamed=B("Eligibility","AffectTamed",false); breaks=B("Binding","DamageBreaksBinding",false);
        exclusions=config.Bind("Eligibility","ExcludedPrefabs",Core.Eligibility.DefaultExclusions,Description("Comma-separated exact prefab identifiers; exclusions always win. Unresolved names are retained."));
        overrides=config.Bind("Eligibility","ClassificationOverrides","",Description("Comma-separated exact prefab:Ground or prefab:Flying mappings. Does not provide a custom motor adapter."));
        additional=config.Bind("Binding","AdditionalHitPolicy","Reject",Description("Reject or Refresh. Refresh only extends grounded binding; never awards extra XP.",new AcceptableValueList<string>("Reject","Refresh")));
        F("Binding","DurationSeconds",6,.1f,120); F("Binding","ImmunitySeconds",8,0,600); F("Binding","HitDamage",0,0,1000);
        F("Flight","MaxDescentSeconds",8,.5f,60); F("Flight","DescentAcceleration",20,1,100); F("Flight","MaxDescentSpeed",8,1,30);
        F("Flight","MaxGroundSlopeDegrees",50,0,80); F("Flight","ContactConfirmationSeconds",.15f,.02f,1); F("Flight","GroundProbeDistance",.15f,.02f,.5f); F("Flight","LandingDamage",0,0,1000);
        F("Charge","TimeAtSkill0",1.5f,.1f,10); F("Charge","TimeAtSkill100",1,.1f,10); F("Charge","ThrowStaminaAtSkill0",15,0,1000); F("Charge","ThrowStaminaAtSkill100",10,0,1000);
        F("Charge","HoldStaminaPerSecond",2,0,100); F("Charge","SkillScalingExponent",1,.1f,5); F("Charge","MinimumChargeFraction",.1f,0,1); F("Charge","MovementMultiplier",.7f,0,1);
        F("Charge","ReleaseDelaySeconds",.12f,.05f,.35f); F("Charge","RecoverySeconds",.25f,.05f,1);
        F("Projectile","FullChargeRangeTarget",25,2,100); F("Projectile","Gravity",9.81f,1,30); F("Projectile","MinimumSpeedFraction",.45f,.1f,1);
        F("Projectile","MaximumSpreadDegrees",8,0,45); F("Projectile","CollisionRadius",.12f,.03f,.5f); F("Projectile","MaxFlightSeconds",15,2,120); F("Skills","BindExperienceFactor",1,0,10);
        Apply(); config.SettingChanged+=(_,__)=>Apply();
    }
    private void Apply()
    {
        try
        {
            var mapping=new Dictionary<string,CreatureClass>(StringComparer.Ordinal);
            foreach(string entry in overrides.Value.Split(',').Select(s=>s.Trim()).Where(s=>s.Length>0))
            {
                var pair=entry.Split(':');
                if(pair.Length!=2 || !Enum.TryParse(pair[1].Trim(),false,out CreatureClass category) || !Enum.IsDefined(typeof(CreatureClass),category) || pair[0].Trim().Length==0) throw new ArgumentException("Invalid classification override: "+entry);
                mapping.Add(pair[0].Trim(),category);
            }
            var eligibility=new Eligibility(ground.Value,flying.Value,bosses.Value,tamed.Value,exclusions.Value.Split(',').Select(s=>s.Trim()).Where(s=>s.Length>0));
            var next=new Balance(revision+1,floats.ToDictionary(p=>p.Key,p=>p.Value.Value),enabled.Value,eligibility,mapping,breaks.Value,additional.Value=="Refresh");
            Current=next; revision++;
        }
        catch(Exception error)
        {
            warning("Bola configuration revision rejected; retaining previous values: "+error.Message);
            if(Current==null) throw; // An invalid initial file fails new actions safely.
        }
    }
}
