using System;
using System.Linq;
using System.Runtime.CompilerServices;
namespace QuickLook.DicomRT
{
    // Parsed source beams are immutable during a preview. Weak ownership releases
    // entries with the document; labels or leaf-only display updates are not keys.
    internal static class BeamExtentCache
    {
        internal sealed class Entry {
            readonly Lazy<double> extent,miniature;
            internal double Value=>extent.Value;
            internal double Miniature=>miniature.Value;
            internal Entry(PlanBeam beam){extent=new Lazy<double>(()=>Calculate(beam));miniature=new Lazy<double>(()=>beam.ControlPoints.Count>0&&beam.ControlPoints.All(p=>p.XJaws?.Length==2&&p.YJaws?.Length==2&&p.XJaws.Concat(p.YJaws).All(BeamProjection.Finite))?Math.Max(10,beam.ControlPoints.SelectMany(p=>p.XJaws.Concat(p.YJaws)).Max(x=>Math.Abs(x))*1.1):Value);}
        }
        static readonly ConditionalWeakTable<PlanBeam,Entry> values=new ConditionalWeakTable<PlanBeam,Entry>();
        internal static Entry Get(PlanBeam beam)=>values.GetValue(beam,b=>new Entry(b));
        internal static double Calculate(PlanBeam beam)=>Math.Max(100,beam.ControlPoints.SelectMany(c=>c.MlcLayers.SelectMany(l=>l.Boundaries.Concat(l.Positions)).Concat(c.XJaws??new double[0]).Concat(c.YJaws??new double[0])).Where(BeamProjection.Finite).Select(v=>Math.Abs(v)+10).DefaultIfEmpty(100).Max());
    }
}
