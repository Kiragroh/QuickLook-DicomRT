using Dicom;
using QuickLook.DicomRT;

sealed partial class PreviewSession
{
    sealed record DiscoveryChoice(int id,string kind,string label);
    sealed record DiscoveryPrompt(string kind,string message,DiscoveryChoice[] choices);
    sealed record SearchScope(string root,int files,int directories,int depth,int ancestors);
    DiscoveryPrompt discovery;
    readonly Dictionary<int,SearchScope> discoveryScopes=new();
    HashSet<string> automaticPaths=new(StringComparer.Ordinal),automaticDirectories=new(StringComparer.Ordinal);
    int discoveryChoice;

    void AutomaticCatalog()
    {
        automaticPaths=new(StringComparer.Ordinal){Path.GetFullPath(opened.Path)};automaticDirectories=new(StringComparer.Ordinal);
        ReadAutomaticPaths();
        SearchAutomatic(new(Path.GetDirectoryName(Path.GetFullPath(opened.Path)),4096,128,6,3));
    }
    object ContinueAutomatic(int choice)
    {
        if(!discoveryScopes.TryGetValue(choice,out var scope))throw new ArgumentException("Stale discovery choice");
        workGroups.CancelAll();partial=false;discovery=null;discoveryScopes.Clear();
        SearchAutomatic(scope);LoadStack(ChooseStack());var model=Model();ReportLoad("Bereit",1,1,true);return model;
    }
    void ReadAutomaticPaths()
    {
        var found=DicomCatalog.Scan(opened.Path,token,seed:opened,knownPaths:automaticPaths,
            imageFilter:e=>e.SopUid==opened.SopUid||DicomPatientScope.Matches(opened,e),
            phaseProgress:(phase,current,total)=>ReportLoad("Unterordner · DICOM-Referenzen prüfen",current,total));
        ReadContext(found);
    }
    PlanData[] RelevantAutomaticPlans()
    {
        if(opened.Modality=="RTPLAN")return plans.Where(p=>p.Entry.SopUid==opened.SopUid).ToArray();
        if(opened.Modality=="RTDOSE"){var exact=doses.FirstOrDefault(d=>d.Entry.SopUid==opened.SopUid);return plans.Where(p=>p.Entry.SopUid==exact?.PlanUid).ToArray();}
        return plans.Where(p=>structures.Any(s=>s.Entry.SopUid==p.StructureSopUid&&
            (opened.Modality=="RTSTRUCT"?s.Entry.SopUid==opened.SopUid:s.ReferencedSeries.Contains(opened.SeriesUid)))).ToArray();
    }
    bool CompleteAutomatic(PlanData candidate)
    {
        var set=structures.FirstOrDefault(s=>s.Entry.SopUid==candidate.StructureSopUid);
        return set!=null&&catalog.Stacks.Any(s=>s.Entries.Count>1&&set.ReferencedSeries.Contains(s.Entries[0].SeriesUid)&&
            Map(candidate.FrameUid,s.FrameUid)!=null&&set.Rois.Any(r=>Map(r.FrameUid,s.FrameUid)!=null)&&
            doses.Any(d=>d.ReferencedPlanCount==1&&d.PlanUid==candidate.Entry.SopUid&&Map(d.FrameUid,s.FrameUid)!=null));
    }
    void SearchAutomatic(SearchScope scope)
    {
        string root=scope.root;
        for(int level=0;level<=scope.ancestors;level++){
            token.ThrowIfCancellationRequested();ReportLoad("Benachbarte Unterordner prüfen",0,0);
            var paths=new HashSet<string>(automaticPaths,StringComparer.Ordinal);var directories=new HashSet<string>(automaticDirectories,StringComparer.Ordinal);
            if(!EnumerateAutomatic(root,scope,paths,directories)){ScopePrompt(root,scope,"Der Suchbereich enthält viele Dateien oder tiefe Unterordner. Bitte einen Bereich wählen oder die Suche erweitern.");return;}
            bool changed=paths.Count!=automaticPaths.Count;automaticPaths=paths;automaticDirectories=directories;if(changed)ReadAutomaticPaths();
            var relevant=RelevantAutomaticPlans();
            if(relevant.Any(CompleteAutomatic)){PlanPrompt(relevant);return;}
            var parent=Directory.GetParent(root)?.FullName;
            if(parent==null||CollectionRoot(parent)){PlanPrompt(relevant);return;}
            if(level==scope.ancestors){if(relevant.Length>1)PlanPrompt(relevant);else ScopePrompt(parent,scope,"Der Plan-Kontext ist noch unvollständig. Weiter oben nach zugehörigen DICOM-Dateien suchen?");return;}
            root=parent;
        }
    }
    bool EnumerateAutomatic(string root,SearchScope scope,HashSet<string> paths,HashSet<string> directories)
    {
        var pending=new Stack<(string path,int depth)>();pending.Push((root,0));
        while(pending.Count>0){token.ThrowIfCancellationRequested();var item=pending.Pop();
            // Previously complete subtrees need not be enumerated again.
            if(directories.Contains(item.path))continue;
            if(directories.Count>=scope.directories)return false;directories.Add(item.path);
            try{
                foreach(var file in Directory.EnumerateFiles(item.path)){
                    token.ThrowIfCancellationRequested();if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)continue;
                    paths.Add(file);if(paths.Count>scope.files)return false;
                }
                foreach(var child in Directory.EnumerateDirectories(item.path)){
                    token.ThrowIfCancellationRequested();var info=new DirectoryInfo(child);
                    if((info.Attributes&FileAttributes.ReparsePoint)!=0||info.Name.StartsWith('.')||info.Name.EndsWith(".app",StringComparison.OrdinalIgnoreCase)||info.Name=="node_modules")continue;
                    if(item.depth>=scope.depth)return false;pending.Push((child,item.depth+1));
                }
            }catch(Exception ex) when(ex is UnauthorizedAccessException||ex is IOException){/* Existing readable context remains available. */}
        }
        return true;
    }
    static bool CollectionRoot(string path)
    {
        var parts=path.Split(Path.DirectorySeparatorChar,StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length==0||path=="/Users"||path=="/Volumes"||path=="/private"||path=="/private/var"||path=="/private/var/folders"||path=="/var"||path=="/var/folders"||path=="/tmp"||path=="/private/tmp")return true;
        if(parts[0]=="Volumes"&&parts.Length<=2)return true;
        if(parts[0]=="Users"&&(parts.Length<=2||parts.Length==3&&new[]{"Desktop","Documents","Downloads","Library","Pictures","Movies","Music"}.Contains(parts[2])))return true;
        return path==Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
    void PlanPrompt(PlanData[] relevant)
    {
        discovery=null;discoveryScopes.Clear();
        if(plan==null&&relevant.Length>1)discovery=new("plans","Mehrere Pläne verweisen auf diese DICOM-Daten. Bitte einen Plan auswählen.",relevant.Select(p=>new DiscoveryChoice(plans.IndexOf(p),"plan",(string.IsNullOrWhiteSpace(p.Label)||p.Label=="None"?"RT-Plan":p.Label)+" · "+p.Beams.Count+" Felder · Plan "+(plans.IndexOf(p)+1))).ToArray());
    }
    void ScopePrompt(string root,SearchScope scope,string message)
    {
        discoveryScopes.Clear();var choices=new List<DiscoveryChoice>();
        void Add(string path,string kind,string label,SearchScope next){int id=++discoveryChoice;discoveryScopes.Add(id,next with{root=path});choices.Add(new(id,kind,label));}
        if(scope.files<65536||scope.depth<18)Add(root,"continue","Gesamten Bereich weiter durchsuchen · "+new DirectoryInfo(root).Name,scope with{files=Math.Min(65536,scope.files*4),directories=Math.Min(2048,scope.directories*4),depth=Math.Min(18,scope.depth+4)});
        try{foreach(var child in Directory.EnumerateDirectories(root).Take(128)){var info=new DirectoryInfo(child);if((info.Attributes&FileAttributes.ReparsePoint)==0&&!info.Name.StartsWith('.')&&!info.Name.EndsWith(".app",StringComparison.OrdinalIgnoreCase)&&info.Name!="node_modules")Add(child,"folder",info.Name,new(child,4096,128,6,0));}}
        catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException){}
        discovery=new("scope",message,choices.ToArray());
    }
}
