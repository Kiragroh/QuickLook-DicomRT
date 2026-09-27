using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuickLook.DicomRT
{
    internal sealed class FieldArrangementControl : Border
    {
        readonly SlicePane slice=new SlicePane{IsHitTestVisible=false};
        readonly TextBlock heading=Theme.Text("FIELD ARRANGEMENT · ISO",10,Theme.Accent);
        readonly TextBlock note=Theme.Text("Matching image required",9,Theme.Muted);
        VolumeData volume;Vec3 iso;double width,level;bool valid;
        public FieldArrangementControl()
        {
            Background=Theme.Background;BorderBrush=Theme.Brush("#3C5067");BorderThickness=new Thickness(1);CornerRadius=new CornerRadius(5);Padding=new Thickness(4);
            var dock=new DockPanel();DockPanel.SetDock(heading,Dock.Top);dock.Children.Add(heading);DockPanel.SetDock(note,Dock.Bottom);dock.Children.Add(note);dock.Children.Add(slice);Child=dock;
            Unloaded+=(s,e)=>slice.CancelPending();Loaded+=(s,e)=>slice.Refresh();
        }
        public void Set(RenderScene scene,PlanData plan,PlanBeam active,ControlPoint cp,Matrix4 map)
        {
            bool available=scene?.Volume!=null&&map!=null&&cp!=null;
            if(!available){if(valid){slice.Scene=null;valid=false;volume=null;}note.Text="Matching volume required";return;}
            var center=map.Transform(cp.Isocenter);if(!BeamProjection.Finite(center.X)||!BeamProjection.Finite(center.Y)||!BeamProjection.Finite(center.Z))return;
            var next=new RenderScene{Volume=scene.Volume,Entry=scene.Entry,Plane="Axial",Focus=center,WindowCenter=scene.WindowCenter,WindowWidth=scene.WindowWidth,Crosshair=false,Plan=plan,PlanToImage=map,ActiveBeam=active,ActiveControlPoint=cp,ShowFields=true,Isocenters=new[]{center}};
            if(!valid||volume!=scene.Volume||(iso-center).Length>1e-5||width!=scene.WindowWidth||level!=scene.WindowCenter){slice.Scene=next;volume=scene.Volume;iso=center;width=scene.WindowWidth;level=scene.WindowCenter;valid=true;}
            else slice.UpdateFields(next);
            note.Text=$"Beam {active?.Number} · G {cp.Gantry:0.#}° · T {cp.Couch:0.#}° · C {cp.Collimator:0.#}°";
        }
    }
}
