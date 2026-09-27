using System;
using System.Collections.Generic;
using System.Linq;
namespace QuickLook.DicomRT
{
    public sealed class BeamBlock
    {
        public string Type {get;private set;}
        public Vec3[] Outline {get;private set;}
        public Vec3[][] Triangles {get;private set;}
        // PS3.3 C.8.8.14: BlockData is already projected to the machine
        // isocentric plane in IEC BLD mm. Never apply tray-distance scaling again.
        public static BeamBlock Create(string type,double[] data,int count)
        {
            if((type!="APERTURE"&&type!="SHIELDING")||count<3||data.Length!=2*count||data.Any(x=>!BeamProjection.Finite(x)))throw new ArgumentException("Unsupported or missing block contour.");
            var points=new List<Vec3>();for(int i=0;i<count;i++){var p=new Vec3(data[2*i],data[2*i+1],0);if(points.Count==0||(points[points.Count-1]-p).Length>1e-7)points.Add(p);}
            if(points.Count>1&&(points[0]-points[points.Count-1]).Length<1e-7)points.RemoveAt(points.Count-1);
            double area=0;for(int i=0;i<points.Count;i++)area+=Cross(points[i],points[(i+1)%points.Count]);if(Math.Abs(area)<1e-8)throw new ArgumentException("Degenerate block contour.");if(area<0)points.Reverse();
            var outline=points.ToArray();var triangles=new List<Vec3[]>();
            while(points.Count>3){bool found=false;for(int i=0;i<points.Count;i++){
                var a=points[(i+points.Count-1)%points.Count];var b=points[i];var c=points[(i+1)%points.Count];double turn=Cross(b-a,c-b);
                if(Math.Abs(turn)<1e-9){points.RemoveAt(i);found=true;break;}if(turn<0)continue;
                if(points.Any(p=>(p-a).Length>1e-7&&(p-b).Length>1e-7&&(p-c).Length>1e-7&&Cross(b-a,p-a)>=-1e-9&&Cross(c-b,p-b)>=-1e-9&&Cross(a-c,p-c)>=-1e-9))continue;
                triangles.Add(new[]{a,b,c});points.RemoveAt(i);found=true;break;
            }if(!found)throw new ArgumentException("Block contour cannot be triangulated.");}
            if(points.Count==3)triangles.Add(points.ToArray());
            return new BeamBlock{Type=type,Outline=outline,Triangles=triangles.ToArray()};
        }
        static double Cross(Vec3 a,Vec3 b)=>a.X*b.Y-a.Y*b.X;
    }
}
