using System;
using System.Collections.Generic;
using System.Linq;

namespace QuickLook.DicomRT
{
    // Open rectangles in IEC BLD coordinates at isocentre. Every layer and both jaws
    // intersect the opening; this also handles staggered and orthogonal dual layers.
    public static class BeamAperture
    {
        public struct Opening { public double Left,Bottom,Right,Top; }
        public static List<Opening> Rectangles(ControlPoint point)
        {
            var result=new List<Opening>();if(point==null)return result;
            double left=double.NegativeInfinity,right=double.PositiveInfinity,bottom=left,top=right;
            if(point.XJaws?.Length==2){left=point.XJaws[0];right=point.XJaws[1];}
            if(point.YJaws?.Length==2){bottom=point.YJaws[0];top=point.YJaws[1];}
            foreach(var block in point.Blocks.Where(b=>b.Type=="APERTURE")){
                left=Math.Max(left,block.Outline.Min(p=>p.X));right=Math.Min(right,block.Outline.Max(p=>p.X));bottom=Math.Max(bottom,block.Outline.Min(p=>p.Y));top=Math.Min(top,block.Outline.Max(p=>p.Y));
            }
            result.Add(new Opening{Left=left,Right=right,Bottom=bottom,Top=top});
            foreach(var layer in point.MlcLayers)
            {
                int n=layer.Boundaries.Length-1;
                if(n<1||layer.Positions.Length!=2*n)return new List<Opening>();
                var next=new List<Opening>();
                for(int i=0;i<n;i++)
                {
                    double a=layer.Positions[i],b=layer.Positions[i+n],lo=layer.Boundaries[i],hi=layer.Boundaries[i+1];
                    if(!BeamProjection.Finite(a)||!BeamProjection.Finite(b)||!BeamProjection.Finite(lo)||!BeamProjection.Finite(hi))return new List<Opening>();
                    foreach(var old in result)
                    {
                        var r=new Opening{Left=Math.Max(old.Left,layer.IsY?lo:a),Right=Math.Min(old.Right,layer.IsY?hi:b),Bottom=Math.Max(old.Bottom,layer.IsY?a:lo),Top=Math.Min(old.Top,layer.IsY?b:hi)};
                        if(r.Right>r.Left&&r.Top>r.Bottom)next.Add(r);
                    }
                }
                result=next;
            }
            result.RemoveAll(r=>!BeamProjection.Finite(r.Left)||!BeamProjection.Finite(r.Right)||!BeamProjection.Finite(r.Bottom)||!BeamProjection.Finite(r.Top)||r.Left>=r.Right||r.Bottom>=r.Top);
            return result;
        }
    }
}
