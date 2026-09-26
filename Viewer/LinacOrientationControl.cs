using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
namespace QuickLook.DicomRT
{
    // Independent schematic in IEC FIXED coordinates: +Y towards gantry, +Z up.
    // Dimensions are normalized illustration units, not machine or patient geometry.
    internal sealed class LinacOrientationControl : Border
    {
        readonly Viewport3D viewport=new Viewport3D();
        readonly AxisAngleRotation3D gantryRotation=new AxisAngleRotation3D(new Vector3D(0,1,0),0);
        readonly AxisAngleRotation3D couchRotation=new AxisAngleRotation3D(new Vector3D(0,0,1),0);
        readonly TextBlock angles;
        public LinacOrientationControl()
        {
            IsHitTestVisible=false;CornerRadius=new CornerRadius(8);Background=Theme.Brush("#B8111314");Padding=new Thickness(9,5,9,5);
            var root=new Grid();root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Child=root;
            angles=Theme.Text("LINAC · IEC",10,Theme.Muted);root.Children.Add(angles);Grid.SetRow(viewport,1);root.Children.Add(viewport);
            var note=Theme.Text("Schematic · patient orientation not registered",8,Theme.Muted);note.Margin=new Thickness(0);Grid.SetRow(note,2);root.Children.Add(note);
            viewport.Camera=new OrthographicCamera(new Point3D(4,-6,3.2),new Vector3D(-4,6.2,-3.25),new Vector3D(0,0,1),3.7){NearPlaneDistance=.1,FarPlaneDistance=50};
            var scene=new Model3DGroup();scene.Children.Add(new AmbientLight(Color.FromRgb(100,109,117)));
            scene.Children.Add(new DirectionalLight(Color.FromRgb(235,242,250),new Vector3D(-.5,1,-2)));
            scene.Children.Add(new DirectionalLight(Color.FromRgb(99,126,146),new Vector3D(1,-.3,-.2)));
            System.Windows.Media.Media3D.Material steel=Material("#607484"),edge=Material("#8C9DA9"),dark=Material("#293943"),couch=Material("#38515A"),body=Material("#D9D2C0"),accent=Material("#70B6C9"),gold=Material("#F2CB6A");
            // Fixed rear support and ground plinth.
            Box(scene,new Point3D(0,1.28,-1.24),.94,.73,.14,dark);
            Box(scene,new Point3D(0,1.34,-.65),.56,.50,1.08,steel);
            Box(scene,new Point3D(0,1.05,-.26),.67,.12,.53,edge);
            Sphere(scene,new Point3D(0,1.23,0),.33,.18,.33,dark);
            var gantry=new Model3DGroup{Transform=new RotateTransform3D(gantryRotation)};scene.Children.Add(gantry);
            Box(gantry,new Point3D(0,1.28,.51),.39,.35,1.16,steel);
            Box(gantry,new Point3D(0,.69,1.06),.46,1.28,.35,steel);
            Box(gantry,new Point3D(0,.17,1.025),.62,.57,.44,edge);
            Box(gantry,new Point3D(0,-.02,.86),.41,.28,.17,dark);
            Box(gantry,new Point3D(0,-.02,.765),.28,.22,.035,accent);
            // Translucent source cone is purely an orientation cue, not a field simulation.
            var beam=Material("#467CA18D",false);Cone(gantry,new Point3D(0,-.02,.75),new Point3D(0,0,0),.11,beam);
            for(int i=0;i<36;i++)
            {double a=i*Math.PI/18,b=a+.080;Rod(scene,new Point3D(1.16*Math.Sin(a),0,1.16*Math.Cos(a)),new Point3D(1.16*Math.Sin(b),0,1.16*Math.Cos(b)),.009,accent);}
            var support=new Model3DGroup{Transform=new RotateTransform3D(couchRotation)};scene.Children.Add(support);
            Box(support,new Point3D(0,-.45,-.20),.58,2.30,.12,couch);
            Box(support,new Point3D(0,-.45,-.13),.55,2.28,.035,dark);
            Box(support,new Point3D(0,-1.13,-.80),.34,.38,1.08,steel);
            Box(support,new Point3D(0,-1.13,-1.25),.76,.72,.13,dark);
            Box(support,new Point3D(0,.51,-.075),.36,.30,.075,Material("#889FA4"));
            Sphere(support,new Point3D(0,.57,.075),.12,.15,.14,body);
            Sphere(support,new Point3D(0,.62,.18),.026,.038,.022,body);
            Sphere(support,new Point3D(0,.09,.018),.20,.37,.12,body);
            Sphere(support,new Point3D(-.09,-.51,-.023),.081,.37,.071,body);Sphere(support,new Point3D(.09,-.51,-.023),.081,.37,.071,body);
            Sphere(support,new Point3D(-.235,.035,-.025),.055,.31,.057,body);Sphere(support,new Point3D(.235,.035,-.025),.055,.31,.057,body);
            Sphere(support,new Point3D(-.09,-.88,-.008),.08,.12,.071,body);Sphere(support,new Point3D(.09,-.88,-.008),.08,.12,.071,body);
            foreach(var axis in new[]{new Vector3D(.105,0,0),new Vector3D(0,.105,0),new Vector3D(0,0,.105)})Rod(scene,new Point3D()-axis,new Point3D()+axis,.011,gold);
            viewport.Children.Add(new ModelVisual3D{Content=scene});
        }
        public void Set(double gantry,double couch)
        {
            bool valid=!(double.IsNaN(gantry)||double.IsInfinity(gantry)||double.IsNaN(couch)||double.IsInfinity(couch));
            viewport.Visibility=valid?Visibility.Visible:Visibility.Hidden;
            angles.Text=valid?string.Format(CultureInfo.InvariantCulture,"GANTRY {0:0.0}°  ·  COUCH {1:0.0}°",gantry,couch):"Orientation angles unavailable";
            if(valid){gantryRotation.Angle=gantry;couchRotation.Angle=couch;}
        }
        static Material Material(string hex,bool specular=true)
        {
            var brush=Theme.Brush(hex);var group=new MaterialGroup();group.Children.Add(new DiffuseMaterial(brush));
            if(specular)group.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(110,214,230,240)),32));group.Freeze();return group;
        }
        static void Add(Model3DGroup group,MeshGeometry3D mesh,Material material){mesh.Freeze();group.Children.Add(new GeometryModel3D(mesh,material){BackMaterial=material});}
        static void Box(Model3DGroup group,Point3D center,double width,double length,double height,Material material)
        {
            var p=new[]{new Point3D(-width/2,-length/2,-height/2),new Point3D(width/2,-length/2,-height/2),new Point3D(width/2,length/2,-height/2),new Point3D(-width/2,length/2,-height/2),new Point3D(-width/2,-length/2,height/2),new Point3D(width/2,-length/2,height/2),new Point3D(width/2,length/2,height/2),new Point3D(-width/2,length/2,height/2)};
            var mesh=new MeshGeometry3D();foreach(var face in new[]{new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,1,5,4},new[]{1,2,6,5},new[]{2,3,7,6},new[]{3,0,4,7}})
            {int start=mesh.Positions.Count;foreach(int index in face)mesh.Positions.Add(p[index]+(Vector3D)center);foreach(int index in new[]{0,1,2,0,2,3})mesh.TriangleIndices.Add(start+index);}Add(group,mesh,material);
        }
        static void Sphere(Model3DGroup group,Point3D center,double x,double y,double z,Material material)
        {
            var mesh=new MeshGeometry3D();const int rings=10,sides=16;
            for(int j=0;j<=rings;j++){double v=Math.PI*j/rings;for(int i=0;i<=sides;i++){double u=2*Math.PI*i/sides;mesh.Positions.Add(new Point3D(center.X+x*Math.Sin(v)*Math.Cos(u),center.Y+y*Math.Sin(v)*Math.Sin(u),center.Z+z*Math.Cos(v)));var normal=new Vector3D(Math.Sin(v)*Math.Cos(u)/x,Math.Sin(v)*Math.Sin(u)/y,Math.Cos(v)/z);normal.Normalize();mesh.Normals.Add(normal);}}
            for(int j=0;j<rings;j++)for(int i=0;i<sides;i++){int a=j*(sides+1)+i,b=a+sides+1;foreach(int k in new[]{a,b,a+1,a+1,b,b+1})mesh.TriangleIndices.Add(k);}Add(group,mesh,material);
        }
        static void Rod(Model3DGroup group,Point3D a,Point3D b,double radius,Material material)=>Cylinder(group,a,b,radius,radius,material);
        static void Cone(Model3DGroup group,Point3D a,Point3D b,double radius,Material material)=>Cylinder(group,a,b,radius,.015,material);
        static void Cylinder(Model3DGroup group,Point3D a,Point3D b,double ra,double rb,Material material)
        {
            var direction=b-a;direction.Normalize();var u=Vector3D.CrossProduct(direction,Math.Abs(direction.Z)<.9?new Vector3D(0,0,1):new Vector3D(0,1,0));u.Normalize();var v=Vector3D.CrossProduct(direction,u);var mesh=new MeshGeometry3D();const int sides=10;
            for(int i=0;i<sides;i++){double angle=2*Math.PI*i/sides;var offset=u*Math.Cos(angle)+v*Math.Sin(angle);mesh.Positions.Add(a+offset*ra);mesh.Positions.Add(b+offset*rb);}
            for(int i=0;i<sides;i++){int j=(i+1)%sides;foreach(int k in new[]{i*2,j*2,i*2+1,i*2+1,j*2,j*2+1})mesh.TriangleIndices.Add(k);}Add(group,mesh,material);
        }
    }
}
