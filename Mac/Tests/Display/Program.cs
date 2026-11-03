using QuickLook.DicomRT;
int checks=0;
void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
var samples=new[]{new DoseVoxel(new Vec3(0,0,0),50,.01),new DoseVoxel(new Vec3(1,0,0),40,.01),new DoseVoxel(new Vec3(2,0,0),30,.01),new DoseVoxel(new Vec3(3,0,0),20,.01),new DoseVoxel(new Vec3(4,0,0),10,.01),new DoseVoxel(new Vec3(-1,0,0),1000,.1)};
Check(HotVolumeDose.Calculate(samples,.035,p=>p.X>=0,CancellationToken.None)==20,"D0.035 excludes external hotspots and accumulates the hottest absolute volume");
Check(HotVolumeDose.Calculate(samples.Reverse(),.035,p=>p.X>=0,CancellationToken.None)==20,"D0.035 is independent of voxel iteration order");
Check(HotVolumeDose.Calculate(samples,.1,p=>p.X>=0,CancellationToken.None)==null,"Insufficient known body volume is unavailable");
Check(HotVolumeDose.Calculate(new[]{new DoseVoxel(new Vec3(),90,.02),new DoseVoxel(new Vec3(),80,.02),new DoseVoxel(new Vec3(),70,.03)},.035,_=>true,CancellationToken.None)==80,"Unequal voxel volumes remain weighted by physical volume");
using(var source=new CancellationTokenSource()){source.Cancel();try{HotVolumeDose.Calculate(samples,.035,_=>true,source.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){checks++;}}
var scene=new RenderScene{IsoLevels=new[]{95d,100,105,107,110,120},MaximumRelativeIsoLevel=120};
Check(SliceRaster.IsodoseLevels(scene).SequenceEqual(new[]{95d,100,105,107,110,120}),"Normalized relative levels above 100 are retained");
Check(SliceRaster.IsodoseLevels(new RenderScene{IsoLevels=new[]{95d,120}}).SequenceEqual(new[]{95d}),"Original Windows maximum-relative default remains unchanged");
var volume=new VolumeData{Width=4,Height=4,Depth=4,Values=Enumerable.Range(0,64).Select(i=>(float)i).ToArray(),Origin=new Vec3(),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1),SpacingX=1,SpacingY=1,SpacingZ=1};
var sampled=new RenderScene{Volume=volume,Focus=new Vec3(1.5,1.5,1.5),Plane="Axial",WindowWidth=64,WindowCenter=32};
var serial=SliceRaster.Render(sampled,256,256,CancellationToken.None);sampled.RasterParallelism=2;var parallel=SliceRaster.Render(sampled,256,256,CancellationToken.None);
Check(serial.Pixels.SequenceEqual(parallel.Pixels),"Parallel row scheduling preserves every image pixel");
Console.WriteLine($"PASS dose display math: {checks} checks");
