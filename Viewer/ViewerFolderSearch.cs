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
        private readonly Button searchSubfolders=new Button{Content="Search subfolders…",Visibility=Visibility.Collapsed,Margin=new Thickness(6,0,0,0),Padding=new Thickness(8,3,8,3),ToolTip="Choose the common parent of your CT and RT folders. Find additional RT for this patient. Image series stay deferred until Search more images; source files remain unchanged."};
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
            if(disposed||folderSearch!=null||imageSearch!=null)return;
            var source=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);folderSearch=source;var token=source.Token;
            searchMoreImages.IsEnabled=false;searchSubfolders.Content="Cancel search";activity.Visibility=Visibility.Visible;activity.Text="● Searching subfolders for matching RT …";
            var clock=System.Diagnostics.Stopwatch.StartNew();
            try{
                var found=await Task.Run(()=>DicomCatalog.Scan(initialEntry.Path,token,null,OnEntryFound,(phase,count,total)=>{
                    if(clock.ElapsedMilliseconds<200)return;clock.Restart();Dispatcher.BeginInvoke(new Action(()=>{if(!disposed&&!token.IsCancellationRequested)activity.Text=$"● Subfolders · {count}/{total} files";}));
                },initialEntry,root,true,imageFilter:e=>false),token);
                token.ThrowIfCancellationRequested();
                MergeDiscoveredCatalog(found);
                await RtLoadsCompletion;token.ThrowIfCancellationRequested();
                var links=await Task.Run(()=>RegistrationReader.Read(catalog),token);token.ThrowIfCancellationRequested();registrations=links;loadRevision++;sumResult=null;
                RefreshPlanChoices();RefreshRt();await TryInitialIsocenterAsync();
                activity.Text=$"✓ Subfolder search complete · {planData.Count} plans · {doses.Count} doses"+(found.SkippedFiles>0?" · some files/folders skipped":"");
            }catch(OperationCanceledException){if(!disposed)activity.Text="Subfolder search cancelled · loaded objects retained";}
            catch(Exception){if(!disposed)activity.Text="Subfolder search incomplete · check folder access";}
            finally{if(folderSearch==source)folderSearch=null;source.Dispose();if(!disposed){searchSubfolders.Content="Search subfolders…";searchMoreImages.IsEnabled=true;}}
        }
    }
}
