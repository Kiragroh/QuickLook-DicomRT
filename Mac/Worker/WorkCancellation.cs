using System.Text.Json;

sealed partial class PreviewSession
{
    // Each snapshot captures its token, not a mutable cancellation source.
    sealed class WorkGroups
    {
        readonly object gate=new();
        readonly Dictionary<string,(long revision,CancellationTokenSource source)> groups=new();
        public CancellationToken Get(string name,long revision,CancellationToken lifetime)
        {
            lock(gate){
                if(groups.TryGetValue(name,out var prior)){
                    if(revision<prior.revision||revision==prior.revision&&prior.source.IsCancellationRequested)throw new OperationCanceledException();
                    if(revision==prior.revision)return prior.source.Token;
                    prior.source.Cancel();prior.source.Dispose();
                }
                var source=CancellationTokenSource.CreateLinkedTokenSource(lifetime);groups[name]=(revision,source);return source.Token;
            }
        }
        public void Advance(string name,long revision,CancellationToken lifetime) { Get(name,revision,lifetime); }
        public void CancelAll(){lock(gate)foreach(var item in groups.Values)item.source.Cancel();}
    }
    WorkGroups workGroups=new();
    CancellationToken workToken;
    public PreviewSession WorkSnapshot(JsonElement request)
    {
        var snapshot=Snapshot();
        if(request.TryGetProperty("workGroup",out var group)&&request.TryGetProperty("workRevision",out var revision))
            snapshot.workToken=workGroups.Get(group.GetString(),revision.GetInt64(),lifetime.Token);
        return snapshot;
    }
    public object CancelWork(JsonElement request)
    {
        workGroups.Advance(request.GetProperty("group").GetString(),request.GetProperty("revision").GetInt64(),lifetime.Token);return true;
    }
}
