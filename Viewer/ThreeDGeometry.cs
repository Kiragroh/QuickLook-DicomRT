using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace QuickLook.DicomRT
{
 public sealed class ThreeDMeshData
 {
  public readonly List<Vec3> Points=new List<Vec3>();public readonly List<int> Indices=new List<int>();
  internal void Triangle(Vec3 a,Vec3 b,Vec3 c)
  {
   if((b-a).Cross(c-a).Length<1e-10)return;
   if(Indices.Count>=360000)throw new InvalidOperationException("3D detail limit reached; lower resolution is required.");
   int k=Points.Count;Points.Add(a);Points.Add(b);Points.Add(c);Indices.Add(k);Indices.Add(k+1);Indices.Add(k+2);
  }
 }
 public static class ThreeDGeometry
 {
  static readonly int[,] Tetra={{0,5,1,6},{0,1,2,6},{0,2,3,6},{0,3,7,6},{0,7,4,6},{0,4,5,6}};
  static readonly int[,] Edge={{0,1},{0,2},{0,3},{1,2},{1,3},{2,3}};
  public static ThreeDMeshData Isosurface(VolumeData bounds,Func<Vec3,float> sample,double level,int limit,CancellationToken token)
  {
   token.ThrowIfCancellationRequested();limit=Math.Max(8,Math.Min(80,limit));
   int nx=Math.Max(2,Math.Min(limit,bounds.Width)),ny=Math.Max(2,Math.Min(limit,bounds.Height)),nz=Math.Max(2,Math.Min(limit,bounds.Depth));
   double sx=(bounds.Width-1.0)/(nx-1),sy=(bounds.Height-1.0)/(ny-1),sz=(bounds.Depth-1.0)/(nz-1);
   var values=new float[nx*ny*nz];
   for(int z=0;z<nz;z++){token.ThrowIfCancellationRequested();for(int y=0;y<ny;y++)for(int x=0;x<nx;x++)values[(z*ny+y)*nx+x]=sample(bounds.WorldAt(x*sx,y*sy,z*sz));}
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
       var center=(intersections[0]+intersections[1]+intersections[2]+intersections[3])/4;var u=(intersections[0]-center).Normalized();var normal=(intersections[1]-intersections[0]).Cross(intersections[2]-intersections[0]).Normalized();var v=normal.Cross(u);
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
  public static VolumeData VoxelizeRoi(StructureRoi roi,Matrix4 transform,int limit,CancellationToken token,out string reason)
  {
   reason=null;limit=Math.Max(8,Math.Min(64,limit));token.ThrowIfCancellationRequested();
   if(roi?.Contours==null||roi.Contours.Count==0){reason="No contours";return null;}
   if(roi.Contours.Count>2048||roi.Contours.Sum(c=>(long)c.Points.Count)>400000){reason="Contour detail limit: line view";return null;}
   if(roi.Contours.Any(c=>c.GeometricType!="CLOSED_PLANAR"&&c.GeometricType!="CLOSEDPLANAR_XOR")){reason="Open contours: line view";return null;}
   bool xor=roi.Contours.Any(c=>c.GeometricType=="CLOSEDPLANAR_XOR");if(xor&&roi.Contours.Any(c=>c.GeometricType!="CLOSEDPLANAR_XOR")){reason="Mixed contour types: line view";return null;}
   var loops=new List<Loop>();foreach(var contour in roi.Contours){token.ThrowIfCancellationRequested();if(contour.Points.Count<3)continue;var points=new Vec3[contour.Points.Count];for(int i=0;i<points.Length;i++){if((i&1023)==0)token.ThrowIfCancellationRequested();points[i]=transform.Transform(contour.Points[i]);}loops.Add(new Loop{Points=points});}
   if(loops.Count==0){reason="No closed surfaces";return null;}
   var first=loops[0].Points;Vec3 u=new Vec3(),normal=new Vec3();bool found=false;
   int baseline=1;while(baseline<first.Length&&(first[baseline]-first[0]).Length<.0001){if((baseline&1023)==0)token.ThrowIfCancellationRequested();baseline++;}
   if(baseline<first.Length)for(int j=baseline+1;j<first.Length&&!found;j++){if((j&1023)==0)token.ThrowIfCancellationRequested();var cross=(first[baseline]-first[0]).Cross(first[j]-first[0]);if(cross.Length>.0001){u=(first[baseline]-first[0]).Normalized();normal=cross.Normalized();found=true;}}
   if(!found){reason="Degenerate contour: line view";return null;}var v=normal.Cross(u).Normalized();
   foreach(var loop in loops){loop.Level=loop.Points[0].Dot(normal);if(loop.Points.Any(p=>Math.Abs(p.Dot(normal)-loop.Level)>.1)){reason="Nonparallel contours: line view";return null;}}
   var groups=new List<List<Loop>>();foreach(var loop in loops.OrderBy(l=>l.Level)){if(groups.Count==0||Math.Abs(groups[groups.Count-1][0].Level-loop.Level)>.1)groups.Add(new List<Loop>());groups[groups.Count-1].Add(loop);}
   if(groups.Count>512){reason="Too many contour planes: line view";return null;}
   if(groups.Count<2){reason="Single contour plane: line view";return null;}
   var levels=groups.Select(g=>g[0].Level).ToArray();var gaps=new List<double>();for(int i=1;i<levels.Length;i++)gaps.Add(levels[i]-levels[i-1]);var sorted=gaps.OrderBy(x=>x).ToArray();double stepZ=sorted[sorted.Length/2];
   double minU=loops.Min(l=>l.Points.Min(p=>p.Dot(u))),maxU=loops.Max(l=>l.Points.Max(p=>p.Dot(u))),minV=loops.Min(l=>l.Points.Min(p=>p.Dot(v))),maxV=loops.Max(l=>l.Points.Max(p=>p.Dot(v)));
   double margin=Math.Max(.1,Math.Max(maxU-minU,maxV-minV)/(limit-4));minU-=margin;maxU+=margin;minV-=margin;maxV+=margin;
   double minZ=levels[0]-.5*stepZ-margin,maxZ=levels[levels.Length-1]+.5*stepZ+margin;
   double span=Math.Max(maxU-minU,Math.Max(maxV-minV,maxZ-minZ));int nx=Math.Max(6,Math.Min(limit,(int)Math.Ceiling((maxU-minU)/span*(limit-1))+1)),ny=Math.Max(6,Math.Min(limit,(int)Math.Ceiling((maxV-minV)/span*(limit-1))+1)),nz=Math.Max(6,Math.Min(limit,(int)Math.Ceiling((maxZ-minZ)/span*(limit-1))+1));
   double sx=(maxU-minU)/(nx-1),sy=(maxV-minV)/(ny-1),sz=(maxZ-minZ)/(nz-1);var masks=new List<bool[]>();
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
    masks.Add(mask);
   }
   var volume=new VolumeData{Width=nx,Height=ny,Depth=nz,Origin=u*minU+v*minV+normal*minZ,AxisX=u,AxisY=v,AxisZ=normal,SpacingX=sx,SpacingY=sy,SpacingZ=sz,Values=new float[nx*ny*nz],Min=0,Max=1};
   for(int z=0;z<nz;z++)
   {
    token.ThrowIfCancellationRequested();double pz=minZ+z*sz;int closest=0;for(int i=1;i<levels.Length;i++)if(Math.Abs(levels[i]-pz)<Math.Abs(levels[closest]-pz))closest=i;
    // Do not bridge absent contour planes across gaps larger than the regular contour interval.
    if(Math.Abs(pz-levels[closest])>stepZ*.50001)continue;var mask=masks[closest];for(int k=0;k<mask.Length;k++)if(mask[k])volume.Values[z*nx*ny+k]=1;
   }
   reason="Voxelized from contours (bounded resolution)";return volume;
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
