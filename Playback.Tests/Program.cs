using System;
using QuickLook.DicomRT;
class Program
{
    static void Near(double expected, double actual) { if (Math.Abs(expected-actual)>1e-8) throw new Exception("Playback interpolation mismatch"); }
    static int Main()
    {
        try { int beam;double local;MlcTimeline.Locate(new[]{3,0,2},2.75,out beam,out local);if(beam!=0)throw new Exception("Cross-field interpolation");Near(2,local);MlcTimeline.Locate(new[]{3,0,2},3,out beam,out local);if(beam!=2)throw new Exception("Field boundary");Near(0,local);MlcTimeline.Locate(new[]{3,0,2},4,out beam,out local);Near(1,local);MlcTimeline.Locate(new int[0],0,out beam,out local);if(beam!=-1)throw new Exception("Empty timeline"); Near(0,MlcTimeline.Angle(359,1,.5)); Near(0,MlcTimeline.Angle(1,359,.5)); Near(180,MlcTimeline.Angle(350,10,.5,"CC",true));Near(0,MlcTimeline.Angle(350,10,.5,"CW",true));Near(95,MlcTimeline.Angle(5,5,.25,"CW",true));Near(5,MlcTimeline.Angle(5,5,.25,"NONE",true));Near(0,MlcTimeline.Angle(350,10,.5,"CC",false));Near(345,MlcTimeline.Angle(170,160,.5,"CC",false));if(!double.IsNaN(MlcTimeline.Angle(0,20,.5,"NONE",true)))throw new Exception("Invalid NONE accepted"); var a=new[]{-4.0,8};var b=new[]{6.0,2};var mid=MlcTimeline.Positions(a,b,.5);Near(1,mid[0]);Near(5,mid[1]);Near(-4,a[0]);Near(8,a[1]); bool rejected=false;try {MlcTimeline.Positions(a,new[]{1.0},.5);}catch(ArgumentException){rejected=true;}if(!rejected)throw new Exception("Mismatched leaf banks accepted"); Console.WriteLine("PASS: directed angles/full turns, leaf interpolation, input immutability, incompatible leaves rejected");return 0; }
        catch(Exception e){Console.WriteLine("FAIL: "+e.GetType().Name);return 1;}
    }
}
