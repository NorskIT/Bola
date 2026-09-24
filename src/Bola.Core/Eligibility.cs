using System;
using System.Collections.Generic;

namespace Bola.Core;

public enum CreatureClass { Ground, Flying }
public enum Rejection { None, Unsupported, Player, Tamed, Boss, GroundDisabled, FlyingDisabled, Excluded }
public sealed class Eligibility
{
    public const string DefaultExclusions="Troll,Serpent,Abomination,Blob,BlobElite,Surtling,Wraith,StoneGolem,Gjall,BonemawSerpent,BlobLava,FallenValkyrie,Morgen";
    private readonly HashSet<string> excluded;
    public bool Ground { get; }
    public bool Flying { get; }
    public bool Bosses { get; }
    public bool Tamed { get; }
    public Eligibility(bool ground=true,bool flying=true,bool bosses=false,bool tamed=false,IEnumerable<string>? exclusions=null)
    {
        Ground=ground; Flying=flying; Bosses=bosses; Tamed=tamed;
        excluded=new HashSet<string>(exclusions??DefaultExclusions.Split(','),StringComparer.Ordinal);
    }
    public Rejection Check(string prefab,bool supported,bool player,bool tamed,bool boss,CreatureClass category)
    {
        if(!supported) return Rejection.Unsupported;
        if(player) return Rejection.Player;
        if(tamed&&!Tamed) return Rejection.Tamed;
        if(excluded.Contains(prefab)) return Rejection.Excluded;
        if(boss) return Bosses ? Rejection.None : Rejection.Boss;
        return category==CreatureClass.Flying ? (Flying?Rejection.None:Rejection.FlyingDisabled) : (Ground?Rejection.None:Rejection.GroundDisabled);
    }
    public static CreatureClass Classify(bool originalFlying,bool randomFly) => originalFlying||randomFly?CreatureClass.Flying:CreatureClass.Ground;
}
