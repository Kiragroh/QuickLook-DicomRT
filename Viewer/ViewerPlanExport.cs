using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace QuickLook.DicomRT
{
    public sealed partial class ViewerControl
    {
        private Button BuildPlanExportButton()
        {
            var button=Theme.Button("ZIP");button.ToolTip="Export selected plan and referenced CT / dose / structures as original DICOM files";
            button.Click+=async(s,e)=>
            {
                if(sumMode||selectedPlan==null){MessageBox.Show(Window.GetWindow(this),"Select an individual RTPLAN in the RT menu first.","Export DICOM ZIP");return;}
                button.IsEnabled=false;
                try
                {
                    var plan=selectedPlan;var known=(catalog?.Files??new List<DicomEntry>()).Concat(catalog?.DeferredImages??new List<DicomEntry>()).ToArray();
                    var selection=await Task.Run(()=>PlanDicomExport.Resolve(plan,known));
                    if(disposed)return;
                    ShowPlanExportDialog(plan,selection);
                }
                catch(Exception){MessageBox.Show(Window.GetWindow(this),"Unable to resolve the selected plan's files. Finish loading or search for missing images, then try again.","Export DICOM ZIP");}
                finally{button.IsEnabled=true;}
            };
            return button;
        }
        private void ShowPlanExportDialog(PlanData plan,PlanDicomSelection selection)
        {
            var dialog=new Window{Title="Export DICOM ZIP",Owner=Window.GetWindow(this),Width=480,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Theme.Background,Foreground=Theme.Foreground};
            dialog.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/QuickLook.DicomRT.Viewer;component/Theme.xaml",UriKind.Relative)});
            var panel=new StackPanel{Margin=new Thickness(22)};dialog.Content=panel;
            panel.Children.Add(Theme.Text("Plan: "+plan.Label,16));panel.Children.Add(Theme.Text("Choose the components to include",12,Theme.Muted));
            var choices=new Dictionary<string,CheckBox>();
            var names=new Dictionary<string,string>{{"CT","Planning CT"},{"RD","RT dose"},{"RS","RT structures"},{"RP","RT plan"}};
            foreach(var item in selection.Parts)
            {
                var check=new CheckBox{Content=item.Key+" · "+names[item.Key]+" · "+item.Value.Length+" files",IsChecked=item.Value.Length>0,IsEnabled=item.Value.Length>0,Foreground=Theme.Foreground,Margin=new Thickness(0,8,0,8)};
                choices[item.Key]=check;panel.Children.Add(check);
            }
            panel.Children.Add(Theme.Text(selection.Note,11,Theme.Muted));
            panel.Children.Add(Theme.Text("Counts reflect files found so far. To include files still loading, close this dialog and reopen it after discovery completes.",11,Theme.Muted));
            var status=Theme.Text("",11,Theme.Accent);panel.Children.Add(status);
            var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};panel.Children.Add(actions);
            var cancel=Theme.Button("Close");var export=Theme.Button("Save ZIP…");actions.Children.Add(cancel);actions.Children.Add(export);
            var cancellation=new CancellationTokenSource();bool busy=false;
            cancel.Click+=(s,e)=>{if(busy)cancellation.Cancel();else dialog.Close();};
            dialog.Closing+=(s,e)=>{if(busy){e.Cancel=true;cancellation.Cancel();}};
            export.Click+=async(s,e)=>
            {
                var selected=choices.Where(p=>p.Value.IsChecked==true).Select(p=>p.Key).ToArray();if(selected.Length==0){status.Text="Choose at least one component.";return;}
                var identity=new ExportIdentity{PatientId=plan.Entry.Dataset?.GetSingleValueOrDefault<string>(Dicom.DicomTag.PatientID,"")??"",PlanId=plan.Label};
                var save=new Microsoft.Win32.SaveFileDialog{Filter="DICOM ZIP archive|*.zip",FileName=identity.FileName("DICOM",".zip"),Title="Export selected plan"};if(save.ShowDialog(dialog)!=true)return;
                busy=true;export.IsEnabled=false;cancel.Content="Cancel export";foreach(var box in choices.Values)box.IsEnabled=false;
                var progress=new Progress<int>(n=>status.Text="Exporting "+n+" / "+selected.Sum(k=>selection.Parts[k].Length)+" files…");
                try{await Task.Run(()=>PlanDicomExport.WriteZip(selection,selected,save.FileName,cancellation.Token,(done,total)=>((IProgress<int>)progress).Report(done)));status.Text="ZIP saved.";}
                catch(OperationCanceledException){status.Text="Export cancelled. No incomplete ZIP was saved.";}
                catch(Exception){status.Text="Export failed. Check the destination and source file availability.";}
                finally{busy=false;cancel.Content="Close";}
            };
            dialog.ShowDialog();cancellation.Dispose();
        }
    }
}
