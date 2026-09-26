using System;
using QuickLook.DicomRT;
class Program
{
    static void Near(double expected, double actual) { if (double.IsNaN(actual) || Math.Abs(expected-actual)>1e-8) throw new Exception("Playback interpolation mismatch"); }
    static int Main()
    {
        try {
            var cp0=new ControlPoint();var cp1=new ControlPoint();
            cp0.MlcLayers.Add(new MlcLayer{Key="A",Type="MLCX1",Boundaries=new[]{-1d,1d},Positions=new[]{-4d,8d}});
            cp0.MlcLayers.Add(new MlcLayer{Key="B",Type="MLCX2",Boundaries=new[]{-2d,2d},Positions=new[]{-2d,4d}});
            cp1.MlcLayers.Add(new MlcLayer{Key="B",Type="MLCX2",Boundaries=new[]{-2d,2d},Positions=new[]{-6d,2d}});
            cp1.MlcLayers.Add(new MlcLayer{Key="A",Type="MLCX1",Boundaries=new[]{-1d,1d},Positions=new[]{6d,2d}});
            var layers=MlcTimeline.Layers(cp0,cp1,.5);Near(1,layers[0].Positions[0]);Near(-4,layers[1].Positions[0]);Near(3,layers[1].Positions[1]);Near(-4,cp0.MlcLayers[0].Positions[0]);
            bool bad=false;cp1.MlcLayers[0].Boundaries[0]=-3;try{MlcTimeline.Layers(cp0,cp1,.5);}catch(ArgumentException){bad=true;}if(!bad)throw new Exception("Changed layer geometry accepted");
            Near(1,MlcTimeline.SourceDirection(90)[0]);Near(-1,MlcTimeline.SourceDirection(180)[2]);Near(-1,MlcTimeline.CouchDirection(90)[0]);Near(1,MlcTimeline.CouchDirection(0)[1]);
            int beam;double local;MlcTimeline.Locate(new[]{3,0,2},2.75,out beam,out local);if(beam!=0)throw new Exception("Cross-field interpolation");Near(2,local);MlcTimeline.Locate(new[]{3,0,2},3,out beam,out local);if(beam!=2)throw new Exception("Field boundary");Near(0,local);MlcTimeline.Locate(new[]{3,0,2},4,out beam,out local);Near(1,local);MlcTimeline.Locate(new int[0],0,out beam,out local);if(beam!=-1)throw new Exception("Empty timeline"); Near(0,MlcTimeline.Angle(359,1,.5)); Near(0,MlcTimeline.Angle(1,359,.5)); Near(180,MlcTimeline.Angle(350,10,.5,"CC",true));Near(0,MlcTimeline.Angle(350,10,.5,"CW",true));Near(95,MlcTimeline.Angle(5,5,.25,"CW",true));Near(5,MlcTimeline.Angle(5,5,.25,"NONE",true));Near(0,MlcTimeline.Angle(350,10,.5,"CC",false));Near(345,MlcTimeline.Angle(170,160,.5,"CC",false));if(!double.IsNaN(MlcTimeline.Angle(0,20,.5,"NONE",true)))throw new Exception("Invalid NONE accepted"); var a=new[]{-4.0,8};var b=new[]{6.0,2};var mid=MlcTimeline.Positions(a,b,.5);Near(1,mid[0]);Near(5,mid[1]);Near(-4,a[0]);Near(8,a[1]); bool rejected=false;try {MlcTimeline.Positions(a,new[]{1.0},.5);}catch(ArgumentException){rejected=true;}if(!rejected)throw new Exception("Mismatched leaf banks accepted"); Console.WriteLine("PASS: directed angles/full turns, leaf interpolation, input immutability, incompatible leaves rejected");return 0; }
        catch(Exception e){Console.WriteLine("FAIL: "+e.GetType().Name);return 1;}
    }
}
