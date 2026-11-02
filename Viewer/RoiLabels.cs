using System.Windows;
using System.Windows.Controls;

namespace QuickLook.DicomRT
{
    internal static class RoiLabels
    {
        public static int GroupOrder(StructureRoi roi,PlanData plan)
        {
            if(roi.HasXPrefix)return 3;
            var category=RoiClassification.Category(roi,plan);
            return category==RoiCategory.Target?0:category==RoiCategory.Organ?1:2;
        }
        public static FrameworkElement Content(StructureRoi roi)
        {
            string tooltip="Name: "+(roi.Name??"")+"\nDescription: "+(string.IsNullOrWhiteSpace(roi.Description)?"—":roi.Description);
            var primary=new TextBlock{Text=roi.PrimaryLabel,TextTrimming=TextTrimming.CharacterEllipsis,ToolTip=tooltip};
            if(roi.SecondaryLabel.Length==0)return primary;
            primary.FontSize=12;primary.FontWeight=FontWeights.SemiBold;
            var panel=new StackPanel{ToolTip=tooltip};panel.Children.Add(primary);
            panel.Children.Add(new TextBlock{Text=roi.SecondaryLabel,FontSize=10,FontWeight=FontWeights.Normal,Foreground=Theme.Muted,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,2,0,0),ToolTip=tooltip});
            return panel;
        }
    }
}
