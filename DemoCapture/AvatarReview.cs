using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using QuickLook.DicomRT;
internal static partial class Program {
 static async Task AvatarReview(){
  sourceKind="Procedural orientation models; no patient data";window.Width=1200;window.Height=420;var root=new Grid{HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Width=1200,Height=420,Background=new SolidColorBrush(Color.FromRgb(16,19,22))};
  var type=typeof(ViewerControl).Assembly.GetType("QuickLook.DicomRT.PatientOrientationGlyph");var create=type.GetMethod("Create",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(string)},null);
  string[] ids={"human","frieza","obelisk","elsa","saitama"},labels={"Human","Freeza","Obelisk / Codex pet","Elsa","Saitama"};
  for(int i=0;i<5;i++){
   root.ColumnDefinitions.Add(new ColumnDefinition());var box=new DockPanel{Margin=new Thickness(12)};Grid.SetColumn(box,i);root.Children.Add(box);
   var label=new TextBlock{Text=labels[i],Foreground=Brushes.White,FontSize=17,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(5)};DockPanel.SetDock(label,Dock.Bottom);box.Children.Add(label);
   var view=new Viewport3D{Camera=new OrthographicCamera(new Point3D(2,-5,1),new Vector3D(-2,5,-1.12),new Vector3D(0,0,1),1.75)};
   var group=new Model3DGroup();group.Children.Add(new AmbientLight(Color.FromRgb(145,145,145)));group.Children.Add(new DirectionalLight(Colors.White,new Vector3D(-1,2,-3)));group.Children.Add((Model3DGroup)create.Invoke(null,new object[]{ids[i]}));view.Children.Add(new ModelVisual3D{Content=group});box.Children.Add(view);
  }
  window.Content=root;await Task.Delay(100);root.Measure(new Size(1200,420));root.Arrange(new Rect(0,0,1200,420));root.UpdateLayout();
  var bitmap=new RenderTargetBitmap(1200,420,96,96,PixelFormats.Pbgra32);bitmap.Render(root);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(output,"orientation-characters.png")))png.Save(file);
  Console.WriteLine("AVATAR_REVIEW_PASS");
 }
}