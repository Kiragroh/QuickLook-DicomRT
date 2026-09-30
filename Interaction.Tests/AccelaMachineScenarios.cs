using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using QuickLook.DicomRT;
internal static class AccelaMachineScenarios
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,F).GetValue(target);
    public static void Run(Action<bool,string> check)
    {
        using(var control=new MlcPlaybackControl()){
            var widget=Get<object>(control,"orientation");var type=widget.GetType();
            var set=type.GetMethod("SetContext");
            foreach(var model in new[]{"Accela","Halcyon","Ethos","Unknown"}){
                var beam=new PlanBeam{ManufacturerModelName=model,TreatmentMachineName="SYNTHETIC",PatientPosition="HFS",DynamicCollimator=true};
                set.Invoke(widget,new object[]{beam,"HEAD",false});
                var expected=Get<Model3DGroup>(widget,model=="Halcyon"||model=="Ethos"?"ring":"cArm");
                var host=Get<Model3DGroup>(widget,"machineHost");check(host.Children.Count==1&&ReferenceEquals(host.Children[0],expected),model+" uses correct model without inferring from MLC layers");
                check(Get<TextBlock>(widget,"machineLabel").Text.Contains(model),"Model label remains visible for "+model);
                var patient=Get<Model3DGroup>(widget,"patientHost").Children[0];
                for(int i=0;i<120;i++){type.GetMethod("Set").Invoke(widget,new object[]{(double)i,0d});type.GetMethod("SetCollimator").Invoke(widget,new object[]{(double)i*2});}
                check(ReferenceEquals(host.Children[0],expected)&&ReferenceEquals(patient,Get<Model3DGroup>(widget,"patientHost").Children[0]),"120 CP changes retain "+model+" machine and patient meshes");
                check(Get<AxisAngleRotation3D>(widget,"collimatorRotation").Angle==238,"Collimator marker physically rotates on "+model+" head");
            }
        }
    }
}
