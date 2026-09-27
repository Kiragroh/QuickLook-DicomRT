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
        private List<DoseGrid> sumGroup;
        private readonly HashSet<string> sumExcluded=new HashSet<string>();
        private string summedSelection;private int planSelectionRevision;
        private sealed class PlanChoice
        {
            public PlanData Plan;public bool Sum;public List<DoseGrid> Doses;public string Label;public override string ToString()=>Sum?Label:Plan.Label;
        }
        private UIElement BuildPlanPicker()
        {
            var row=new DockPanel {Margin=new Thickness(0,2,0,3)};
            var label=Theme.Text("Plan",11,Theme.Muted);label.Margin=new Thickness(0,0,8,0);label.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(label,Dock.Left);row.Children.Add(label);
            plans.ToolTip="Individual plan or spatially added plan sum";row.Children.Add(plans);return row;
        }
        private void RefreshPlanChoices()
        {
            var choices=planData.Select(p=>new PlanChoice{Plan=p}).ToList();foreach(var group in DoseSum.CompatibleGroups(doses,registrations))choices.Add(new PlanChoice{Sum=true,Doses=group,Label="Σ "+string.Join(" + ",group.Select(DosePlanLabel))});
            bool prior=changing;changing=true;plans.ItemsSource=choices;
            var choice=sumMode?choices.FirstOrDefault(c=>c.Sum&&sumGroup!=null&&c.Doses.Any(d=>sumGroup.Contains(d))):choices.FirstOrDefault(c=>!c.Sum&&c.Plan==selectedPlan);
            if(!userSelectedPlan&&!sumMode&&initialEntry?.Modality=="RTDOSE")
            {var reference=doses.FirstOrDefault(d=>d.Entry.SopUid==initialEntry.SopUid)?.PlanUid;choice=choices.FirstOrDefault(c=>c.Plan!=null&&c.Plan.Entry.SopUid==reference);selectedPlan=choice?.Plan;}
            if(!userSelectedPlan&&!sumMode&&initialEntry?.Modality=="RTSTRUCT")
            {var matches=choices.Where(c=>c.Plan!=null&&c.Plan.StructureSopUid==initialEntry.SopUid).ToArray();choice=matches.Length==1?matches[0]:null;selectedPlan=choice?.Plan;}
            if(choice==null){var opened=choices.FirstOrDefault(c=>c.Plan!=null&&c.Plan.Entry.SopUid==initialEntry?.SopUid);bool referencedOnly=initialEntry?.Modality=="RTDOSE"||initialEntry?.Modality=="RTSTRUCT";choice=opened??(referencedOnly?null:choices.FirstOrDefault(c=>c.Plan!=null));selectedPlan=choice?.Plan;sumMode=false;}
            if(choice?.Sum==true)sumGroup=choice.Doses;plans.SelectedItem=choice;changing=prior;
            if(viewButtons.ContainsKey("MLC"))viewButtons["MLC"].IsEnabled=!sumMode&&selectedPlan!=null;
        }
        private async Task SelectPlanChoiceAsync()
        {
            if(changing)return;var choice=plans.SelectedItem as PlanChoice;if(choice==null)return;
            int selection=++planSelectionRevision;userSelectedPlan=true;initialIsocenterApplied=true;sumLoad?.Cancel();sumMode=choice.Sum;selectedPlan=choice.Plan;sumGroup=choice.Doses;
            viewButtons["MLC"].IsEnabled=!sumMode&&selectedPlan!=null;
            if(sumMode&&rtTabs!=null)rtTabs.SelectedIndex=1;
            if(sumMode&&workspaceMode=="MLC")SetWorkspace("Bild");
            if(!sumMode&&scanComplete)activity.Visibility=Visibility.Collapsed;RefreshRt();
            if(sumMode)await BuildSumAsync();
            else if(selectedPlan!=null&&catalog!=null&&(!HasImage||TransformToImage(selectedPlan.FrameUid)==null)){
                var stack=MatchingPlanStack();
                if(stack!=null&&stack!=currentStack){changing=true;series.SelectedItem=stack;changing=false;await SelectStackAsync(stack);if(!disposed)RefreshRt();}
            }
            if(!sumMode&&selection==planSelectionRevision&&!disposed)await JumpToPlanDoseAsync();
        }
        private ImageStack MatchingPlanStack()
        {
            if(selectedPlan==null||catalog==null)return null;
            var referenced=structures.FirstOrDefault(s=>s.Entry.SopUid==selectedPlan.StructureSopUid)?.ReferencedSeries;
            if(referenced==null)return null;
            var matches=catalog.Stacks.Where(s=>s.Entries.Any(e=>referenced.Contains(e.SeriesUid))&&RegistrationReader.Resolve(registrations,selectedPlan.FrameUid,s.FrameUid)!=null).ToArray();
            if(matches.Length==1)return matches[0];
            var ct=matches.Where(s=>s.Entries.All(e=>e.Modality=="CT")).ToArray();return ct.Length==1?ct[0]:null;
        }
        private async Task BuildSumAsync()
        {
            if(!sumMode||disposed)return;
            if(!scanComplete){sumResult=null;activity.Text="● Plan sum is waiting for the RT scan to finish …";return;}
            var sources=(sumGroup??new List<DoseGrid>()).Where(d=>!sumExcluded.Contains(d.PlanUid)).ToList();
            string selection=string.Join("|",sources.Select(d=>d.Entry.SopUid).OrderBy(x=>x));
            if(sources.Count<2){activity.Text="Select at least two plans and generate the sum.";activity.Visibility=Visibility.Visible;BuildDoseList();return;}
            if(summedRevision==loadRevision&&summedSelection==selection&&sumResult!=null){RefreshRt();await JumpToPlanDoseAsync();return;}
            sumLoad?.Cancel();sumLoad?.Dispose();sumLoad=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);var token=sumLoad.Token;int revision=loadRevision;
            activity.Visibility=Visibility.Visible;activity.Text="● Adding dose grids in physical space …";sumResult=null;Redraw();
            var links=registrations.ToList();
            try
            {
                var result=await Task.Run(()=>DoseSum.Calculate(sources,links,token),token);
                if(disposed||token.IsCancellationRequested||revision!=loadRevision||!sumMode)return;
                sumResult=result;summedRevision=revision;summedSelection=selection;activity.Text=result.Dose!=null?"✓ Plan sum ready":"Plan sum unavailable";
                if(result.Dose!=null){result.Dose.Label="Σ "+string.Join(" + ",sources.Select(DosePlanLabel));
                    var stack=catalog?.Stacks.Where(s=>s.FrameUid==result.Dose.FrameUid).OrderByDescending(s=>s.Modality=="CT").ThenByDescending(s=>s.Entries.Count).FirstOrDefault();
                    if(stack!=null&&(!HasImage||TransformToImage(result.Dose.FrameUid)==null)){changing=true;series.SelectedItem=stack;changing=false;await SelectStackAsync(stack);}
                }
                if(disposed||token.IsCancellationRequested||!sumMode)return;RefreshRt();await JumpToPlanDoseAsync();
            }
            catch(OperationCanceledException){}
            catch(Exception){if(!token.IsCancellationRequested)activity.Text="Plan sum unavailable; check dose associations and units.";}
        }
        private string DosePlanLabel(DoseGrid dose)=>planData.FirstOrDefault(p=>p.Entry.SopUid==dose.PlanUid)?.Label??dose.Label;
        private UIElement BuildSumMembers()
        {
            var panel=new StackPanel();panel.Children.Add(Theme.Text("Included plans",12,Theme.Accent));panel.Children.Add(Theme.Text("Compatible physical PLAN doses · same frame or rigid REG",10,Theme.Muted));
            foreach(var dose in sumGroup??new List<DoseGrid>()){
                var check=new CheckBox{Content=DosePlanLabel(dose),IsChecked=!sumExcluded.Contains(dose.PlanUid),Foreground=Theme.Foreground,Margin=new Thickness(2,4,2,4)};
                check.Checked+=(s,e)=>{sumExcluded.Remove(dose.PlanUid);MarkSumPending();};check.Unchecked+=(s,e)=>{sumExcluded.Add(dose.PlanUid);MarkSumPending();};panel.Children.Add(check);
            }
            var generate=Theme.Button("Generate sum");generate.ToolTip="Add only the checked physical PLAN doses. No fraction scaling. Compatible coordinate systems only.";generate.Click+=async(s,e)=>await BuildSumAsync();panel.Children.Add(generate);
            if(sumResult?.Dose!=null)panel.Children.Add(Theme.Text("Displayed: "+sumResult.Dose.Label,11,Theme.Accent));
            return panel;
        }
        private void MarkSumPending(){activity.Visibility=Visibility.Visible;activity.Text="Selection changed · click Generate sum to update";sumLoad?.Cancel();}
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
                if(structure!=null||dose!=null||plan!=null)AutoOpenRtPanel();
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
