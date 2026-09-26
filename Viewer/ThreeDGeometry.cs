using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace QuickLook.DicomRT
{
 internal sealed class ThreeDDetailLimitException : InvalidOperationException
 {
  internal ThreeDDetailLimitException():base("3D object exceeds the 1000000 triangle limit."){}
 }
 public sealed class ThreeDMeshData
 {
  public const int MaximumTriangles=1000000;
  public readonly List<Vec3> Points=new List<Vec3>();public readonly List<int> Indices=new List<int>();public readonly List<Vec3> Normals=new List<Vec3>();
  internal void Triangle(Vec3 a,Vec3 b,Vec3 c)
  {
   if((b-a).Cross(c-a).Length<1e-10)return;
   if(Indices.Count>=MaximumTriangles*3)throw new ThreeDDetailLimitException();
   int k=Points.Count;Points.Add(a);Points.Add(b);Points.Add(c);Indices.Add(k);Indices.Add(k+1);Indices.Add(k+2);
  }
 }
 public static class ThreeDGeometry
 {
  struct VertexKey : IEquatable<VertexKey>
  {
   readonly long x,y,z;public VertexKey(Vec3 p){x=(long)Math.Round(p.X*1000);y=(long)Math.Round(p.Y*1000);z=(long)Math.Round(p.Z*1000);}
   VertexKey(long a,long b,long c){x=a;y=b;z=c;}public VertexKey Offset(int a,int b,int c)=>new VertexKey(x+a,y+b,z+c);
   public bool Equals(VertexKey other)=>x==other.x&&y==other.y&&z==other.z;
   public override bool Equals(object other)=>other is VertexKey&&Equals((VertexKey)other);
   public override int GetHashCode(){unchecked{return (x.GetHashCode()*397^y.GetHashCode())*397^z.GetHashCode();}}
  }
  public static bool ExternalRoi(StructureRoi roi)=>roi!=null&&(string.Equals(roi.InterpretedType?.Trim(),"EXTERNAL",StringComparison.OrdinalIgnoreCase)||(string.IsNullOrWhiteSpace(roi.InterpretedType)&&(string.Equals(roi.Name?.Trim(),"BODY",StringComparison.OrdinalIgnoreCase)||string.Equals(roi.Name?.Trim(),"EXTERNAL",StringComparison.OrdinalIgnoreCase))));
  public static bool DefaultRoi(StructureRoi roi)=>roi!=null&&!ExternalRoi(roi)&&(string.Equals(roi.InterpretedType,"PTV",StringComparison.OrdinalIgnoreCase)||string.Equals(roi.InterpretedType,"ORGAN",StringComparison.OrdinalIgnoreCase));
  public static bool DisplayRoi(StructureRoi roi,bool allTypes)=>roi!=null&&!ExternalRoi(roi)&&(allTypes||DefaultRoi(roi));
  // Display-only extent heuristic: make an enclosing organ faint without changing
  // target opacity or inferring anatomy from names. Bounds are in patient mm.
  public static double RoiOpacityScale(string type,Vec3 extent,Vec3 referenceExtent,bool enclosedTarget)
  {
   if(!string.Equals(type,"ORGAN",StringComparison.OrdinalIgnoreCase))return 1;
   double volume=extent.X*extent.Y*extent.Z,reference=referenceExtent.X*referenceExtent.Y*referenceExtent.Z;
   if(volume<=0||reference<=0)return 1;
   bool large=enclosedTarget?volume/reference>=8&&extent.X>=referenceExtent.X*1.5&&extent.Y>=referenceExtent.Y*1.5&&extent.Z>=referenceExtent.Z*1.5:volume/reference>=.15;
   return large?.18:1;
  }
  // Weld coincident marching-tetrahedron vertices before smoothing. Each vertex
  // remains within maxDisplacement mm of its original sampled surface position.
  // This is a bounded display approximation, never a replacement for contours.
  public static ThreeDMeshData Smooth(ThreeDMeshData source,double maxDisplacement,CancellationToken token)
  {
   var mesh=new ThreeDMeshData();var map=new Dictionary<VertexKey,int>();var indices=new int[source.Points.Count];
   for(int i=0;i<source.Points.Count;i++)
   {
    if((i&4095)==0)token.ThrowIfCancellationRequested();var p=source.Points[i];var key=new VertexKey(p);int k;
    if(!map.TryGetValue(key,out k))
    {
     k=-1;for(int dz=-1;dz<=1&&k<0;dz++)for(int dy=-1;dy<=1&&k<0;dy++)for(int dx=-1;dx<=1&&k<0;dx++){int candidate;if(map.TryGetValue(key.Offset(dx,dy,dz),out candidate)&&(mesh.Points[candidate]-p).Length<.001)k=candidate;}
     if(k<0){k=mesh.Points.Count;mesh.Points.Add(p);}map.Add(key,k);
    }indices[i]=k;
   }
   for(int i=0;i<source.Indices.Count;i+=3){int a=indices[source.Indices[i]],b=indices[source.Indices[i+1]],c=indices[source.Indices[i+2]];if(a==b||b==c||a==c)continue;mesh.Indices.Add(a);mesh.Indices.Add(b);mesh.Indices.Add(c);}
   if(maxDisplacement>0)
   {
   var neighbors=new List<int>[mesh.Points.Count];for(int i=0;i<neighbors.Length;i++)neighbors[i]=new List<int>(12);
   for(int i=0;i<mesh.Indices.Count;i+=3)for(int j=0;j<3;j++){int a=mesh.Indices[i+j],b=mesh.Indices[i+(j+1)%3];neighbors[a].Add(b);neighbors[b].Add(a);}
   var original=mesh.Points.ToArray();var next=new Vec3[original.Length];
   // Six Taubin pairs reduce voxel stair steps without accumulating shrinkage.
   for(int pass=0;pass<12&&maxDisplacement>0;pass++)
   {
    token.ThrowIfCancellationRequested();double weight=pass%2==0?.5:-.53;
    for(int i=0;i<next.Length;i++)
    {
     var sum=new Vec3();foreach(int n in neighbors[i])sum+=mesh.Points[n];var p=neighbors[i].Count==0?mesh.Points[i]:mesh.Points[i]+(sum/neighbors[i].Count-mesh.Points[i])*weight;
     var delta=p-original[i];if(delta.Length>maxDisplacement)p=original[i]+delta*(maxDisplacement/delta.Length);next[i]=p;
    }
    for(int i=0;i<next.Length;i++)mesh.Points[i]=next[i];
   }
   // Prevent display smoothing from turning an original triangle inside out.
   // Restore affected vertices; if restoration cannot converge, retain the
   // original welded surface rather than publish inverted faces.
   for(int attempt=0;attempt<12;attempt++)
   {
    bool restored=false;
    for(int i=0;i<mesh.Indices.Count;i+=3)
    {
     int a=mesh.Indices[i],b=mesh.Indices[i+1],c=mesh.Indices[i+2];var reference=(original[b]-original[a]).Cross(original[c]-original[a]);var normal=(mesh.Points[b]-mesh.Points[a]).Cross(mesh.Points[c]-mesh.Points[a]);
     if(reference.Length>1e-10&&normal.Dot(reference)<=0){mesh.Points[a]=original[a];mesh.Points[b]=original[b];mesh.Points[c]=original[c];restored=true;}
    }
    if(!restored)break;if(attempt==11)for(int i=0;i<original.Length;i++)mesh.Points[i]=original[i];
   }
   }
   var normals=new Vec3[mesh.Points.Count];
   for(int i=0;i<mesh.Indices.Count;i+=3){int a=mesh.Indices[i],b=mesh.Indices[i+1],c=mesh.Indices[i+2];var normal=(mesh.Points[b]-mesh.Points[a]).Cross(mesh.Points[c]-mesh.Points[a]);normals[a]+=normal;normals[b]+=normal;normals[c]+=normal;}
   foreach(var normal in normals)mesh.Normals.Add(normal.Length>1e-12?normal.Normalized():new Vec3(0,0,1));return mesh;
  }
  static readonly int[,] Tetra={{0,5,1,6},{0,1,2,6},{0,2,3,6},{0,3,7,6},{0,7,4,6},{0,4,5,6}};
  static readonly int[,] Edge={{0,1},{0,2},{0,3},{1,2},{1,3},{2,3}};
  public static ThreeDMeshData Isosurface(VolumeData bounds,Func<Vec3,float> sample,double level,int limit,CancellationToken token)
   =>IsosurfaceCore(bounds,sample,level,limit,token,false);
  // Native regular grids can share their immutable scalar payload during extraction.
  public static ThreeDMeshData Isosurface(VolumeData bounds,double level,int limit,CancellationToken token)
   =>IsosurfaceCore(bounds,bounds.Sample,level,limit,token,true);
  static ThreeDMeshData IsosurfaceCore(VolumeData bounds,Func<Vec3,float> sample,double level,int limit,CancellationToken token,bool native)
  {
   token.ThrowIfCancellationRequested();limit=Math.Max(8,Math.Min(256,limit));
   int nx=Math.Max(2,Math.Min(limit,bounds.Width)),ny=Math.Max(2,Math.Min(limit,bounds.Height)),nz=Math.Max(2,Math.Min(limit,bounds.Depth));
   double sx=(bounds.Width-1.0)/(nx-1),sy=(bounds.Height-1.0)/(ny-1),sz=(bounds.Depth-1.0)/(nz-1);
   bool reuse=native&&nx==bounds.Width&&ny==bounds.Height&&nz==bounds.Depth;
   var values=reuse?bounds.Values:new float[nx*ny*nz];
   if(!reuse)for(int z=0;z<nz;z++){token.ThrowIfCancellationRequested();for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)values[(z*ny+y)*nx+x]=sample(bounds.WorldAt(x*sx,y*sy,z*sz));}
   var mesh=new ThreeDMeshData();var cp=new Vec3[8];var cv=new float[8];var intersections=new Vec3[4];var q=new Vec3[4];var f=new float[4];var order=new double[4];
   int[] dx={0,1,1,0,0,1,1,0},dy={0,0,1,1,0,0,1,1},dz={0,0,0,0,1,1,1,1};
   for(int z=0;z<nz-1;z++)
   {
    token.ThrowIfCancellationRequested();
    for(int y=0;y<ny-1;y++)for(int x=0;x<nx-1;x++)
    {
     bool below=false,above=false;for(int i=0;i<8;i++){cv[i]=values[((z+dz[i])*ny+y+dy[i])*nx+x+dx[i]];if(cv[i]<level)below=true;if(cv[i]>=level)above=true;}
     if(!below||!above)continue;
     for(int i=0;i<8;i++)cp[i]=bounds.WorldAt((x+dx[i])*sx,(y+dy[i])*sy,(z+dz[i])*sz);
     for(int t=0;t<6;t++)
     {
      bool valid=true;for(int i=0;i<4;i++){int a=Tetra[t,i];q[i]=cp[a];f[i]=cv[a];if(float.IsNaN(f[i])||float.IsInfinity(f[i]))valid=false;}if(!valid)continue;
      int n=0;for(int edge=0;edge<6;edge++){int a=Edge[edge,0],b=Edge[edge,1];if((f[a]<level)==(f[b]<level))continue;var p=q[a]+(q[b]-q[a])*((level-f[a])/(f[b]-f[a]));bool duplicate=false;for(int j=0;j<n;j++)if((intersections[j]-p).Length<1e-8)duplicate=true;if(!duplicate)intersections[n++]=p;}
      if(n<3)continue;
      if(n==4)
      {
       var center=(intersections[0]+intersections[1]+intersections[2]+intersections[3])/4;var u=(intersections[0]-center).Normalized();var normal=(intersections[1]-intersections[0]).Cross(intersections[2]-intersections[0]);
       // Near an exact threshold corner the first three crossings can be almost
       // collinear. Use the strongest face before normalizing; zero-area faces
       // would also be rejected by Triangle and must not discard the whole ROI.
       for(int i=1;i<3;i++){var candidate=(intersections[i]-intersections[0]).Cross(intersections[3]-intersections[0]);if(candidate.Length>normal.Length)normal=candidate;}
       if(normal.Length<1e-10)continue;normal=normal.Normalized();var v=normal.Cross(u);
       for(int i=0;i<4;i++)order[i]=Math.Atan2((intersections[i]-center).Dot(v),(intersections[i]-center).Dot(u));Array.Sort(order,intersections,0,4);
      }
      var inside=new Vec3();var outside=new Vec3();int ni=0,no=0;for(int i=0;i<4;i++){if(f[i]>=level){inside+=q[i];ni++;}else{outside+=q[i];no++;}}var outward=outside/no-inside/ni;
      if((intersections[1]-intersections[0]).Cross(intersections[2]-intersections[0]).Dot(outward)<0)Array.Reverse(intersections,0,n);
      mesh.Triangle(intersections[0],intersections[1],intersections[2]);if(n==4)mesh.Triangle(intersections[0],intersections[2],intersections[3]);
     }
    }
   }
   return mesh;
  }
  sealed class Loop {public Vec3[] Points;public double Level;}
  // Display reconstruction, not a segmentation or a contour replacement. Each axis
  // targets 0.5 mm for small ROIs and 1 mm otherwise; long extents retain thin anatomy.
  // At most 256^3 scalar samples, 512 contour-plane fields and 1000000 triangles.
  // Large objects retry in 0.5 mm steps up to 3 mm, always reporting actual pitch.
  // Unsupported/budget-limited objects return null with an explicit line-view reason.
  public static ThreeDMeshData BuildRoiSurface(StructureRoi roi,Matrix4 transform,CancellationToken token,out string reason)
  {
   reason=null;
   int initial=EstimateSurfaceStart(roi,transform,token);
   if(initial==0&&transform!=null&&roi?.Contours!=null&&roi.Contours.Count<=2048&&roi.Contours.All(c=>c?.Points!=null)&&roi.Contours.Sum(c=>(long)c.Points.Count)<=400000)
   {
    var low=new Vec3(double.MaxValue,double.MaxValue,double.MaxValue);var high=new Vec3(double.MinValue,double.MinValue,double.MinValue);
    foreach(var contour in roi.Contours){token.ThrowIfCancellationRequested();foreach(var source in contour.Points){var p=transform.Transform(source);low=new Vec3(Math.Min(low.X,p.X),Math.Min(low.Y,p.Y),Math.Min(low.Z,p.Z));high=new Vec3(Math.Max(high.X,p.X),Math.Max(high.Y,p.Y),Math.Max(high.Z,p.Z));}}
    var span=high-low;if(span.X>0&&span.Y>0&&span.Z>0&&Math.Max(span.X,Math.Max(span.Y,span.Z))<=80)initial=-1;
   }
   for(int attempt=initial;attempt<5;attempt++)
   {
    double targetSpacing=1+attempt*.5;
    var volume=VoxelizeRoiCore(roi,transform,256,token,out reason,true,true,targetSpacing);
    if(volume==null)return null;
    try
    {
     var mesh=Isosurface(volume,.5,256,token);
     if(mesh.Indices.Count==0){reason="No resolvable contour interior: line view";return null;}
     if(attempt>0)reason+=string.Format(System.Globalization.CultureInfo.InvariantCulture,"; bounded triangle budget: {0} at {1:0.0} mm",attempt==initial?"estimated start":"retry",targetSpacing);
     return Smooth(mesh,Math.Min(.65,targetSpacing*.65),token);
    }
    catch(ThreeDDetailLimitException){reason="Surface detail limit: line view (no clipped surface)";}
    catch(InvalidOperationException){reason="Degenerate surface geometry: line view";return null;}
   }
   return null;
  }
  sealed class SurfacePlane {public double Level,Perimeter,Area;}
  // A conservative display-cost estimate avoids allocating and discarding several
  // million-vertex meshes for very large ROIs. It does not bypass extraction's hard
  // bound. Plane perimeter estimates lateral area; end areas and changing equivalent
  // radii account for caps and tapering. Small structures always start at 1 mm.
  static int EstimateSurfaceStart(StructureRoi roi,Matrix4 transform,CancellationToken token)
  {
   token.ThrowIfCancellationRequested();
   if(transform==null||roi?.Contours==null||roi.Contours.Count<2||roi.Contours.Count>2048||roi.Contours.Any(c=>c?.Points==null)||roi.Contours.Sum(c=>(long)c.Points.Count)>400000)return 0;
   var first=roi.Contours.FirstOrDefault(c=>c.Points.Count>=3);if(first==null)return 0;
   var origin=transform.Transform(first.Points[0]);var normal=new Vec3();
   for(int i=1;i+1<first.Points.Count;i++){normal=(transform.Transform(first.Points[i])-origin).Cross(transform.Transform(first.Points[i+1])-origin);if(Finite(normal.Length)&&normal.Length>.0001)break;}
   if(!Finite(normal.Length)||normal.Length<=.0001)return 0;normal=normal.Normalized();
   var planes=new List<SurfacePlane>();
   foreach(var contour in roi.Contours)
   {
    token.ThrowIfCancellationRequested();if(contour.Points.Count<3)continue;var p0=transform.Transform(contour.Points[0]);double perimeter=0,area=0;
    for(int i=0;i<contour.Points.Count;i++)
    {
     if((i&1023)==0)token.ThrowIfCancellationRequested();var a=transform.Transform(contour.Points[i]);var b=transform.Transform(contour.Points[(i+1)%contour.Points.Count]);perimeter+=(b-a).Length;area+=(a-p0).Cross(b-p0).Dot(normal);
    }
    if(!Finite(perimeter)||!Finite(area))return 0;planes.Add(new SurfacePlane{Level=p0.Dot(normal),Perimeter=perimeter,Area=Math.Abs(area)*.5});
   }
   var grouped=new List<SurfacePlane>();foreach(var plane in planes.OrderBy(p=>p.Level)){if(grouped.Count==0||Math.Abs(grouped[grouped.Count-1].Level-plane.Level)>.1)grouped.Add(new SurfacePlane{Level=plane.Level});var group=grouped[grouped.Count-1];group.Perimeter+=plane.Perimeter;group.Area+=plane.Area;}
   if(grouped.Count<2)return 0;var gaps=new List<double>();for(int i=1;i<grouped.Count;i++)gaps.Add(grouped[i].Level-grouped[i-1].Level);gaps.Sort();double regular=gaps[(gaps.Count-1)/2],areaEstimate=grouped[0].Area+grouped[grouped.Count-1].Area;
   for(int i=1;i<grouped.Count;i++)
   {
    var a=grouped[i-1];var b=grouped[i];double dz=b.Level-a.Level;
    if(dz>regular*1.5){areaEstimate+=a.Area+b.Area;continue;}
    double taper=a.Perimeter>0&&b.Perimeter>0?2*(b.Area/b.Perimeter-a.Area/a.Perimeter):0;
    areaEstimate+=(a.Perimeter+b.Perimeter)*.5*Math.Sqrt(dz*dz+taper*taper);
   }
   // Six triangles/mm^2 is deliberately below typical fine tetrahedral surfaces;
   // an additional 15% margin favors retaining 1 mm when the estimate is uncertain.
   double estimatedTriangles=6*areaEstimate;if(!Finite(estimatedTriangles))return 0;
   int start=0;while(start<4&&estimatedTriangles/Math.Pow(1+start*.5,2)>ThreeDMeshData.MaximumTriangles*1.15)start++;return start;
  }
  public static VolumeData VoxelizeRoi(StructureRoi roi,Matrix4 transform,int limit,CancellationToken token,out string reason,bool smoothField=false)
   =>VoxelizeRoiCore(roi,transform,limit,token,out reason,smoothField,false);
  static VolumeData VoxelizeRoiCore(StructureRoi roi,Matrix4 transform,int limit,CancellationToken token,out string reason,bool smoothField,bool physicalSpacing,double targetSpacing=1)
  {
   reason=null;limit=Math.Max(8,Math.Min(physicalSpacing?256:64,limit));token.ThrowIfCancellationRequested();
   if(roi?.Contours==null||roi.Contours.Count==0){reason="No contours";return null;}
   if(transform==null||roi.Contours.Any(c=>c==null||c.Points==null)){reason="Invalid contour geometry: line view";return null;}
   if(roi.Contours.Count>2048||roi.Contours.Sum(c=>(long)c.Points.Count)>400000){reason="Contour detail limit: line view";return null;}
   if(roi.Contours.Any(c=>c.GeometricType!="CLOSED_PLANAR"&&c.GeometricType!="CLOSEDPLANAR_XOR")){reason="Open contours: line view";return null;}
   bool xor=roi.Contours.Any(c=>c.GeometricType=="CLOSEDPLANAR_XOR");if(xor&&roi.Contours.Any(c=>c.GeometricType!="CLOSEDPLANAR_XOR")){reason="Mixed contour types: line view";return null;}
   var loops=new List<Loop>();foreach(var contour in roi.Contours){token.ThrowIfCancellationRequested();if(contour.Points.Count<3)continue;var points=new Vec3[contour.Points.Count];for(int i=0;i<points.Length;i++){if((i&1023)==0)token.ThrowIfCancellationRequested();points[i]=transform.Transform(contour.Points[i]);if(!Finite(points[i].X)||!Finite(points[i].Y)||!Finite(points[i].Z)){reason="Nonfinite contour geometry: line view";return null;}}loops.Add(new Loop{Points=points});}
   if(loops.Count==0){reason="No closed surfaces";return null;}
   var first=loops[0].Points;Vec3 u=new Vec3(),normal=new Vec3();bool found=false;
   int baseline=1;while(baseline<first.Length&&(first[baseline]-first[0]).Length<.0001){if((baseline&1023)==0)token.ThrowIfCancellationRequested();baseline++;}
   if(baseline<first.Length)for(int j=baseline+1;j<first.Length&&!found;j++){if((j&1023)==0)token.ThrowIfCancellationRequested();var cross=(first[baseline]-first[0]).Cross(first[j]-first[0]);if(cross.Length>.0001){u=(first[baseline]-first[0]).Normalized();normal=cross.Normalized();found=true;}}
   if(!found){reason="Degenerate contour: line view";return null;}var v=normal.Cross(u).Normalized();
   foreach(var loop in loops){loop.Level=loop.Points[0].Dot(normal);if(loop.Points.Any(p=>Math.Abs(p.Dot(normal)-loop.Level)>.1)){reason="Nonparallel contours: line view";return null;}}
   var groups=new List<List<Loop>>();foreach(var loop in loops.OrderBy(l=>l.Level)){if(groups.Count==0||Math.Abs(groups[groups.Count-1][0].Level-loop.Level)>.1)groups.Add(new List<Loop>());groups[groups.Count-1].Add(loop);}
   if(groups.Count>512){reason="Too many contour planes: line view";return null;}
   if(groups.Count<2){reason="Single contour plane: line view";return null;}
   var levels=groups.Select(g=>g[0].Level).ToArray();var gaps=new List<double>();for(int i=1;i<levels.Length;i++)gaps.Add(levels[i]-levels[i-1]);var sorted=gaps.OrderBy(x=>x).ToArray();double stepZ=sorted[(sorted.Length-1)/2];
   double minU=loops.Min(l=>l.Points.Min(p=>p.Dot(u))),maxU=loops.Max(l=>l.Points.Max(p=>p.Dot(u))),minV=loops.Min(l=>l.Points.Min(p=>p.Dot(v))),maxV=loops.Max(l=>l.Points.Max(p=>p.Dot(v)));
   double margin=physicalSpacing?Math.Max(2,Math.Max(maxU-minU,maxV-minV)/(limit-4)*2):Math.Max(.1,Math.Max(maxU-minU,maxV-minV)/(limit-4));minU-=margin;maxU+=margin;minV-=margin;maxV+=margin;
   double minZ=levels[0]-.5*stepZ-margin,maxZ=levels[levels.Length-1]+.5*stepZ+margin;
   double span=Math.Max(maxU-minU,Math.Max(maxV-minV,maxZ-minZ));int nx=Math.Max(6,Math.Min(limit,(int)Math.Ceiling((maxU-minU)/span*(limit-1))+1)),ny=Math.Max(6,Math.Min(limit,(int)Math.Ceiling((maxV-minV)/span*(limit-1))+1)),nz=Math.Max(6,Math.Min(limit,(int)Math.Ceiling((maxZ-minZ)/span*(limit-1))+1));
   if(physicalSpacing){nx=DetailSamples(maxU-minU,limit,targetSpacing);ny=DetailSamples(maxV-minV,limit,targetSpacing);nz=DetailSamples(maxZ-minZ,limit,targetSpacing);}
   double sx=(maxU-minU)/(nx-1),sy=(maxV-minV)/(ny-1),sz=(maxZ-minZ)/(nz-1);var masks=new List<bool[]>();
   var distanceFields=smoothField?new List<float[]>():null;
   foreach(var group in groups)
   {
    token.ThrowIfCancellationRequested();var mask=new bool[nx*ny];
    foreach(var loop in group)
    {
     var xx=loop.Points.Select(p=>p.Dot(u)).ToArray();var yy=loop.Points.Select(p=>p.Dot(v)).ToArray();var intersections=new List<double>();
     for(int y=0;y<ny;y++)
     {
      token.ThrowIfCancellationRequested();double py=minV+y*sy;intersections.Clear();
      for(int a=0;a<xx.Length;a++){if((a&4095)==0)token.ThrowIfCancellationRequested();int b=(a+1)%xx.Length;if((yy[a]<=py&&yy[b]>py)||(yy[b]<=py&&yy[a]>py))intersections.Add(xx[a]+(py-yy[a])*(xx[b]-xx[a])/(yy[b]-yy[a]));}
      intersections.Sort();for(int k=0;k+1<intersections.Count;k+=2){int begin=Math.Max(0,(int)Math.Ceiling((intersections[k]-minU)/sx)),end=Math.Min(nx-1,(int)Math.Ceiling((intersections[k+1]-minU)/sx)-1);for(int x=begin;x<=end;x++){int index=y*nx+x;mask[index]=xor?!mask[index]:true;}}
     }
    }
    if(smoothField)distanceFields.Add(SignedDistance(mask,nx,ny,sx,sy,token));else masks.Add(mask);
   }
   var fields=smoothField?distanceFields.ToArray():null;
   var volume=new VolumeData{Width=nx,Height=ny,Depth=nz,Origin=u*minU+v*minV+normal*minZ,AxisX=u,AxisY=v,AxisZ=normal,SpacingX=sx,SpacingY=sy,SpacingZ=sz,Values=new float[nx*ny*nz],Min=0,Max=1};
   for(int z=0;z<nz;z++)
   {
    token.ThrowIfCancellationRequested();double pz=minZ+z*sz;int closest=0;for(int i=1;i<levels.Length;i++)if(Math.Abs(levels[i]-pz)<Math.Abs(levels[closest]-pz))closest=i;
    if(!smoothField){if(Math.Abs(pz-levels[closest])>stepZ*.50001)continue;var mask=masks[closest];for(int k=0;k<mask.Length;k++)if(mask[k])volume.Values[z*nx*ny+k]=1;continue;}
    // Interpolate a continuous display distance field only across regular adjacent
    // contour planes. Segmentation masks themselves retain their nearest-plane mode.
    int low=closest,high=closest;if(pz>levels[closest]&&closest+1<levels.Length)high=closest+1;else if(pz<levels[closest]&&closest>0)low=closest-1;
    if(levels[high]-levels[low]>stepZ*1.5)low=high=closest;
    // Small interval variation still has two supporting planes; do not introduce
    // empty slivers by applying a nearest-plane half-interval cut inside that run.
    if(low==high&&Math.Abs(pz-levels[closest])>stepZ*.50001)continue;
    double fraction=low==high?0:(pz-levels[low])/(levels[high]-levels[low]);
    double cap=Math.Min(pz-(levels[0]-.5*stepZ),levels[levels.Length-1]+.5*stepZ-pz);
    // Cap each disconnected contour run as well as the overall first/last plane.
    // Otherwise distance-field interpolation can inflate caps beside a missing run.
    if(closest>0&&levels[closest]-levels[closest-1]>stepZ*1.5)cap=Math.Min(cap,pz-(levels[closest]-.5*stepZ));
    if(closest+1<levels.Length&&levels[closest+1]-levels[closest]>stepZ*1.5)cap=Math.Min(cap,levels[closest]+.5*stepZ-pz);
    for(int k=0;k<nx*ny;k++){double distance=Math.Min(cap,fields[low][k]*(1-fraction)+fields[high][k]*fraction);volume.Values[z*nx*ny+k]=(float)(.5+distance/Math.Max(sx,sy));}
   }
   reason=physicalSpacing?string.Format(System.Globalization.CultureInfo.InvariantCulture,"Contour display surface; sampling {0:0.00}/{1:0.00}/{2:0.00} mm{3}",sx,sy,sz,Math.Max(sx,Math.Max(sy,sz))>targetSpacing*1.01?" (bounded to 256 samples per axis)":""):"Voxelized from contours (bounded resolution)";return volume;
  }
  static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
  static int DetailSamples(double span,int limit,double spacing)=>Math.Max(6,(int)Math.Min(limit,Math.Ceiling(span/spacing)+1));
  static float[] SignedDistance(bool[] mask,int width,int height,double sx,double sy,CancellationToken token)
  {
   var inside=new float[mask.Length];var outside=new float[mask.Length];const float far=1000000;
   for(int i=0;i<mask.Length;i++){inside[i]=mask[i]?0:far;outside[i]=mask[i]?far:0;}
   double diagonal=Math.Sqrt(sx*sx+sy*sy);
   foreach(var field in new[]{inside,outside})for(int pass=0;pass<2;pass++)
   {
    token.ThrowIfCancellationRequested();int direction=pass==0?1:-1;
    for(int y=pass==0?0:height-1;y>=0&&y<height;y+=direction)for(int x=pass==0?0:width-1;x>=0&&x<width;x+=direction)
    {
     int k=y*width+x,previousX=x-direction,previousY=y-direction;
     if(previousX>=0&&previousX<width)field[k]=Math.Min(field[k],(float)(field[y*width+previousX]+sx));
     if(previousY>=0&&previousY<height){field[k]=Math.Min(field[k],(float)(field[previousY*width+x]+sy));for(int dx=-1;dx<=1;dx+=2)if(x+dx>=0&&x+dx<width)field[k]=Math.Min(field[k],(float)(field[previousY*width+x+dx]+diagonal));}
    }
   }
   double half=.5*Math.Min(sx,sy);for(int i=0;i<mask.Length;i++)inside[i]=(float)(mask[i]?outside[i]-half:half-inside[i]);return inside;
  }
  public static ThreeDMeshData ContourLines(StructureRoi roi,Matrix4 transform,CancellationToken token)
  {
   var mesh=new ThreeDMeshData();foreach(var contour in roi.Contours)
   {
    token.ThrowIfCancellationRequested();bool closed=contour.GeometricType=="CLOSED_PLANAR"||contour.GeometricType=="CLOSEDPLANAR_XOR";int count=closed?contour.Points.Count:contour.Points.Count-1;
    for(int i=0;i<count;i++)
    {
     if((i&1023)==0)token.ThrowIfCancellationRequested();var a=transform.Transform(contour.Points[i]);var b=transform.Transform(contour.Points[(i+1)%contour.Points.Count]);var d=b-a;if(d.Length<1e-6)continue;
     var seed=Math.Abs(d.Normalized().Z)<.9?new Vec3(0,0,1):new Vec3(0,1,0);var r=d.Cross(seed).Normalized()*.15;var q=d.Normalized().Cross(r);
     mesh.Triangle(a+r,b+r,b-r);mesh.Triangle(a+r,b-r,a-r);mesh.Triangle(a+q,b+q,b-q);mesh.Triangle(a+q,b-q,a-q);
    }
   }
   return mesh;
  }
 }
}
