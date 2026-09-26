using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QuickLook.DicomRT
{
    internal sealed class ProjectedOutline {public StructureRoi Roi;public Geometry Boundary;}
    internal sealed class MlcProjectionFrame
    {
        public BitmapSource Drr;public double Extent;public string Note;
        public readonly List<ProjectedOutline> Outlines=new List<ProjectedOutline>();
    }
    // Accessed by a single background worker. Original ROI-space meshes survive CP changes.
    internal sealed class MlcProjectionRenderer
    {
        readonly Dictionary<StructureRoi,ThreeDMeshData> meshes=new Dictionary<StructureRoi,ThreeDMeshData>();
        readonly Queue<StructureRoi> order=new Queue<StructureRoi>();
        long vertices;
        public MlcProjectionFrame Render(BeamProjection projection,VolumeData ct,RoiOverlay[] rois,double extent,int size,bool drr,CancellationToken token,int parallelism=4)
        {
            var result=new MlcProjectionFrame{Extent=extent};
            if(drr&&ct!=null)
            {
                var values=projection.Integrate(ct,extent,size,size<300?2:1,token,parallelism);
                var sorted=values.Where(v=>v>0).OrderBy(v=>v).ToArray();double high=sorted.Length==0?1:Math.Max(1,sorted[(int)((sorted.Length-1)*.99)]);
                var pixels=new byte[size*size];for(int i=0;i<values.Length;i++)pixels[i]=(byte)(255*Math.Pow(Math.Min(1,values[i]/high),.8));
                result.Drr=BitmapSource.Create(size,size,96,96,PixelFormats.Gray8,null,pixels,size);result.Drr.Freeze();
            }
            int skipped=0;
            foreach(var overlay in rois)
            {
                token.ThrowIfCancellationRequested();ThreeDMeshData mesh;
                if(!meshes.TryGetValue(overlay.Roi,out mesh))
                {
                    string reason;mesh=ThreeDGeometry.BuildRoiSurface(overlay.Roi,Matrix4.Identity,token,out reason);
                    if(mesh!=null){while(order.Count>0&&(meshes.Count>=48||vertices+mesh.Points.Count>3000000)){var old=order.Dequeue();vertices-=meshes[old].Points.Count;meshes.Remove(old);}meshes[overlay.Roi]=mesh;vertices+=mesh.Points.Count;order.Enqueue(overlay.Roi);}
                }
                if(mesh==null){skipped++;continue;}
                var boundary=Silhouette(mesh,overlay.RoiToImage,projection,extent,512,token);
                result.Outlines.Add(new ProjectedOutline{Roi=overlay.Roi,Boundary=boundary});
            }
            result.Note=(result.Drr!=null?"CT-derived DRR · perspective at ISO":"DRR off / matching CT unavailable")+" · "+result.Outlines.Count+" outlines"+(skipped>0?" · "+skipped+" unsupported contours":"");
            return result;
        }
        // Union of projected surface triangles, not a convex hull and not individual contour slices.
        // The boundary is normalized to [0,1], retaining separate targets and concave silhouettes.
        internal static Geometry Silhouette(ThreeDMeshData mesh,Matrix4 transform,BeamProjection projection,double extent,int size,CancellationToken token)
        {
            var px=new double[mesh.Points.Count];var py=new double[px.Length];
            for(int i=0;i<px.Length;i++){
                if((i&4095)==0)token.ThrowIfCancellationRequested();double x,y;
                bool ok=projection.Project(transform.Transform(mesh.Points[i]),out x,out y);
                px[i]=ok?(x/extent+1)*size*.5:double.NaN;py[i]=ok?(1-y/extent)*size*.5:double.NaN;
            }
            var mask=new bool[size*size];
            for(int i=0;i<mesh.Indices.Count;i+=3)
            {
                if((i&3071)==0)token.ThrowIfCancellationRequested();int a=mesh.Indices[i],b=mesh.Indices[i+1],c=mesh.Indices[i+2];
                if(double.IsNaN(px[a])||double.IsNaN(px[b])||double.IsNaN(px[c]))continue;
                double ax=px[a],ay=py[a],bx=px[b],by=py[b],cx=px[c],cy=py[c];double area=(bx-ax)*(cy-ay)-(by-ay)*(cx-ax);
                if(Math.Abs(area)<1e-12)continue;
                int minX=(int)Math.Max(0,Math.Floor(Math.Min(ax,Math.Min(bx,cx)))),maxX=(int)Math.Min(size-1,Math.Ceiling(Math.Max(ax,Math.Max(bx,cx))));
                int minY=(int)Math.Max(0,Math.Floor(Math.Min(ay,Math.Min(by,cy)))),maxY=(int)Math.Min(size-1,Math.Ceiling(Math.Max(ay,Math.Max(by,cy))));
                for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
                {
                    if(mask[y*size+x])continue;double dx=x+.5,dy=y+.5;
                    double u=((bx-dx)*(cy-dy)-(by-dy)*(cx-dx))/area;
                    double v=((cx-dx)*(ay-dy)-(cy-dy)*(ax-dx))/area;
                    if(u>=-1e-8&&v>=-1e-8&&u+v<=1+1e-8)mask[y*size+x]=true;
                }
            }
            var geometry=new StreamGeometry();using(var context=geometry.Open())
            {
                Action<int,int,int,int> edge=(x,y,xx,yy)=>{context.BeginFigure(new Point(x/(double)size,y/(double)size),false,false);context.LineTo(new Point(xx/(double)size,yy/(double)size),true,false);};
                for(int y=0;y<size;y++){token.ThrowIfCancellationRequested();for(int x=0;x<size;x++)if(mask[y*size+x]){
                    if(x==0||!mask[y*size+x-1])edge(x,y,x,y+1);if(x==size-1||!mask[y*size+x+1])edge(x+1,y,x+1,y+1);
                    if(y==0||!mask[(y-1)*size+x])edge(x,y,x+1,y);if(y==size-1||!mask[(y+1)*size+x])edge(x,y+1,x+1,y+1);
                }}
            }geometry.Freeze();return geometry;
        }
    }
}
