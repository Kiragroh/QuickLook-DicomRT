using System;
using System.Globalization;
using System.Linq;
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
        readonly TextBlock angles,note;
        readonly Model3DGroup patientHost=new Model3DGroup();
        readonly Model3DGroup couchTop=new Model3DGroup();
        readonly CollimatorIndicator collimator=new CollimatorIndicator();
        readonly OrientationAvatarPreferences avatarPreferences=OrientationAvatarPreferences.Current;
        PlanBeam contextBeam;string contextRegion,contextPosition;bool contextSet;bool? contextNoncoplanar;
        public LinacOrientationControl()
        {
            System.ComponentModel.PropertyChangedEventManager.AddHandler(avatarPreferences,AvatarChanged,"Selected");
            IsHitTestVisible=false;CornerRadius=new CornerRadius(8);Background=Theme.Brush("#B8111314");Padding=new Thickness(9,5,9,5);
            var root=new Grid();root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Child=root;
            angles=Theme.Text("LINAC · IEC",10,Theme.Muted);var heading=new StackPanel();heading.Children.Add(angles);heading.Children.Add(collimator);root.Children.Add(heading);Grid.SetRow(viewport,1);root.Children.Add(viewport);
            var footer=new StackPanel();Grid.SetRow(footer,2);root.Children.Add(footer);
            var legend=new TextBlock{FontSize=8,HorizontalAlignment=HorizontalAlignment.Center};
            legend.Inlines.Add(new System.Windows.Documents.Run("L hand / foot"){Foreground=Theme.Brush(PatientOrientationGlyph.LeftColor)});
            legend.Inlines.Add(new System.Windows.Documents.Run("   ·   "){Foreground=Theme.Muted});
            legend.Inlines.Add(new System.Windows.Documents.Run("R hand / foot"){Foreground=Theme.Brush(PatientOrientationGlyph.RightColor)});footer.Children.Add(legend);
            note=Theme.Text("Schematic · patient position unavailable",8,Theme.Muted);note.TextWrapping=TextWrapping.Wrap;note.Margin=new Thickness(0);footer.Children.Add(note);
            viewport.Camera=new OrthographicCamera(new Point3D(4,-6,3.2),new Vector3D(-4,6.2,-3.25),new Vector3D(0,0,1),4.2){NearPlaneDistance=.1,FarPlaneDistance=50};
            var scene=new Model3DGroup();scene.Children.Add(new AmbientLight(Color.FromRgb(100,109,117)));
            scene.Children.Add(new DirectionalLight(Color.FromRgb(235,242,250),new Vector3D(-.5,1,-2)));
            scene.Children.Add(new DirectionalLight(Color.FromRgb(99,126,146),new Vector3D(1,-.3,-.2)));
            System.Windows.Media.Media3D.Material steel=Material("#607484"),edge=Material("#8C9DA9"),dark=Material("#293943"),accent=Material("#70B6C9"),gold=Material("#F2CB6A");
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
            // Dashed central ray passes exactly through the isocenter; no field cone.
            var beam=Material("#FFE16B",false);
            for(double z=-.18;z<.75;z+=.105)Rod(gantry,new Point3D(0,0,z),new Point3D(0,0,Math.Min(z+.060,.75)),.012,beam);
            for(int i=0;i<36;i++)
            {double a=i*Math.PI/18,b=a+.080;Rod(scene,new Point3D(1.16*Math.Sin(a),0,1.16*Math.Cos(a)),new Point3D(1.16*Math.Sin(b),0,1.16*Math.Cos(b)),.009,accent);}
            var support=new Model3DGroup{Transform=new RotateTransform3D(couchRotation)};scene.Children.Add(support);
            support.Children.Add(couchTop);BuildCouch(-1.05,.7);
            support.Children.Add(patientHost);
            foreach(var axis in new[]{new Vector3D(.105,0,0),new Vector3D(0,.105,0),new Vector3D(0,0,.105)})Rod(scene,new Point3D()-axis,new Point3D()+axis,.011,gold);
            viewport.Children.Add(new ModelVisual3D{Content=scene});
        }
        void AvatarChanged(object sender,System.ComponentModel.PropertyChangedEventArgs e){if(!contextSet)return;contextSet=false;SetContext(contextBeam,contextRegion,contextNoncoplanar);}
        public void SetContext(PlanBeam beam,string bodyRegion=null,bool? planNoncoplanar=null)
        {
            if(contextSet&&ReferenceEquals(contextBeam,beam)&&contextRegion==bodyRegion&&contextPosition==beam?.PatientPosition&&contextNoncoplanar==planNoncoplanar)return;
            contextSet=true;contextBeam=beam;contextRegion=bodyRegion;contextPosition=beam?.PatientPosition;contextNoncoplanar=planNoncoplanar;
            patientHost.Children.Clear();var orientation=PatientOrientation.ToIec(beam?.PatientPosition);
            if(orientation==null){BuildCouch(-1.05,.7);note.Text="Schematic · patient position unavailable / unsupported";return;}
            bool noncoplanar=planNoncoplanar??beam.ControlPoints.Any(c=>!double.IsNaN(c.Couch)&&!double.IsInfinity(c.Couch)&&Math.Abs(Math.Sin(c.Couch*Math.PI/180))>.01);
            string cue;double anchor=PatientOrientation.SchematicAnchor(bodyRegion,noncoplanar,out cue);
            var head=orientation.Transform(new Vec3(0,0,.70-anchor));var feet=orientation.Transform(new Vec3(0,0,-1.05-anchor));BuildCouch(Math.Min(head.Y,feet.Y),Math.Max(head.Y,feet.Y));
            var patient=PatientOrientationGlyph.Create();var transform=new Transform3DGroup();
            transform.Children.Add(new TranslateTransform3D(0,0,-anchor));
            var m=orientation.Values;
            transform.Children.Add(new MatrixTransform3D(new Matrix3D(m[0],m[4],m[8],0,m[1],m[5],m[9],0,m[2],m[6],m[10],0,0,0,0,1)));
            patient.Transform=transform;patientHost.Children.Add(patient);
            note.Text="Schematic · "+beam.PatientPosition+" · "+cue;
        }
        void BuildCouch(double low,double high)
        {
            couchTop.Children.Clear();double middle=(low+high)/2,length=high-low;
            Box(couchTop,new Point3D(0,middle,-.20),.65,length,.12,Material("#38515A"));
            Box(couchTop,new Point3D(0,middle,-.13),.62,length,.035,Material("#293943"));
            Box(couchTop,new Point3D(0,middle,-.80),.34,.38,1.08,Material("#607484"));
            Box(couchTop,new Point3D(0,middle,-1.25),.76,.72,.13,Material("#293943"));
        }
        public void SetCollimator(double angle)=>collimator.Angle=angle;
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
        static void Cylinder(Model3DGroup group,Point3D a,Point3D b,double ra,double rb,Material material)
        {
            var direction=b-a;direction.Normalize();var u=Vector3D.CrossProduct(direction,Math.Abs(direction.Z)<.9?new Vector3D(0,0,1):new Vector3D(0,1,0));u.Normalize();var v=Vector3D.CrossProduct(direction,u);var mesh=new MeshGeometry3D();const int sides=10;
            for(int i=0;i<sides;i++){double angle=2*Math.PI*i/sides;var offset=u*Math.Cos(angle)+v*Math.Sin(angle);mesh.Positions.Add(a+offset*ra);mesh.Positions.Add(b+offset*rb);}
            for(int i=0;i<sides;i++){int j=(i+1)%sides;foreach(int k in new[]{i*2,j*2,i*2+1,i*2+1,j*2,j*2+1})mesh.TriangleIndices.Add(k);}Add(group,mesh,material);
        }
    }
}
