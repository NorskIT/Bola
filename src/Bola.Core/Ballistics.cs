using System;
namespace Bola.Core;
public static class Ballistics
{
    // Maximum range from a height: R = v/g * sqrt(v*v + 2*g*h).
    public static double SpeedForRange(double range,double gravity=9.81,double height=1.5)
    {
        Charge.Range(range,2,100,nameof(range)); Charge.Range(gravity,1,30,nameof(gravity)); Charge.Range(height,0,1000,nameof(height));
        return Math.Sqrt(gravity*(Math.Sqrt(height*height+range*range)-height));
    }
    public static bool LowArc(double horizontal,double vertical,double speed,double gravity,out double radians)
    {
        Charge.Range(horizontal,0,double.MaxValue,nameof(horizontal)); Charge.Range(vertical,-double.MaxValue,double.MaxValue,nameof(vertical));
        Charge.Range(speed,.001,1000,nameof(speed)); Charge.Range(gravity,1,30,nameof(gravity));
        radians=0;
        if(horizontal<1e-8) { radians=vertical>=0?Math.PI/2:-Math.PI/2; return vertical<=speed*speed/(2*gravity); }
        var v2=speed*speed; var d=v2*v2-gravity*(gravity*horizontal*horizontal+2*vertical*v2);
        if(d<0) return false;
        radians=Math.Atan((v2-Math.Sqrt(d))/(gravity*horizontal)); return true;
    }
    public static double Spread(double charge,double maxDegrees=8)
    { Charge.Range(charge,0,1,nameof(charge)); Charge.Range(maxDegrees,0,45,nameof(maxDegrees)); return maxDegrees*(1-charge)*(1-charge); }
}
