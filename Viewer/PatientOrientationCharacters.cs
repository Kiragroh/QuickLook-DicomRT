using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Media3D;
namespace QuickLook.DicomRT {
 internal static partial class PatientOrientationGlyph {
  static readonly Dictionary<string,Model3DGroup> models=new Dictionary<string,Model3DGroup>();
  public static Model3DGroup Create()=>Create(OrientationAvatarPreferences.Current.Selected);
  internal static Model3DGroup Create(string id){
   Model3DGroup cached;if(!models.TryGetValue(id,out cached)){cached=id=="human"?CreateHuman():Character(id);cached.Freeze();models[id]=cached;}
   return new Model3DGroup{Children={cached}};
  }
  static Model3DGroup Character(string id){
   var g=new Model3DGroup();bool ice=id=="elsa",alien=id=="frieza",guardian=id=="obelisk";
   var skin=Material(guardian?"#287EB8":alien?"#EEEFF8":"#F1CAB0");var suit=Material(guardian?"#1670AF":alien?"#EEEFF8":ice?"#50CBED":"#F8D438");
   var purple=Material("#8249BC");var dark=Material("#203648");var hair=Material("#FAE9BA");var red=Material("#CE343B");var silver=Material("#83CEF2");
   double broad=guardian?1.35:ice?.83:1;
   Ellipsoid(g,new Point3D(0,0,.55),.12,.115,.15,skin);Ellipsoid(g,new Point3D(0,-.116,.54),.022,.027,.025,skin);
   Ellipsoid(g,new Point3D(0,0,.36),.053,.056,.092,skin);
   Ellipsoid(g,new Point3D(0,0,.10),.18*broad,.11,.25,suit);Ellipsoid(g,new Point3D(0,0,-.18),.14*broad,.10,.15,suit);
   foreach(int side in new[]{-1,1}){
    double x=side*.205*broad;var hand=alien||guardian||ice?skin:red;
    Ellipsoid(g,new Point3D(x,0,.12),.06*broad,.067,.16,suit);Ellipsoid(g,new Point3D(side*.25*broad,0,-.075),.046*broad,.055,.14,ice?skin:suit);
    Ellipsoid(g,new Point3D(side*.25*broad,-.01,-.25),.05*broad,.045,.077,hand);
    Ellipsoid(g,new Point3D(side*.084,0,-.43),.074,.075,.24,suit);Ellipsoid(g,new Point3D(side*.087,0,-.73),.058,.06,.19,ice?silver:alien||guardian?skin:red);
    Ellipsoid(g,new Point3D(side*.087,-.05,-.91),.064,.11,.06,ice?silver:alien||guardian?skin:red);
    // Fixed anatomical L/R cuffs and shoe-tip markers survive every costume.
    var accent=Material(side>0?LeftColor:RightColor);
    Ellipsoid(g,new Point3D(side*.25*broad,-.002,-.20),.054*broad,.052,.022,accent);Ellipsoid(g,new Point3D(side*.087,-.13,-.91),.059,.037,.027,accent);
    Ellipsoid(g,new Point3D(side*.047,-.108,.58),.033,.016,.019,Material("#FAFAFA"));Ellipsoid(g,new Point3D(side*.047,-.124,.58),.011,.007,.013,Material(guardian?"#FF3448":alien?"#C42A62":"#285989"));
   }
   if(alien){
    Ellipsoid(g,new Point3D(0,.001,.645),.099,.10,.059,purple);Ellipsoid(g,new Point3D(0,-.108,.13),.10,.025,.115,purple);
    foreach(int side in new[]{-1,1}){Ellipsoid(g,new Point3D(side*.19,-.025,.225),.076,.065,.07,purple);Ellipsoid(g,new Point3D(side*.117,.008,.555),.037,.044,.075,skin);}
    var tail=new[]{new Point3D(0,.08,-.2),new Point3D(0,.22,-.30),new Point3D(.17,.28,-.47),new Point3D(.34,.18,-.62),new Point3D(.40,-.02,-.67),new Point3D(.34,-.17,-.64)};
    for(int i=1;i<tail.Length;i++)Tube(g,tail[i-1],tail[i],.066-i*.009,.057-i*.009,i==tail.Length-1?purple:skin);
   }else if(ice){
    Cone(g,new Point3D(0,0,-.85),new Point3D(0,0,-.12),.29,.13,suit);
    Ellipsoid(g,new Point3D(0,.034,.615),.125,.108,.105,hair);Ellipsoid(g,new Point3D(-.067,-.087,.653),.077,.045,.048,hair);
    for(int i=0;i<8;i++)Ellipsoid(g,new Point3D(.115+i*.003,-.07-i*.011,.48-i*.067),.035-i*.0017,.032,.048,hair);
    Panel(g,new[]{new Point3D(-.14,.075,.27),new Point3D(.14,.075,.27),new Point3D(.42,.22,-.90),new Point3D(-.42,.22,-.90)},Material("#A3DCF3"));
    foreach(int side in new[]{-1,1})Tube(g,new Point3D(side*.12,-.095,.25),new Point3D(0,-.122,.07),.009,.007,silver);
   }else if(guardian){
    foreach(int side in new[]{-1,1}){
     Panel(g,new[]{new Point3D(side*.018,-.14,.28),new Point3D(side*.22,-.09,.25),new Point3D(side*.20,-.16,.11),new Point3D(side*.03,-.17,.075)},silver);
     for(int i=0;i<3;i++)Ellipsoid(g,new Point3D(side*.073,-.10,.04-i*.065),.075,.025,.045,Material(i%2==0?"#478FBF":"#236394"));
     Cone(g,new Point3D(side*.085,.01,.64),new Point3D(side*.17,.015,.83),.045,.004,silver);
     Panel(g,new[]{new Point3D(side*.18,.065,.24),new Point3D(side*.54,.13,.76),new Point3D(side*.69,.15,.40),new Point3D(side*.50,.15,-.18),new Point3D(side*.34,.12,-.37)},suit);
     Tube(g,new Point3D(side*.18,.05,.24),new Point3D(side*.54,.12,.76),.025,.016,silver);
     Tube(g,new Point3D(side*.54,.12,.76),new Point3D(side*.50,.13,-.18),.016,.010,silver);
     Cone(g,new Point3D(side*.27,0,.24),new Point3D(side*.39,0,.36),.07,.002,silver);
     for(int i=0;i<3;i++)Cone(g,new Point3D(side*(.30+i*.027),-.04,-.28),new Point3D(side*(.31+i*.027),-.09,-.35),.014,.002,silver);
    }
    Ellipsoid(g,new Point3D(0,-.107,.66),.047,.025,.065,silver);
   }else{
    Panel(g,new[]{new Point3D(-.21,.055,.30),new Point3D(.21,.055,.30),new Point3D(.32,.20,-.62),new Point3D(-.32,.20,-.62)},Material("#F4F2E9"));
    Ellipsoid(g,new Point3D(0,-.014,-.15),.148,.105,.031,dark);Ellipsoid(g,new Point3D(0,-.123,-.15),.035,.013,.032,Material("#E9DD9A"));
    Tube(g,new Point3D(0,-.112,.30),new Point3D(0,-.12,.04),.006,.006,dark);
    foreach(int side in new[]{-1,1})Ellipsoid(g,new Point3D(side*.14,-.08,.30),.025,.018,.027,Material("#9BA6AE"));
   }
   return g;
  }
  static void Cone(Model3DGroup g,Point3D a,Point3D b,double r1,double r2,Material m)=>Tube(g,a,b,r1,r2,m);
  static void Tube(Model3DGroup g,Point3D a,Point3D b,double r1,double r2,Material material){
   var axis=b-a;axis.Normalize();var u=Vector3D.CrossProduct(axis,Math.Abs(axis.Z)<.9?new Vector3D(0,0,1):new Vector3D(0,1,0));u.Normalize();var v=Vector3D.CrossProduct(axis,u);var mesh=new MeshGeometry3D();const int n=16;
   for(int k=0;k<2;k++)for(int i=0;i<n;i++){double t=i*2*Math.PI/n;var q=u*Math.Cos(t)+v*Math.Sin(t);mesh.Positions.Add((k==0?a:b)+q*(k==0?r1:r2));mesh.Normals.Add(q);}
   for(int i=0;i<n;i++){int j=(i+1)%n;foreach(int x in new[]{i,j,n+i,j,n+j,n+i})mesh.TriangleIndices.Add(x);}
   mesh.Positions.Add(a);mesh.Positions.Add(b);mesh.Normals.Add(-axis);mesh.Normals.Add(axis);
   for(int i=0;i<n;i++){int j=(i+1)%n;foreach(int x in new[]{2*n,j,i,2*n+1,n+i,n+j})mesh.TriangleIndices.Add(x);}
   mesh.Freeze();g.Children.Add(new GeometryModel3D(mesh,material){BackMaterial=material});
  }
  static void Panel(Model3DGroup g,Point3D[] points,Material material){var mesh=new MeshGeometry3D();foreach(var p in points)mesh.Positions.Add(p);for(int i=1;i<points.Length-1;i++){mesh.TriangleIndices.Add(0);mesh.TriangleIndices.Add(i);mesh.TriangleIndices.Add(i+1);}mesh.Freeze();g.Children.Add(new GeometryModel3D(mesh,material){BackMaterial=material});}
 }
}