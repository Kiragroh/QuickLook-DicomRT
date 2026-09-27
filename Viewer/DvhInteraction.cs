using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace QuickLook.DicomRT
{
    public sealed partial class DvhControl
    {
        sealed partial class DvhPlot
        {
            public event Action<StructureRoi> CurveSelected;
            readonly ToolTip curveTip=new ToolTip {Background=Theme.Panel,Foreground=Theme.Foreground,
                BorderBrush=Theme.Accent,Padding=new Thickness(12),Placement=PlacementMode.Mouse,
                IsHitTestVisible=false,StaysOpen=true};
            Curve hovered;
            public DvhPlot()
            {
                // This tooltip is controlled by proximity, not the enclosing view's export hint.
                ToolTipService.SetIsEnabled(this,false);
                MouseMove+=(s,e)=>UpdateHover(e.GetPosition(this));
                MouseLeave+=(s,e)=>HideHover();Unloaded+=(s,e)=>HideHover();
                MouseLeftButtonDown+=(s,e)=>{if(SelectAt(e.GetPosition(this)))e.Handled=true;};
            }
            public void HideHover(){curveTip.IsOpen=false;hovered=null;Cursor=null;}
            bool SelectAt(Point point)
            {
                var hit=FindCurve(point);if(hit==null)return false;
                CurveSelected?.Invoke(hit.Roi);return true;
            }
            void UpdateHover(Point point)
            {
                var hit=FindCurve(point);if(hit==null){HideHover();return;}
                Cursor=Cursors.Hand;if(ReferenceEquals(hit,hovered))return;hovered=hit;
                var content=new StackPanel {MaxWidth=330};
                content.Children.Add(new TextBlock {Text=hit.Roi.Name,Foreground=hit.Color,FontWeight=FontWeights.SemiBold,FontSize=14,TextWrapping=TextWrapping.Wrap});
                content.Children.Add(new TextBlock {Text=$"{hit.Result.EstimatedVolumeCc:0.##} cm³\n\n"+MetricDetails(hit.Result),Foreground=Theme.Foreground,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,0)});
                content.Children.Add(new TextBlock {Text="Click to focus · click again to show all",Foreground=Theme.Accent,Margin=new Thickness(0,8,0,0)});
                curveTip.Content=content;curveTip.PlacementTarget=this;curveTip.IsOpen=true;
            }
            // Dose samples are ordered. Binary search limits hit testing to the cursor's
            // horizontal pixel neighbourhood, even for 128 curves with 2048 bins each.
            Curve FindCurve(Point point)
            {
                if(ActualWidth<140||ActualHeight<140)return null;
                var bounds=new Rect(52,22,Math.Max(1,ActualWidth-70),Math.Max(1,ActualHeight-74));
                if(!bounds.Contains(point))return null;
                var usable=Curves.Where(c=>c.Visible&&c.Result.DoseValues.Length>1).ToArray();
                double max=usable.Length==0?1:usable.Max(c=>c.Result.DoseValues.Last());if(max<=0)max=1;
                const double radius=7;double best=radius*radius;Curve nearest=null;
                double left=(point.X-radius-bounds.Left)/bounds.Width*max,right=(point.X+radius-bounds.Left)/bounds.Width*max;
                foreach(var curve in usable)
                {
                    var dose=curve.Result.DoseValues;var volume=curve.Result.CumulativeVolumePercent;
                    int lo=0,hi=dose.Length;
                    while(lo<hi){int mid=(lo+hi)/2;if(dose[mid]<left)lo=mid+1;else hi=mid;}
                    for(int i=Math.Max(1,lo);i<dose.Length&&dose[i-1]<=right;i++)
                    {
                        var a=new Point(bounds.Left+dose[i-1]/max*bounds.Width,bounds.Bottom-volume[i-1]/100*bounds.Height);
                        var b=new Point(bounds.Left+dose[i]/max*bounds.Width,bounds.Bottom-volume[i]/100*bounds.Height);
                        var segment=b-a;double length=segment.LengthSquared;
                        double t=length>0?Math.Max(0,Math.Min(1,Vector.Multiply(point-a,segment)/length)):0;
                        double distance=(point-(a+segment*t)).LengthSquared;
                        if(distance<best-0.001||(Math.Abs(distance-best)<=0.001&&ReferenceEquals(curve.Roi,FocusedStructure)))
                        {best=distance;nearest=curve;}
                    }
                }
                return nearest;
            }
        }
    }
}
