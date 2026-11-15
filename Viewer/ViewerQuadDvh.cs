using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace QuickLook.DicomRT
{
    public sealed partial class ViewerControl
    {
        bool quadDvh;
        DockPanel quadHost;
        readonly Grid quadBody=new Grid();
        Button quadThreeDButton,quadDvhButton;
        bool DvhActive=>workspaceMode=="DVH"||(workspaceMode=="Bild"&&(string)planes.SelectedItem=="MPR + 3D"&&quadDvh);
        void EnsureDvhView()
        {
            if(dvhView!=null)return;
            dvhView=new DvhControl();var dock=new DockPanel();
            var header=new DockPanel{Margin=new Thickness(6,2,6,2)};var label=Theme.Text("Dose",11,Theme.Muted);label.VerticalAlignment=VerticalAlignment.Center;label.Margin=new Thickness(0,0,8,0);DockPanel.SetDock(label,Dock.Left);header.Children.Add(label);header.Children.Add(dvhDose);
            DockPanel.SetDock(header,Dock.Top);dock.Children.Add(header);dock.Children.Add(dvhView);
            dvhContainer=new Grid();dvhContainer.Children.Add(dock);
            dvhDose.SelectionChanged+=(s,e)=>{if(!changing)UpdateDvh();};
        }
        void AttachDvh(bool quad)
        {
            EnsureDvhView();Panel parent=quad?quadBody:workspace;
            if(dvhContainer.Parent!=parent){(dvhContainer.Parent as Panel)?.Children.Remove(dvhContainer);parent.Children.Add(dvhContainer);}
            dvhView.SetCompact(quad);dvhContainer.Visibility=Visibility.Visible;
        }
        void SetQuadDvh(bool enabled)
        {
            quadDvh=enabled;AttachQuadView();if(!enabled)dvhView?.Cancel();
        }
        void AttachQuadView()
        {
            if(quadHost==null){quadHost=new DockPanel{Margin=new Thickness(2),Background=Theme.Background};var bar=new StackPanel{Orientation=Orientation.Horizontal};quadThreeDButton=Theme.Button("3D");quadDvhButton=Theme.Button("DVH");quadThreeDButton.ToolTip="Show 3D in the fourth pane";quadDvhButton.ToolTip="Show dose–volume histogram in the fourth pane";quadThreeDButton.Click+=(s,e)=>SetQuadDvh(false);quadDvhButton.Click+=(s,e)=>SetQuadDvh(true);bar.Children.Add(quadThreeDButton);bar.Children.Add(quadDvhButton);DockPanel.SetDock(bar,Dock.Top);quadHost.Children.Add(bar);quadHost.Children.Add(quadBody);}
            if(quadHost.Parent!=imageGrid){(quadHost.Parent as Panel)?.Children.Remove(quadHost);imageGrid.Children.Add(quadHost);}Grid.SetColumn(quadHost,1);Grid.SetRow(quadHost,1);
            quadThreeDButton.Foreground=quadDvh?Theme.Foreground:Theme.Accent;quadDvhButton.Foreground=quadDvh?Theme.Accent:Theme.Foreground;
            bool refresh=quadDvh&&(dvhContainer==null||dvhContainer.Parent!=quadBody||dvhContainer.Visibility!=Visibility.Visible);
            foreach(UIElement child in quadBody.Children)if((quadDvh&&child!=dvhContainer)||(!quadDvh&&child!=threeDView))child.Visibility=Visibility.Collapsed;
            if(quadDvh){AttachDvh(true);if(refresh){UpdateDvhChoices();UpdateDvh();}}
            else AttachThreeD(true);
        }
        double[] DvhPrescriptions(DoseGrid dose)
        {
            if(dose==null||!doses.Contains(dose)||!string.Equals(dose.Units,"GY",StringComparison.OrdinalIgnoreCase)||dose.SummationType!="PLAN"||string.IsNullOrEmpty(dose.PlanUid))return new double[0];
            var matches=planData.Where(p=>p.Entry?.SopUid==dose.PlanUid).ToArray();
            return matches.Length==1?matches[0].PrescriptionDosesGy:new double[0];
        }
    }
}
