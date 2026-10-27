using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using QuickLook.DicomRT;

internal static class RoiVolumeScenarios
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static void Call(object o,string n)=>o.GetType().GetMethod(n,F).Invoke(o,null);
    public static void Run(Action<bool,string> check)
    {
        var previous=SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        try
        {
            using(var viewer=new ViewerControl())
            {
                var filled=new StructureRoi{Name="Target",FrameUid="volume-test"};
                foreach(double z in new[]{0d,2d})filled.Contours.Add(new Contour{GeometricType="CLOSED_PLANAR",Points=new List<Vec3>{new Vec3(0,0,z),new Vec3(10,0,z),new Vec3(10,10,z),new Vec3(0,10,z)}});
                var empty=new StructureRoi{Name="Empty target",FrameUid="volume-test"};
                var single=new StructureRoi{Name="Single plane",FrameUid="volume-test",Contours=new List<Contour>{filled.Contours[0]}};
                ((List<StructureSet>)Get(viewer,"structures")).Add(new StructureSet{Entry=new DicomEntry(),Rois=new List<StructureRoi>{filled,empty,single}});
                Call(viewer,"BuildRoiList");
                var rows=(IList)Get(viewer,"roiOutlineRows");
                var timer=Stopwatch.StartNew();
                while(((TextBlock)Get(rows[0],"Volume")).Text=="…"||((TextBlock)Get(rows[2],"Volume")).Text=="…")
                {
                    if(timer.ElapsedMilliseconds>5000)throw new Exception("ROI volumes did not complete");
                    var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);
                    Thread.Sleep(5);
                }
                check(((TextBlock)Get(rows[0],"Volume")).Text==(.4).ToString("0.##")+" cm³","Sidebar shows contour volume without CT or dose");
                check(((TextBlock)Get(rows[1],"Volume")).Text=="0 cm³"&&!((Button)Get(rows[1],"Name")).IsEnabled,"Empty structure has zero volume and cannot jump to invalid coordinates");
                check(Equals(((Button)Get(rows[1],"Name")).Foreground,((TextBlock)Get(rows[1],"Volume")).Foreground),"Empty structure name is muted");
                check(((TextBlock)Get(rows[2],"Volume")).Text=="—"&&((Button)Get(rows[2],"Name")).IsEnabled,"Unknown thickness is not misrepresented as an empty structure");
                var cache=(IDictionary)Get(viewer,"roiVolumes");var cached=cache[filled];
                Call(viewer,"BuildRoiList");
                check(ReferenceEquals(cached,cache[filled])&&cache.Count==2,"Rebuilding the list reuses completed volume work");
                for(int i=0;i<35;i++)((List<StructureSet>)Get(viewer,"structures"))[0].Rois.Add(new StructureRoi{Name="A long structure name with a planning suffix "+i,FrameUid="volume-test"});
                Call(viewer,"BuildRoiList");
                viewer.GetType().GetField("layersVisible",F).SetValue(viewer,true);Call(viewer,"UpdatePanels");
                foreach(double width in new[]{290d,430d})
                {
                    ((ColumnDefinition)Get(viewer,"leftColumn")).Width=new GridLength(width);
                    ((TextBlock)Get(rows[0],"Volume")).Text="40030,24 cm³";
                    viewer.Measure(new Size(1200,650));viewer.Arrange(new Rect(0,0,1200,650));viewer.UpdateLayout();
                    var label=(TextBlock)Get(rows[0],"Volume");
                    DependencyObject ancestor=label;while(ancestor!=null&&!(ancestor is ScrollContentPresenter))ancestor=VisualTreeHelper.GetParent(ancestor);
                    var viewport=(ScrollContentPresenter)ancestor;
                    var bounds=label.TransformToAncestor(viewport).TransformBounds(new Rect(label.RenderSize));
                    check(bounds.Right<=viewport.ActualWidth+.1,"Volume and unit stay inside the scrolling viewport at sidebar width "+width);
                    check(label.ActualHeight<20,"Volume stays on one line at sidebar width "+width);
                    DependencyObject owner=viewport;while(owner!=null&&!(owner is ScrollViewer))owner=VisualTreeHelper.GetParent(owner);
                    var scroll=(ScrollViewer)owner;
                    check(scroll.ReadLocalValue(FrameworkElement.StyleProperty)!=DependencyProperty.UnsetValue,"ROI scroller is isolated from host overlay scrollbar styles");
                    check(scroll.Padding.Right>=8&&scroll.HorizontalScrollBarVisibility==ScrollBarVisibility.Disabled,"Sidebar reserves text clearance and constrains rows to viewport width");
                    var longName=(Button)Get(rows[rows.Count-1],"Name");
                    check(longName.HorizontalContentAlignment==HorizontalAlignment.Stretch&&((TextBlock)longName.Content).ToolTip!=null,"Long structure names can trim within their button and expose full text on hover");
                    if(width==430)
                    {
                        var panel=(FrameworkElement)Get(viewer,"leftPanel");
                        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(panel.ActualWidth),(int)Math.Ceiling(panel.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(panel);
                        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                        using(var output=System.IO.File.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"quicklook-roi-list-layout.png")))encoder.Save(output);
                    }
                }
            }
        }
        finally{SynchronizationContext.SetSynchronizationContext(previous);}
    }
}
