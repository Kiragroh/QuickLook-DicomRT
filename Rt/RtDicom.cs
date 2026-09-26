using System;
using System.Collections.Generic;
using Dicom;
namespace QuickLook.DicomRT
{
    internal static class RtDicom
    {
        public static IEnumerable<DicomDataset> Items(DicomDataset d, DicomTag tag)
        {
            DicomSequence seq; return d != null && d.TryGetSequence(tag,out seq) ? (IEnumerable<DicomDataset>)seq.Items : new DicomDataset[0];
        }
        public static string Text(DicomDataset d,DicomTag tag,string fallback="") => d?.GetSingleValueOrDefault(tag,fallback) ?? fallback;
        public static double Number(DicomDataset d,DicomTag tag,double fallback=double.NaN) => d?.GetSingleValueOrDefault(tag,fallback) ?? fallback;
        public static int Int(DicomDataset d,DicomTag tag,int fallback=0) => d?.GetSingleValueOrDefault(tag,fallback) ?? fallback;
        public static double[] Numbers(DicomDataset d,DicomTag tag)
        { double[] value; return d != null && d.TryGetValues(tag,out value) ? value : new double[0]; }
        public static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
        public static Vec3 Vector(double[] a, Vec3 fallback) => a.Length==3 && Finite(a[0]) && Finite(a[1]) && Finite(a[2]) ? new Vec3(a[0],a[1],a[2]) : fallback;
        public static DicomDataset Full(DicomEntry entry) => string.IsNullOrEmpty(entry.Path) ? entry.Dataset : DicomFile.Open(entry.Path).Dataset;
    }
}
