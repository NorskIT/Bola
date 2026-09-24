using Bola.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Bola.Tests;
[TestClass] public sealed class BindingDisplayTests
{
    [TestMethod] public void CountdownRoundsUpAndBrieflyShowsZero()
    {
        foreach(var pair in new[]{(6d,"6s"),(2.1,"3s"),(1d,"1s"),(0d,"0s"),(-.19,"0s")})
        { Assert.IsTrue(BindingDisplay.TryGet(2,pair.Item1,0,true,out var text)); Assert.AreEqual(pair.Item2,text); }
        Assert.IsFalse(BindingDisplay.TryGet(3,-.201,0,true,out _));
    }
    [TestMethod] public void DescentHasIconWithoutTimerAndAbortHides()
    {
        Assert.IsTrue(BindingDisplay.TryGet(1,7,0,true,out var text)); Assert.AreEqual("",text);
        Assert.IsFalse(BindingDisplay.TryGet(1,0,0,true,out _));
        Assert.IsFalse(BindingDisplay.TryGet(0,5,0,true,out _));
    }
    [TestMethod] public void StaleOrFormerOwnerReportsNeverDisplay()
    {
        Assert.IsFalse(BindingDisplay.TryGet(2,5,2.01,true,out _));
        Assert.IsFalse(BindingDisplay.TryGet(2,5,0,false,out _));
        Assert.IsFalse(BindingDisplay.TryGet(2,double.NaN,0,true,out _));
    }
}
