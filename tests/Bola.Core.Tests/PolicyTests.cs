using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Bola.Core;
namespace Bola.Tests;
[TestClass]
public sealed class PolicyTests
{
    [TestMethod]
    [DataRow(0d,1.5d,15d)] [DataRow(50d,1.25d,12.5d)] [DataRow(100d,1d,10d)]
    public void SkillSnapshots(double skill,double seconds,double stamina)
    {
        var c=new Charge(skill); Assert.AreEqual(seconds,c.Duration,1e-9); Assert.AreEqual(stamina,c.ThrowStamina,1e-9);
        Assert.AreEqual(2d,c.HoldingCost(1),1e-9); Assert.AreEqual(20d,c.HoldingCost(10),1e-9);
        Assert.IsFalse(c.CanRelease(seconds*.099,100)); Assert.IsTrue(c.CanRelease(seconds*.1,stamina));
        Assert.IsFalse(c.CanRelease(seconds,stamina-.01));
    }
    [TestMethod] public void EffectiveSkillFromOtherModsClampsToEndpoints()
    {
        Assert.AreEqual(1.5d,new Charge(-10).Duration);
        Assert.AreEqual(1d,new Charge(150).Duration);
        Assert.AreEqual(10d,new Charge(150).ThrowStamina);
    }
    [TestMethod] public void InvalidChargeSnapshotsAreRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>new Charge(double.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>new Charge(0,time100:2));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>new Charge(0,stamina100:16));
    }
    [TestMethod] public void ExclusionsOverrideBossSwitchAndTamedPolicy()
    {
        var rules=new Eligibility(bosses:true,tamed:true);
        var names=Eligibility.DefaultExclusions.Split(','); Assert.HasCount(13,names);
        foreach(var name in names) Assert.AreEqual(Rejection.Excluded,rules.Check(name,true,false,true,true,CreatureClass.Flying));
        Assert.AreEqual(Rejection.Player,rules.Check("Player",true,true,false,false,CreatureClass.Ground));
        Assert.AreEqual(Rejection.Tamed,new Eligibility().Check("Boar",true,false,true,false,CreatureClass.Ground));
    }
    [TestMethod] public void BossesAreIndependentOfGroundFlyingSwitches()
    {
        var rules=new Eligibility(false,false,true);
        Assert.AreEqual(Rejection.None,rules.Check("Dragon",true,false,false,true,CreatureClass.Flying));
        Assert.AreEqual(Rejection.GroundDisabled,rules.Check("Greyling",true,false,false,false,CreatureClass.Ground));
        Assert.AreEqual(Rejection.FlyingDisabled,rules.Check("Ghost_Void",true,false,false,false,Eligibility.Classify(false,true)));
    }
    [TestMethod] public void BallisticRangeAndLowArcAgree()
    {
        double speed=Ballistics.SpeedForRange(25);
        Assert.AreEqual(15.2,speed,.02);
        Assert.IsTrue(Ballistics.LowArc(20,-1.5,speed,9.81,out double angle));
        double time=20/(speed*Math.Cos(angle));
        Assert.AreEqual(-1.5,speed*Math.Sin(angle)*time-9.81*time*time/2,1e-8);
        Assert.IsFalse(Ballistics.LowArc(30,-1.5,speed,9.81,out _));
        Assert.AreEqual(0d,Ballistics.Spread(1)); Assert.AreEqual(2d,Ballistics.Spread(.5));
    }
}
