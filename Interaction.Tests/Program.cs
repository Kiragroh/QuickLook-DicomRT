using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using QuickLook.DicomRT;
class Program
{
    static int checks;
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    static void Layout(FrameworkElement element){element.Measure(new Size(800,700));element.Arrange(new Rect(0,0,800,700));element.UpdateLayout();}
    static System.Collections.Generic.IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T match)yield return match;foreach(var item in Descendants<T>(child))yield return item;}}
    [STAThread] static int Main()
    {
        try
        {
            var app=new Application();app.Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri("/QuickLook.DicomRT.Viewer;component/Theme.xaml",UriKind.Relative)});
            var slider=new Slider {Minimum=0,Maximum=20,Value=3,Style=(Style)app.FindResource(typeof(Slider))};Layout(slider);slider.ApplyTemplate();var track=(Track)slider.Template.FindName("PART_Track",slider);
            Check(track!=null,"Custom slider track");track.Value=14;Check(slider.Value==14,"Track drag pushes Slider.Value");slider.Value=7;Check(track.Value==7,"Slider pushes track");Check(slider.IsMoveToPointEnabled,"Track clicking enabled");
            var combo=new ComboBox {Style=(Style)app.FindResource(typeof(ComboBox))};combo.Items.Add("Readable selection");combo.SelectedIndex=0;Layout(combo);
            var presenter=Descendants<ContentPresenter>(combo).First(p=>p.Content as string=="Readable selection");var foreground=(SolidColorBrush)TextElement.GetForeground(presenter);Check(foreground.Color.R>180&&foreground.Color.G>180,"Selected combo text stays light");
            var rows=new[]{new TagRow{Path="(0008,1115)",Tag="(0008,1115)",Name="Sequence",VR="SQ",Value="1 item"},new TagRow{Path="(0008,1115)[0]",Tag="",Name="Item [0]"},new TagRow{Path="(0008,1115)[0]/(0008,1155)",Tag="(0008,1155)",Name="Referenced SOP",Value="needle"}};
            var roots=TagNode.Build(rows);Check(roots.Count==1&&roots[0].Children.Single().Children.Count==1,"Tag sequence hierarchy");Check(!roots[0].IsExpanded,"Tags start collapsed");roots[0].IsExpanded=true;var matches=TagNode.Search(roots,"needle");Check(matches.Single().IsExpanded&&matches[0].Children[0].IsExpanded,"Search expands ancestors");Check(roots[0].IsExpanded,"Search preserves manual expansion");Check(TagNode.Search(roots,"absent").Count==0,"Search excludes absent tags");
            var mlc=new MlcPlaybackControl();mlc.Resources.MergedDictionaries.Add(app.Resources.MergedDictionaries[0]);var plan=new PlanData();plan.Beams.Add(new PlanBeam{Name="A",ControlPoints={new ControlPoint(),new ControlPoint(),new ControlPoint()}});plan.Beams.Add(new PlanBeam{Name="B",ControlPoints={new ControlPoint(),new ControlPoint()}});mlc.SetPlan(plan);Layout(mlc);
            var cursor=Field<Slider>(mlc,"cursor");Check(cursor.Maximum==4,"Global plan cursor spans both fields");cursor.Value=3;Check(Field<ComboBox>(mlc,"beams").SelectedIndex==1,"Scrub enters next beam");Check(Field<Canvas>(mlc,"markers").Children.Count==2,"Each field end has a dot");
            bool jumped=false;mlc.IsocenterSelected+=p=>jumped=true;mlc.SetPlan(new PlanData());var jump=Descendants<Button>(mlc).First(b=>b.Content as string=="Go to isocenter");jump.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!jumped,"Empty plan never uses previous isocenter");mlc.Dispose();
            using(var viewer=new ViewerControl())
            {
                Field<System.Collections.Generic.List<DoseGrid>>(viewer,"doses").Add(new DoseGrid {Label="Only dose"});
                typeof(ViewerControl).GetMethod("RefreshPlanChoices",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(viewer,null);
                Check(Field<bool>(viewer,"sumMode"),"Dose-only fallback synchronizes sum state");Check(Field<ComboBox>(viewer,"plans").SelectedIndex==0,"Dose-only sum is selected");
            }
            Console.WriteLine("PASS: "+checks+" WPF interaction/contrast/tree/timeline checks");return 0;
        }
        catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}
    }
}
