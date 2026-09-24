using Bola.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Bola.Tests;
[TestClass] public sealed class BindingTests
{
    [TestMethod] public void GroundExpiryAndImmunityUseDeadlineNotLateUpdateTime()
    {
        var b=new Binding(100,false,new BindingRules()); Assert.IsTrue(b.ClaimAward()); Assert.IsFalse(b.ClaimAward());
        b.Tick(105.999,false); Assert.AreEqual(BindingPhase.Bound,b.Phase);
        b.Tick(110,false); Assert.AreEqual(106,b.ReleasedAt); Assert.AreEqual(114,b.ImmuneUntil);
        b.Tick(120,false); Assert.AreEqual(114,b.ImmuneUntil);
    }
    [TestMethod] public void FlyerNeedsContinuousContactAndGetsFullGroundDuration()
    {
        var b=new Binding(0,true,new BindingRules()); Assert.IsFalse(b.ClaimAward());
        b.Tick(1,true); b.Tick(1.1,false); b.Tick(2,true); b.Tick(2.14,true);
        Assert.AreEqual(BindingPhase.Descending,b.Phase); b.Tick(2.15,true);
        Assert.AreEqual(BindingPhase.Bound,b.Phase); Assert.AreEqual(8.15,b.Deadline,1e-8); Assert.IsTrue(b.ClaimAward());
        b.Tick(3,false); Assert.AreEqual(8.15,b.Deadline,1e-8);
    }
    [TestMethod] public void TimeoutDeathWaterNeverAwardBeforeGrounding()
    {
        var timeout=new Binding(0,true,new BindingRules()); timeout.Tick(12,false);
        Assert.AreEqual(ReleaseReason.DescentTimeout,timeout.Reason); Assert.AreEqual(16,timeout.ImmuneUntil); Assert.IsFalse(timeout.ClaimAward());
        var death=new Binding(0,true,new BindingRules()); death.Tick(1,false,alive:false); Assert.AreEqual(ReleaseReason.Death,death.Reason); Assert.IsFalse(death.ClaimAward());
        var water=new Binding(0,true,new BindingRules()); water.Tick(2,false,true); Assert.AreEqual(ReleaseReason.Water,water.Reason); Assert.AreEqual(10,water.ImmuneUntil);
    }
    [TestMethod] public void AdditionalHitsNeverRefreshDescentOrDuplicateAward()
    {
        var b=new Binding(0,false,new BindingRules()); Assert.IsFalse(b.AdditionalHit(2)); Assert.AreEqual(6,b.Deadline);
        var refresh=new Binding(0,false,new BindingRules(refresh:true)); Assert.IsTrue(refresh.ClaimAward()); Assert.IsTrue(refresh.AdditionalHit(2)); Assert.AreEqual(8,refresh.Deadline); Assert.IsFalse(refresh.ClaimAward());
        var flyer=new Binding(0,true,new BindingRules(refresh:true)); Assert.IsFalse(flyer.AdditionalHit(2)); Assert.AreEqual(8,flyer.Deadline);
    }
    [TestMethod] public void ExternalDamageOnlyBreaksOptIn()
    {
        var b=new Binding(0,false,new BindingRules()); b.Damage(1,20); Assert.AreEqual(BindingPhase.Bound,b.Phase);
        var enabled=new Binding(0,false,new BindingRules(damageBreaks:true)); enabled.Damage(1,0); Assert.AreEqual(BindingPhase.Bound,enabled.Phase);
        enabled.Damage(2,1); Assert.AreEqual(ReleaseReason.Damage,enabled.Reason); Assert.AreEqual(10,enabled.ImmuneUntil);
    }
    [TestMethod] public void RejectedHitsDoNotInterruptLandingConfirmation()
    {
        var b=new Binding(0,true,new BindingRules()); b.Tick(1,true); b.AdditionalHit(1.05); b.Damage(1.1,0); b.Tick(1.15,true);
        Assert.AreEqual(BindingPhase.Bound,b.Phase);
    }
}
