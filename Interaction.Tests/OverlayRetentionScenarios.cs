using System;
using System.Collections;
using System.Reflection;
using System.Windows.Controls;
using QuickLook.DicomRT;
internal static class OverlayRetentionScenarios
{
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
 static void Set(object o,string n,object value)=>o.GetType().GetField(n,F).SetValue(o,value);
 static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,F).Invoke(o,args);
 public static void Run(Action<bool,string> check)
 {
  var assembly=typeof(ViewerControl).Assembly;var frameType=assembly.GetType("QuickLook.DicomRT.MlcProjectionFrame");var outlineType=assembly.GetType("QuickLook.DicomRT.ProjectedOutline");
  Func<StructureRoi,object> frame=roi=>{var value=Activator.CreateInstance(frameType);if(roi!=null){var outline=Activator.CreateInstance(outlineType);Set(outline,"Roi",roi);((IList)Get(value,"Outlines")).Add(outline);}return value;};
  using(var mlc=new MlcPlaybackControl()){
   var ptv=new StructureRoi{InterpretedType="PTV"};var organ=new StructureRoi{InterpretedType="ORGAN"};Set(mlc,"anatomy",new RenderScene{Structures={new RoiOverlay{Roi=ptv},new RoiOverlay{Roi=organ}}});Set(mlc,"projectionKey","angle-A");
   Call(mlc,"ApplyProjection",frame(ptv));((CheckBox)Get(mlc,"showOrgans")).IsChecked=true;Call(mlc,"ApplyProjection",frame(organ));
   var aperture=Get(mlc,"aperture");Func<int> count=()=>((IList)Get(aperture.GetType().GetProperty("Projection").GetValue(aperture),"Outlines")).Count;
   check(count()==2,"enabling organs retains valid PTV while partial frame arrives at same geometry");
   Set(mlc,"projectionKey","angle-B");Call(mlc,"ApplyProjection",frame(organ));check(count()==1,"new angle cannot retain old PTV projection");
   ((CheckBox)Get(mlc,"showOrgans")).IsChecked=false;Call(mlc,"ApplyProjection",frame(null));check(count()==0,"disabled category removed from retained frame");
  }
  var cacheType=assembly.GetType("QuickLook.DicomRT.MlcProjectionCache");var cache=Activator.CreateInstance(cacheType);
  try{var store=Get(cache,"others");Set(store,"budget",1500);Func<object> item=()=>{var x=frame(null);Set(x,"EstimatedBytes",600);return x;};
   var a=item();Call(store,"Put","a",a);Call(store,"Put","b",item());Call(store,"Get","a",true);Call(store,"Put","c",item());
   check(ReferenceEquals(Call(store,"Get","a",false),a)&&Call(store,"Get","b",false)==null,"interactive LRU entries survive background cache pressure");
   var images=Get(cache,"drrs");Set(images,"budget",600);
   Func<int,System.Windows.Media.Imaging.BitmapSource> image=width=>{var bmp=System.Windows.Media.Imaging.BitmapSource.Create(width,10,96,96,System.Windows.Media.PixelFormats.Gray16,null,new byte[width*20],width*2);bmp.Freeze();return bmp;};
   var used=image(10);Call(images,"Put","used",used);Call(images,"Get","used",true);
   for(int i=0;i<12;i++)Call(images,"Put","unused"+i,image(10));
   check(ReferenceEquals(Call(images,"Get","used",false),used),"displayed DRR survives background scan beyond its memory budget");
   Call(images,"PutActive","b",image(10));Call(images,"PutActive","c",image(10));Call(images,"Get","used",true);Call(images,"PutActive","d",image(10));
   check(Call(images,"Get","b",false)==null&&ReferenceEquals(Call(images,"Get","used",false),used)&&(int)Get(images,"bytes")<=600,"requested DRRs use bounded LRU when active working set exceeds budget");
   Call(images,"PutActive","used",image(20));check((int)Get(images,"bytes")<=600&&((System.Windows.Media.Imaging.BitmapSource)Call(images,"Get","used",false)).PixelWidth==20,"DRR refinement replaces its entry and accounts for enlarged memory");
   Call(images,"Clear");check((int)Get(images,"bytes")==0,"viewer disposal releases both DRR cache queues");
   var target=Get(cache,"targets");Call(target,"Put","target",a);for(int i=0;i<10;i++)Call(store,"Put","organ"+i,item());
   check(ReferenceEquals(Call(store,"Get","a",false),a),"replaying an already displayed outline survives a whole background scan");
   check(ReferenceEquals(Call(target,"Get","target",false),a),"organ warming cannot evict target contour cache");
  }finally{((IDisposable)cache).Dispose();}
 }
}
