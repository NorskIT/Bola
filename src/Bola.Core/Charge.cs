using System;

namespace Bola.Core;

// Immutable per-charge snapshot. Runtime integration must charge these costs directly once.
public sealed class Charge
{
    public double Duration { get; }
    public double ThrowStamina { get; }
    public double HoldPerSecond { get; }
    public double MinimumFraction { get; }
    public Charge(double skill, double time0=1.5, double time100=1, double stamina0=15,
        double stamina100=10, double hold=2, double exponent=1, double minimum=.1)
    {
        Range(skill,-double.MaxValue,double.MaxValue,nameof(skill)); Range(time0,.1,10,nameof(time0)); Range(time100,.1,time0,nameof(time100));
        Range(stamina0,0,1000,nameof(stamina0)); Range(stamina100,0,stamina0,nameof(stamina100));
        Range(hold,0,100,nameof(hold)); Range(exponent,.1,5,nameof(exponent)); Range(minimum,0,1,nameof(minimum));
        double scale=Math.Pow(Math.Max(0,Math.Min(1,skill/100)),exponent);
        Duration=time0+(time100-time0)*scale; ThrowStamina=stamina0+(stamina100-stamina0)*scale;
        HoldPerSecond=hold; MinimumFraction=minimum;
    }
    public double Fraction(double elapsed) { Range(elapsed,0,double.MaxValue,nameof(elapsed)); return Math.Min(1,elapsed/Duration); }
    public bool CanRelease(double elapsed,double stamina) => Fraction(elapsed)>=MinimumFraction && stamina>=ThrowStamina;
    public double HoldingCost(double seconds) { Range(seconds,0,double.MaxValue,nameof(seconds)); return seconds*HoldPerSecond; }
    internal static void Range(double value,double min,double max,string name)
    { if(double.IsNaN(value)||double.IsInfinity(value)||value<min||value>max) throw new ArgumentOutOfRangeException(name); }
}
