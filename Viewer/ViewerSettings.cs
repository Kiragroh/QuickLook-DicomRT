using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace QuickLook.DicomRT
{
 public sealed partial class ViewerControl
 {
  readonly ViewerPreferences preferences=ViewerPreferences.Current;
  readonly CheckBox hideX=new CheckBox{Content="Hide x*",Margin=new Thickness(5,0,5,0),VerticalAlignment=VerticalAlignment.Center,Foreground=Theme.Foreground,ToolTip="Hide structures whose primary displayed label begins with x (case-insensitive). Their individual visibility choices are retained. When shown, x* structures sort last."};
  UIElement BuildSettingsButton(){var button=Theme.Button("⚙");button.ToolTip="Settings · preparation speed, structure labels and default isodoses";button.Click+=(s,e)=>ShowViewerSettings();return button;}
  void ShowViewerSettings()=>CreateSettingsWindow().ShowDialog();
  Window CreateSettingsWindow()
  {
   var window=new Window{Title="Viewer settings",ShowInTaskbar=false,Owner=Window.GetWindow(this),Width=540,SizeToContent=SizeToContent.Height,MaxHeight=780,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Theme.Background,Foreground=Theme.Foreground,FontFamily=FontFamily,ResizeMode=ResizeMode.NoResize};
   window.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/QuickLook.DicomRT.Viewer;component/Theme.xaml",UriKind.Relative)});
   var panel=new StackPanel{Margin=new Thickness(22)};window.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
   panel.Children.Add(Theme.Text("Preparation",18,Theme.Accent));
   Func<string,bool,string,CheckBox> check=(label,value,tip)=>{var c=new CheckBox{Content=label,IsChecked=value,ToolTip=tip,Foreground=Theme.Foreground,Margin=new Thickness(0,9,0,0)};panel.Children.Add(c);return c;};
   var first=check("Prepare outlines before background DRRs",preferences.OutlinesFirst,"Requested views remain interactive. Background ray tracing waits for contour preparation.");
   var demand=check("Prepare DRRs only when DRR is enabled",preferences.DrrOnDemand,"No hidden plan-wide DRR workload while DRR is switched off.");
   var progressive=check("Progressive DRRs: coarse coverage, then fill the gaps",preferences.ProgressiveDrr,"Playback visits prepared recorded control points first. As preparation advances, smaller gaps become available. Every displayed aperture, DRR and outline uses the same geometry.");
   var relevant=check("MLC outlines: D2% above 10% of the highest structure D2%",preferences.DoseRelevantOutlines,"Uses the selected, unambiguous dose. Unknown, partial or unsupported DVHs remain eligible. Applies only to MLC projections, not slice contours, DVH or 3D.");
   panel.Children.Add(Theme.Text("Defaults for new files",18,Theme.Accent));
   panel.Children.Add(Theme.Text("Structure labels",12));var labels=Theme.Combo();labels.ItemsSource=new[]{"Name","Description","Both: name first","Both: description first"};labels.SelectedIndex=preferences.LabelMode;panel.Children.Add(labels);
   var prefix=check("Hide x* structures",preferences.HideX,"Uses the primary displayed label, including Description-first mode.");
   panel.Children.Add(Theme.Text("Default isodose units",12));var units=Theme.Combo();units.ItemsSource=new[]{"Gy (when available)","Relative %"};units.SelectedIndex=preferences.RelativeIsodoses?1:0;panel.Children.Add(units);
   panel.Children.Add(Theme.Text("Default relative isodose levels (%)",12));var levels=new TextBox{Text=string.Join("; ",(isodosePreferences.Read(false)??IsodoseConfiguration.AutomaticLevels(100,false)).Select(v=>v.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture))),Margin=new Thickness(0,4,0,4)};panel.Children.Add(levels);
   panel.Children.Add(new TextBlock{Text="Absolute Gy levels remain specific to the current dose. Relative levels apply across files.",Foreground=Theme.Muted,TextWrapping=TextWrapping.Wrap});
   var message=Theme.Text("",11,Theme.Accent);panel.Children.Add(message);var apply=Theme.Button("Save and apply");panel.Children.Add(apply);
   apply.Click+=(s,e)=>{double[] parsed;string error;if(!IsodoseConfiguration.TryParse(levels.Text,false,out parsed,out error)){message.Text=error;return;}
    preferences.LabelMode=labels.SelectedIndex;preferences.HideX=prefix.IsChecked==true;preferences.RelativeIsodoses=units.SelectedIndex==1;preferences.OutlinesFirst=first.IsChecked==true;preferences.DrrOnDemand=demand.IsChecked==true;preferences.ProgressiveDrr=progressive.IsChecked==true;preferences.DoseRelevantOutlines=relevant.IsChecked==true;
    if(!preferences.Save(out error)||!isodosePreferences.Save(false,parsed,out error)){message.Text=error;return;}
    roiLabelMode.SelectedIndex=preferences.LabelMode;hideX.IsChecked=preferences.HideX;preferRelativeIsodoses=preferences.RelativeIsodoses;RefreshIsodoseLevels();BuildRoiList();centralPlayback?.RefreshPreparationSettings();RoiVisibilityChanged();window.Close();};
   AppIcon.Apply(window);return window;
  }
 }
}
