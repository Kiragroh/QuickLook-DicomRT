using System;
using System.Collections.Generic;
using System.Threading;
using Dicom;
namespace QuickLook.DicomRT {
 public struct Vec3 {
  public readonly double X,Y,Z;
  public Vec3(double x,double y,double z){X=x;Y=y;Z=z;}
  public static Vec3 operator +(Vec3 a,Vec3 b)=>new Vec3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
  public static Vec3 operator -(Vec3 a,Vec3 b)=>new Vec3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
  public static Vec3 operator *(Vec3 a,double b)=>new Vec3(a.X*b,a.Y*b,a.Z*b);
  public static Vec3 operator /(Vec3 a,double b)=>a*(1/b);
  public double Dot(Vec3 b)=>X*b.X+Y*b.Y+Z*b.Z;
  public Vec3 Cross(Vec3 b)=>new Vec3(Y*b.Z-Z*b.Y,Z*b.X-X*b.Z,X*b.Y-Y*b.X);
  public double Length=>Math.Sqrt(Dot(this));
  public Vec3 Normalized(){double length=Length;if(double.IsNaN(length)||double.IsInfinity(length)||length<1e-12)throw new InvalidOperationException("Invalid direction vector.");return this/length;}
 }
 public class DicomEntry {
  public string Path{get;set;} public string SopUid{get;set;} public string SeriesUid{get;set;} public string StudyUid{get;set;} public string FrameUid{get;set;} public string Modality{get;set;} public string Description{get;set;} public string PatientKey{get;set;}
  public DicomDataset Dataset{get;set;}
  public int Rows{get;set;} public int Columns{get;set;} public int Frames{get;set;}
  public Vec3 Origin{get;set;} public Vec3 AxisX{get;set;} public Vec3 AxisY{get;set;}
  public double SpacingX{get;set;} public double SpacingY{get;set;} public double WindowCenter{get;set;} public double WindowWidth{get;set;}
  public bool HasGeometry{get;set;}
 }
 public class ImageStack {
  public List<DicomEntry> Entries=new List<DicomEntry>();
  public string Key,Description,Modality,FrameUid,GeometryWarning;
  public bool CanMpr;
  public override string ToString()=>Modality+" / "+Description+" ("+Entries.Count+")";
 }
 public class TagRow {public string Path{get;set;} public string Tag{get;set;} public string Name{get;set;} public string VR{get;set;} public string Value{get;set;}}
}
