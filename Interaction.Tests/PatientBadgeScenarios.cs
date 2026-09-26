using System;
using System.Reflection;
using System.Windows.Media.Media3D;
using QuickLook.DicomRT;
internal static class PatientBadgeScenarios
{
 public static void Run(Action<bool,string> check)
 {
  var type=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.PatientOrientationBadge");var badge=Activator.CreateInstance(type,true);
  var volume=new VolumeData{Width=10,Height=12,Depth=8,SpacingX=1,SpacingY=1,SpacingZ=2,AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1)};
  foreach(var plane in new[]{"Axial","Coronal","Sagittal"})
  {
   var geometry=SliceGeometry.Create(new RenderScene{Volume=volume,Plane=plane});type.GetMethod("SetPlane").Invoke(badge,new object[]{geometry});
   var camera=(OrthographicCamera)type.GetField("camera",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(badge);var right=Vector3D.CrossProduct(camera.LookDirection,camera.UpDirection);right.Normalize();
   check(Math.Abs(right.X-geometry.Right.X)<1e-9&&Math.Abs(right.Y-geometry.Right.Y)<1e-9&&Math.Abs(right.Z-geometry.Right.Z)<1e-9,"Patient badge LPS left/right agrees with "+plane+" image axes");
  }
 }
}
