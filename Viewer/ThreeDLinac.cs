using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace QuickLook.DicomRT {
 public sealed partial class ThreeDControl {
  readonly LinacOrientationControl linacOrientation=new LinacOrientationControl{Width=300,Height=260};
  Viewbox linacOverlay;PlanData linacPlan;bool linacNoncoplanar;
  void BuildLinacOverlay(Grid host){
   linacOverlay=new Viewbox{Child=linacOrientation,Width=compact?170:240,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(8,34,8,8),IsHitTestVisible=false,Visibility=Visibility.Collapsed};
   host.Children.Add(linacOverlay);
   host.SizeChanged+=(s,e)=>{linacOverlay.Width=Math.Max(90,Math.Min(compact?170:240,Math.Min(Math.Max(90,host.ActualWidth*.35),Math.Max(90,(host.ActualHeight-50)*300/260*.85))));};
  }
  void UpdateLinac(){
   if(linacOverlay==null)return;
   var beam=scene?.ActiveBeam;var cp=scene?.ActiveControlPoint;
   if(beam==null||cp==null){linacOverlay.Visibility=Visibility.Collapsed;return;}
   if(linacPlan!=scene.Plan){linacPlan=scene.Plan;linacNoncoplanar=linacPlan?.Beams.SelectMany(b=>b.ControlPoints).Any(c=>!double.IsNaN(c.Couch)&&!double.IsInfinity(c.Couch)&&Math.Abs(Math.Sin(c.Couch*Math.PI/180))>.01)==true;}
   string region=scene.PlanToImage!=null?scene.Entry?.Dataset?.GetSingleValueOrDefault<string>(Dicom.DicomTag.BodyPartExamined,""):null;
   linacOrientation.SetContext(beam,region,linacNoncoplanar);
   linacOrientation.Set(cp.Gantry,cp.Couch);linacOrientation.SetCollimator(cp.Collimator);
   linacOverlay.Visibility=Visibility.Visible;
  }
 }
}