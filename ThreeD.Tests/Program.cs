using System;
using System.Collections.Generic;
using System.Threading;
using QuickLook.DicomRT;
class Program
{
 static int count;static void Check(bool value,string label){if(!value)throw new Exception(label);count++;}
 static Contour Square(double low,double high,double z,string type="CLOSED_PLANAR")=>new Contour{GeometricType=type,Points=new List<Vec3>{new Vec3(low,low,z),new Vec3(high,low,z),new Vec3(high,high,z),new Vec3(low,high,z)}};
 [STAThread] static int Main(string[] args){if(args.Length==3&&args[0]=="--approved-public-3d")return VisualCapture.Run(args[1],args[2]);if(args.Length==2&&args[0]=="--scene-budget")return SceneBudget.Run(args[1]);if((args.Length==2||args.Length==3)&&args[0]=="--frame-benchmark")return FrameBenchmark.Run(args[1],args.Length==3?args[2]:null);if(args.Length==2&&args[0]=="--benchmark")return Benchmark.Run(args[1]);try{
 var v=new VolumeData{Width=2,Height=2,Depth=2,Origin=new Vec3(10,20,30),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1),SpacingX=2,SpacingY=3,SpacingZ=4,Values=new float[]{0,1,0,1,0,1,0,1}};
 var m=ThreeDGeometry.Isosurface(v,p=>v.Sample(p),.5,16,CancellationToken.None);Check(m.Indices.Count>0,"threshold produces surface");foreach(var p in m.Points)Check(Math.Abs(p.X-11)<1e-6,"physical threshold interpolation");
 for(int i=0;i<m.Indices.Count;i+=3){var a=m.Points[m.Indices[i]];var b=m.Points[m.Indices[i+1]];var c=m.Points[m.Indices[i+2]];Check((b-a).Cross(c-a).X<0,"surface normal points out of threshold region");}
 var roi=new StructureRoi{Contours=new List<Contour>{Square(0,10,0,"CLOSEDPLANAR_XOR"),Square(3,7,0,"CLOSEDPLANAR_XOR"),Square(0,10,2,"CLOSEDPLANAR_XOR"),Square(3,7,2,"CLOSEDPLANAR_XOR")}};
 string why;var vox=ThreeDGeometry.VoxelizeRoi(roi,Matrix4.Identity,32,CancellationToken.None,out why);Check(vox!=null,"parallel contour voxelization");Check(vox.SampleNearest(new Vec3(1,1,1))>.5,"ROI interior retained");Check(vox.SampleNearest(new Vec3(5,5,1))<.5,"XOR hole retained");
 var moved=ThreeDGeometry.VoxelizeRoi(roi,new Matrix4(new double[]{1,0,0,100,0,1,0,0,0,0,1,0,0,0,0,1}),32,CancellationToken.None,out why);Check(moved.SampleNearest(new Vec3(101,1,1))>.5,"ROI registration applied");
 var fieldHole=ThreeDGeometry.VoxelizeRoi(roi,Matrix4.Identity,32,CancellationToken.None,out why,true);Check(fieldHole.Sample(new Vec3(5,5,1))<.5,"display distance field retains XOR hole");
 var gapRoi=new StructureRoi{Contours=new List<Contour>{Square(0,10,0),Square(0,10,2),Square(0,10,4),Square(0,10,10)}};var gapField=ThreeDGeometry.VoxelizeRoi(gapRoi,Matrix4.Identity,32,CancellationToken.None,out why,true);Check(gapField.SampleNearest(new Vec3(5,5,7))<.5,"display interpolation does not bridge missing contour planes");
 roi.Contours[0].GeometricType="OPEN_PLANAR";Check(ThreeDGeometry.VoxelizeRoi(roi,Matrix4.Identity,32,CancellationToken.None,out why)==null,"open contour rejects volumetric surface");Check(!string.IsNullOrEmpty(why),"fallback explains limitation");
 var excessive=new StructureRoi();for(int i=0;i<2050;i++)excessive.Contours.Add(Square(0,10,i%2));Check(ThreeDGeometry.VoxelizeRoi(excessive,Matrix4.Identity,32,CancellationToken.None,out why)==null,"contour input allocation bound");
 bool cancelled=false;try{ThreeDGeometry.Isosurface(v,p=>v.Sample(p),.5,16,new CancellationToken(true));}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"mesh cancellation");
 QualityTests.Run();ContextTests.Run();FocusInteractionTests.Run();SliceGuideTests.Run();PopupSmoke.Run();UiSmoke.Run();Check(true,"WPF 3D background build and composition");
 Console.WriteLine("PASS 3D checks: "+count);return 0;
 }catch(Exception e){Console.WriteLine("FAIL 3D: "+e.Message);return 1;}}
}
