using System;
using System.Windows.Media;
using System.Windows.Media.Media3D;
namespace QuickLook.DicomRT
{
    // Shared normalized human in DICOM LPS: +X left, +Y posterior, +Z superior.
    // Chest at origin; this is a generic orientation cue, never patient anatomy.
    internal static partial class PatientOrientationGlyph
    {
        public const string LeftColor="#53C9CF",RightColor="#F0A15E";
        private static Model3DGroup CreateHuman()
        {
            var model=new Model3DGroup();var body=Material("#D9D2C0");
            Ellipsoid(model,new Point3D(0,0,.55),.12,.12,.15,body);
            Ellipsoid(model,new Point3D(0,-.122,.56),.025,.032,.026,body); // Nose marks anterior.
            Ellipsoid(model,new Point3D(0,0,.36),.055,.058,.095,body);
            Ellipsoid(model,new Point3D(0,0,.10),.185,.10,.26,body);
            Ellipsoid(model,new Point3D(0,0,-.17),.145,.10,.18,body);
            foreach(int side in new[]{-1,1})
            {
                var accent=Material(side>0?LeftColor:RightColor);
                Ellipsoid(model,new Point3D(side*.20,0,.13),.061,.067,.17,body);
                Ellipsoid(model,new Point3D(side*.245,0,-.08),.046,.050,.14,body);
                Ellipsoid(model,new Point3D(side*.25,-.008,-.25),.044,.038,.075,accent);
                Ellipsoid(model,new Point3D(side*.083,0,-.43),.077,.075,.24,body);
                Ellipsoid(model,new Point3D(side*.087,0,-.72),.058,.057,.18,body);
                Ellipsoid(model,new Point3D(side*.087,-.047,-.91),.065,.11,.057,accent);
            }
            return model;
        }
        static Material Material(string hex)
        {
            var group=new MaterialGroup();group.Children.Add(new DiffuseMaterial(Theme.Brush(hex)));
            group.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(75,245,245,240)),24));group.Freeze();return group;
        }
        static void Ellipsoid(Model3DGroup group,Point3D center,double x,double y,double z,Material material)
        {
            var mesh=new MeshGeometry3D();const int rings=12,sides=20;
            for(int j=0;j<=rings;j++)for(int i=0;i<=sides;i++)
            {
                double v=Math.PI*j/rings,u=2*Math.PI*i/sides;
                mesh.Positions.Add(new Point3D(center.X+x*Math.Sin(v)*Math.Cos(u),center.Y+y*Math.Sin(v)*Math.Sin(u),center.Z+z*Math.Cos(v)));
                var normal=new Vector3D(Math.Sin(v)*Math.Cos(u)/x,Math.Sin(v)*Math.Sin(u)/y,Math.Cos(v)/z);normal.Normalize();mesh.Normals.Add(normal);
            }
            for(int j=0;j<rings;j++)for(int i=0;i<sides;i++)
            {int a=j*(sides+1)+i,b=a+sides+1;foreach(int k in new[]{a,b,a+1,a+1,b,b+1})mesh.TriangleIndices.Add(k);}
            mesh.Freeze();group.Children.Add(new GeometryModel3D(mesh,material){BackMaterial=material});
        }
    }
}
