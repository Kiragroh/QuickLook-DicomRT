using System;
using System.Collections.Generic;
using System.Threading;

namespace QuickLook.DicomRT
{
    // Geometry only: never used as image intensities, a DRR input or a CT surface.
    public static class RtSliceBounds
    {
        public static VolumeData Create(IEnumerable<RoiOverlay> structures,IEnumerable<Tuple<DoseGrid,Matrix4>> doses,CancellationToken token)
        {
            var low=new Vec3(double.PositiveInfinity,double.PositiveInfinity,double.PositiveInfinity);
            var high=new Vec3(double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity);
            bool found=false;
            Action<Vec3> include=p=>{
                if(double.IsNaN(p.X+p.Y+p.Z)||double.IsInfinity(p.X+p.Y+p.Z))return;
                found=true;low=new Vec3(Math.Min(low.X,p.X),Math.Min(low.Y,p.Y),Math.Min(low.Z,p.Z));high=new Vec3(Math.Max(high.X,p.X),Math.Max(high.Y,p.Y),Math.Max(high.Z,p.Z));
            };
            foreach(var overlay in structures)foreach(var contour in overlay.Roi.Contours){token.ThrowIfCancellationRequested();foreach(var p in contour.Points)include(overlay.RoiToImage.Transform(p));}
            foreach(var pair in doses){token.ThrowIfCancellationRequested();var v=pair.Item1.SamplingBounds;if(v==null)continue;
                foreach(int x in new[]{0,v.Width-1})foreach(int y in new[]{0,v.Height-1})foreach(int z in new[]{0,v.Depth-1})include(pair.Item2.Transform(v.WorldAt(x,y,z)));
            }
            if(!found)return null;
            // A one-mm navigation grid with a small margin. No voxel array is allocated.
            var size=high-low;double step=Math.Max(1,Math.Max(size.X,Math.Max(size.Y,size.Z))/10000);
            return new VolumeData{Origin=low-new Vec3(5,5,5),AxisX=new Vec3(1,0,0),AxisY=new Vec3(0,1,0),AxisZ=new Vec3(0,0,1),SpacingX=step,SpacingY=step,SpacingZ=step,
                Width=(int)Math.Ceiling((size.X+10)/step)+1,Height=(int)Math.Ceiling((size.Y+10)/step)+1,Depth=(int)Math.Ceiling((size.Z+10)/step)+1};
        }
    }
}
