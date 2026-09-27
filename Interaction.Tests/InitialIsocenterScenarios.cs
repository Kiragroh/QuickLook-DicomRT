using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using QuickLook.DicomRT;
internal static class InitialIsocenterScenarios
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
 static void Jump(ViewerControl v)=>((Task)typeof(ViewerControl).GetMethod("TryInitialIsocenterAsync",F).Invoke(v,null)).GetAwaiter().GetResult();
 public static void Run(Action<bool,string> check)
 {
  using(var viewer=new ViewerControl()){
   var stack=new ImageStack{FrameUid="A"};var cache=Get<Dictionary<string,PixelPlane>>(viewer,"pixelCache");
   for(int i=0;i<3;i++){var e=new DicomEntry{Path="fixture"+i,SeriesUid="series",FrameUid="A",HasGeometry=true,Rows=4,Columns=4,SpacingX=1,SpacingY=1,AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),Origin=new Vec3(0,0,i*5),Modality="CT"};stack.Entries.Add(e);cache[e.Path]=new PixelPlane{Width=4,Height=4,Values=new float[16]};}
   Set(viewer,"currentStack",stack);Set(viewer,"currentEntry",stack.Entries[0]);Set(viewer,"native",cache[stack.Entries[0].Path]);
   var plan=new PlanData{Entry=new DicomEntry{SopUid="plan"},FrameUid="A"};plan.Beams.Add(new PlanBeam{ControlPoints={new ControlPoint{Isocenter=new Vec3(1,2,5)}}});Set(viewer,"selectedPlan",plan);
   Jump(viewer);check(Get<int>(viewer,"sliceIndex")==1,"plan isocenter does not require an RTDOSE");
   Set(viewer,"sliceIndex",0);Set(viewer,"focus",new Vec3(0,0,0));
   var doses=Get<List<DoseGrid>>(viewer,"doses");doses.Add(new DoseGrid{PlanUid="plan",FrameUid="B"});Jump(viewer);check(!Get<bool>(viewer,"initialIsocenterApplied"),"early iso stays provisional until final image loading");doses.Clear();doses.Add(new DoseGrid{PlanUid="plan",FrameUid="A"});
   Set(viewer,"userNavigatedImage",true);Jump(viewer);check(!Get<bool>(viewer,"initialIsocenterApplied")&&Get<int>(viewer,"sliceIndex")==1,"manual navigation before load is preserved");Set(viewer,"userNavigatedImage",false);
   var pane=Get<List<SlicePane>>(viewer,"panes")[0];var zoomHandler=(Action<double>)typeof(SlicePane).GetField("ZoomChanged",F).GetValue(pane);zoomHandler(1.2);
   check(!Get<bool>(viewer,"userNavigatedImage"),"zooming while loading does not suppress final isocenter positioning");
   Set(viewer,"scanComplete",true);Set(viewer,"volume",new VolumeData());
   Set(viewer,"workspaceMode","MLC");Jump(viewer);check(Get<int>(viewer,"sliceIndex")==1&&(Get<Vec3>(viewer,"focus")-new Vec3(1,2,5)).Length<1e-8,"initial iso jump updates focus and actual native slice even in MLC");
   Set(viewer,"focus",new Vec3(3,3,10));Jump(viewer);check((Get<Vec3>(viewer,"focus")-new Vec3(3,3,10)).Length<1e-8,"initial dose jump runs only once");
  }
 }
}
