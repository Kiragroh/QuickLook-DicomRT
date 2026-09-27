using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace QuickLook.DicomRT
{
    public sealed partial class ViewerControl
    {
        private readonly Button searchSubfolders=new Button{Content="Search subfolders…",Visibility=Visibility.Collapsed,Margin=new Thickness(6,0,0,0),Padding=new Thickness(8,3,8,3),ToolTip="Choose the common parent of your CT and RT folders. Search recursively for this patient; source files remain unchanged."};
        private CancellationTokenSource folderSearch;
        private async Task SearchSubfoldersAsync()
        {
            if(folderSearch!=null){folderSearch.Cancel();return;}
            string root;
            using(var picker=new System.Windows.Forms.FolderBrowserDialog{Description="Select the common parent of the CT and RT folders",SelectedPath=Path.GetDirectoryName(initialEntry.Path),ShowNewFolderButton=false})
            {if(picker.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;root=picker.SelectedPath;}
            await SearchFolderAsync(root);
        }
        private async Task SearchFolderAsync(string root)
        {
            if(disposed||folderSearch!=null)return;
            var source=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);folderSearch=source;var token=source.Token;
            searchSubfolders.Content="Cancel search";activity.Visibility=Visibility.Visible;activity.Text="● Searching subfolders for matching RT …";
            var clock=System.Diagnostics.Stopwatch.StartNew();
            try{
                var found=await Task.Run(()=>DicomCatalog.Scan(initialEntry.Path,token,null,OnEntryFound,(phase,count,total)=>{
                    if(clock.ElapsedMilliseconds<200)return;clock.Restart();Dispatcher.BeginInvoke(new Action(()=>{if(!disposed&&!token.IsCancellationRequested)activity.Text=$"● Subfolders · {count}/{total} files";}));
                },initialEntry,root,true),token);
                token.ThrowIfCancellationRequested();
                Func<DicomEntry,bool> same=e=>initialEntry.PatientKey!="|"&&!string.IsNullOrEmpty(initialEntry.PatientKey)?e.PatientKey==initialEntry.PatientKey:e.StudyUid==initialEntry.StudyUid&&!string.IsNullOrEmpty(e.StudyUid);
                found.Files=found.Files.Where(same).ToList();found.Stacks=found.Stacks.Where(s=>s.Entries.All(same)).ToList();
                // Preserve the displayed stack and cached pixels while adding discoveries.
                var merged=new DicomCatalog{Files=(catalog?.Files??new System.Collections.Generic.List<DicomEntry>()).Concat(found.Files).GroupBy(e=>e.SopUid).Select(g=>g.First()).ToList(),Stacks=(catalog?.Stacks??new System.Collections.Generic.List<ImageStack>()).Concat(found.Stacks).GroupBy(s=>s.Key).Select(g=>g.OrderByDescending(s=>s.Entries.Count).First()).ToList(),SkippedFiles=found.SkippedFiles};
                var links=await Task.Run(()=>RegistrationReader.Read(merged),token);token.ThrowIfCancellationRequested();catalog=merged;registrations=links;loadRevision++;sumResult=null;
                changing=true;series.ItemsSource=catalog.Stacks;series.SelectedItem=currentStack;changing=false;
                RefreshPlanChoices();RefreshRt();await TryInitialIsocenterAsync();
                activity.Text=$"✓ Subfolder search complete · {planData.Count} plans · {doses.Count} doses"+(found.SkippedFiles>0?" · some files/folders skipped":"");
            }catch(OperationCanceledException){if(!disposed)activity.Text="Subfolder search cancelled · loaded objects retained";}
            catch(Exception){if(!disposed)activity.Text="Subfolder search incomplete · check folder access";}
            finally{if(folderSearch==source)folderSearch=null;source.Dispose();if(!disposed)searchSubfolders.Content="Search subfolders…";}
        }
    }
}
