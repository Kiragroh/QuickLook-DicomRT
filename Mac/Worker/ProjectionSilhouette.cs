// Same triangle-union raster and edge tracing as Viewer/MlcProjectionRenderer.cs.
using QuickLook.DicomRT;
static class ProjectionSilhouette {
 public static double[][][] Build(ThreeDMeshData mesh,Matrix4 transform,BeamProjection projection,double extent,int size,CancellationToken token) {
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
            // Trace the same raster edges into polylines. One figure per pixel edge
            // wasted most of the contour cache on WPF objects, particularly for organs.
            // Vertices stay on the exact same pixel boundaries; no hull or smoothing.
            var edges=new Dictionary<int,List<int>>();int stride=size+1;
            Action<int,int,int,int> edge=(x,y,xx,yy)=>{int start=y*stride+x;List<int> ends;if(!edges.TryGetValue(start,out ends))edges[start]=ends=new List<int>(1);ends.Add(yy*stride+xx);};
            for(int y=0;y<size;y++){token.ThrowIfCancellationRequested();for(int x=0;x<size;x++)if(mask[y*size+x]){
                if(x==0||!mask[y*size+x-1])edge(x,y,x,y+1);if(x==size-1||!mask[y*size+x+1])edge(x+1,y+1,x+1,y);
                if(y==0||!mask[(y-1)*size+x])edge(x+1,y,x,y);if(y==size-1||!mask[(y+1)*size+x])edge(x,y+1,x+1,y+1);
            }}
            var result=new List<double[][]>();
            foreach(int start in edges.Keys.ToArray())while(edges[start].Count>0) {
                token.ThrowIfCancellationRequested();var points=new List<Vec3>();int at=start;
                do {points.Add(new Vec3(at%stride/(double)size,at/stride/(double)size,0));var ends=edges[at];if(ends.Count==0)break;int next=ends[ends.Count-1];ends.RemoveAt(ends.Count-1);at=next;}while(at!=start);
                bool closed=at==start;var simplified=new List<double[]>();
                for(int i=0;i<points.Count;i++){if(!closed&&(i==0||i==points.Count-1)){simplified.Add(new[]{points[i].X,points[i].Y});continue;}var u=points[i]-points[(i+points.Count-1)%points.Count];var v=points[(i+1)%points.Count]-points[i];if(Math.Abs(u.X*v.Y-u.Y*v.X)>1e-15)simplified.Add(new[]{points[i].X,points[i].Y});}
                if(simplified.Count<2)continue;if(closed)simplified.Add(simplified[0]);result.Add(simplified.ToArray());
            }
            return result.ToArray();
 }
}
