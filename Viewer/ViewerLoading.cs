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
        private readonly TextBlock activity = Theme.Text("● Searching for DICOM RT …",11,Theme.Accent);
        private readonly HashSet<string> loadedRt = new HashSet<string>();
        private bool scanComplete, sumMode, userSelectedPlan;
        private int rtFailures, loadRevision, summedRevision=-1;
        private CancellationTokenSource sumLoad;
        private DoseSumResult sumResult;
        private sealed class PlanChoice
        {
            public PlanData Plan;public bool Sum;public override string ToString()=>Sum?"Σ Plan sum · available plan doses":Plan.Label;
        }
        private UIElement BuildPlanPicker()
        {
            var row=new DockPanel {Margin=new Thickness(0,2,0,3)};
            var label=Theme.Text("Plan",11,Theme.Muted);label.Margin=new Thickness(0,0,8,0);label.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(label,Dock.Left);row.Children.Add(label);
            plans.ToolTip="Individual plan or spatially added plan sum";row.Children.Add(plans);return row;
        }
        private void RefreshPlanChoices()
        {
            var choices=planData.Select(p=>new PlanChoice{Plan=p}).ToList();if(doses.Count>1)choices.Add(new PlanChoice{Sum=true});
            bool prior=changing;changing=true;plans.ItemsSource=choices;
            var choice=sumMode?choices.FirstOrDefault(c=>c.Sum):choices.FirstOrDefault(c=>!c.Sum&&c.Plan==selectedPlan);
            if(!userSelectedPlan&&!sumMode&&initialEntry?.Modality=="RTDOSE")
            {var reference=doses.FirstOrDefault(d=>d.Entry.SopUid==initialEntry.SopUid)?.PlanUid;choice=choices.FirstOrDefault(c=>c.Plan!=null&&c.Plan.Entry.SopUid==reference);selectedPlan=choice?.Plan;}
            if(!userSelectedPlan&&!sumMode&&initialEntry?.Modality=="RTSTRUCT")
            {var matches=choices.Where(c=>c.Plan!=null&&c.Plan.StructureSopUid==initialEntry.SopUid).ToArray();choice=matches.Length==1?matches[0]:null;selectedPlan=choice?.Plan;}
            if(choice==null){var opened=choices.FirstOrDefault(c=>c.Plan!=null&&c.Plan.Entry.SopUid==initialEntry?.SopUid);bool referencedOnly=initialEntry?.Modality=="RTDOSE"||initialEntry?.Modality=="RTSTRUCT";choice=opened??(referencedOnly?null:choices.FirstOrDefault(c=>c.Plan!=null));selectedPlan=choice?.Plan;sumMode=false;}
            plans.SelectedItem=choice;changing=prior;
            if(viewButtons.ContainsKey("MLC"))viewButtons["MLC"].IsEnabled=!sumMode&&selectedPlan!=null;
        }
        private async Task SelectPlanChoiceAsync()
        {
            if(changing)return;var choice=plans.SelectedItem as PlanChoice;if(choice==null)return;
            userSelectedPlan=true;sumLoad?.Cancel();sumMode=choice.Sum;selectedPlan=choice.Plan;
            viewButtons["MLC"].IsEnabled=!sumMode&&selectedPlan!=null;
            if(sumMode&&workspaceMode=="MLC")SetWorkspace("Bild");
            if(!sumMode&&scanComplete)activity.Visibility=Visibility.Collapsed;RefreshRt();
            if(sumMode)await BuildSumAsync();
        }
        private async Task BuildSumAsync()
        {
            if(!sumMode||disposed)return;
            if(!scanComplete){sumResult=null;activity.Text="● Plan sum is waiting for the RT scan to finish …";return;}
            if(summedRevision==loadRevision&&sumResult!=null){RefreshRt();return;}
            sumLoad?.Cancel();sumLoad?.Dispose();sumLoad=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);var token=sumLoad.Token;int revision=loadRevision;
            activity.Visibility=Visibility.Visible;activity.Text="● Adding dose grids in physical space …";sumResult=null;Redraw();
            var sources=doses.ToList();var links=registrations.ToList();
            try
            {
                var result=await Task.Run(()=>DoseSum.Calculate(sources,links,token),token);
                if(disposed||token.IsCancellationRequested||revision!=loadRevision||!sumMode)return;
                sumResult=result;summedRevision=revision;activity.Text=result.Dose!=null?"✓ Plan sum ready":"Plan sum unavailable";RefreshRt();
            }
            catch(OperationCanceledException){}
            catch(Exception){if(!token.IsCancellationRequested)activity.Text="Plan sum unavailable; check dose associations and units.";}
        }
        // Called by the scanner on its worker thread. Only matching patient's RT objects are decoded.
        private void OnEntryFound(DicomEntry entry)
        {
            if(!(entry.Modality.StartsWith("RT")||entry.Modality=="REG")||loadedRt.Contains(entry.SopUid))return;
            bool same=initialEntry.PatientKey!="|"&&!string.IsNullOrEmpty(initialEntry.PatientKey)?entry.PatientKey==initialEntry.PatientKey:!string.IsNullOrEmpty(initialEntry.StudyUid)&&entry.StudyUid==initialEntry.StudyUid;
            if(!same&&!SamePath(entry.Path,initialEntry.Path))return;lifetime.Token.ThrowIfCancellationRequested();
            StructureSet structure=null;DoseGrid dose=null;PlanData plan=null;bool failed=false;
            try
            {
                switch(entry.Modality){case "RTSTRUCT":structure=StructureSet.Load(entry);break;case "RTDOSE":dose=DoseGrid.Load(entry);break;case "RTPLAN":plan=PlanData.Load(entry);break;case "REG":break;}
            }
            catch(Exception){failed=true;}
            Dispatcher.Invoke(new Action(()=>
            {
                if(disposed||!loadedRt.Add(entry.SopUid))return;
                if(structure!=null)structures.Add(structure);if(dose!=null)doses.Add(dose);if(plan!=null)planData.Add(plan);if(failed)rtFailures++;
                if(FirstRtMilliseconds==0&&(structure!=null||dose!=null||plan!=null))FirstRtMilliseconds=elapsed.Elapsed.TotalMilliseconds;
                if(FirstPlanMilliseconds==0&&plan!=null)FirstPlanMilliseconds=elapsed.Elapsed.TotalMilliseconds;
                loadRevision++;sumResult=null;
                tagSource.Items.Add(new TagChoice(entry.Modality+" · "+entry.Description,entry));
                if(!userSelectedPlan&&plan!=null&&entry.SopUid==initialEntry.SopUid){selectedPlan=plan;sumMode=false;}
                RefreshPlanChoices();rtSummary.Text=$"{structures.Sum(s=>s.Rois.Count)} structures · {doses.Count} doses · {planData.Count} plans";
                RefreshRt();activity.Text=$"● RT available: {planData.Count} plans · {doses.Count} doses · scanning remaining files …";
            }));
        }
    }
}
