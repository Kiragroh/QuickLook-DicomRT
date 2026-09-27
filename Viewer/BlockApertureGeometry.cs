using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Windows;
using System.Windows.Media;
namespace QuickLook.DicomRT
{
    internal static class BlockApertureGeometry
    {
        static readonly ConditionalWeakTable<List<BeamBlock>,Dictionary<string,Geometry>> cache=new ConditionalWeakTable<List<BeamBlock>,Dictionary<string,Geometry>>();
        internal static Geometry Create(ControlPoint point)
        {
            if(point.MlcLayers.Count>0)return Build(point);
            var entries=cache.GetValue(point.Blocks,_=>new Dictionary<string,Geometry>());string key=string.Join("|",(point.XJaws??new double[0]).Concat(point.YJaws??new double[0]).Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
            lock(entries){Geometry value;if(entries.TryGetValue(key,out value))return value;value=Build(point);if(entries.Count>=32)entries.Clear();entries[key]=value;return value;}
        }
        static Geometry Build(ControlPoint point)
        {
            Geometry result=Geometry.Empty;
            foreach(var r in BeamAperture.Rectangles(point))result=Geometry.Combine(result,new RectangleGeometry(new Rect(r.Left,r.Bottom,r.Right-r.Left,r.Top-r.Bottom)),GeometryCombineMode.Union,null,.001,ToleranceType.Absolute);
            foreach(var block in point.Blocks){var polygon=new StreamGeometry();using(var g=polygon.Open()){g.BeginFigure(new Point(block.Outline[0].X,block.Outline[0].Y),true,true);g.PolyLineTo(block.Outline.Skip(1).Select(v=>new Point(v.X,v.Y)).ToArray(),true,false);}
                result=Geometry.Combine(result,polygon,block.Type=="APERTURE"?GeometryCombineMode.Intersect:GeometryCombineMode.Exclude,null,.001,ToleranceType.Absolute);
            }
            result.Freeze();return result;
        }
    }
}
