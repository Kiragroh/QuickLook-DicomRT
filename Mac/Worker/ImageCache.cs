using QuickLook.DicomRT;

sealed partial class PreviewSession
{
    sealed class PixelCache
    {
        readonly object gate=new();
        readonly Dictionary<DicomEntry,PixelPlane> values=new();
        readonly Queue<DicomEntry> order=new();long bytes;
        public PixelPlane Get(DicomEntry entry,VolumeData volume,ImageStack stack,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            lock(gate)if(values.TryGetValue(entry,out var cached))return cached;
            PixelPlane plane;int index=stack?.Entries.IndexOf(entry)??-1;
            if(volume!=null&&index>=0&&entry.Frames==1){
                int count=volume.Width*volume.Height;var pixels=new float[count];Array.Copy(volume.Values,index*count,pixels,0,count);
                plane=new PixelPlane{Width=volume.Width,Height=volume.Height,Values=pixels,Min=volume.Min,Max=volume.Max,Invert=volume.Invert};
            }else plane=PixelPlane.Load(entry);
            token.ThrowIfCancellationRequested();long size=plane.Values.LongLength*4;
            if(size<=32L*1024*1024)lock(gate){
                if(values.TryGetValue(entry,out var existing))return existing;
                while(order.Count>0&&(order.Count>=9||bytes+size>32L*1024*1024)){var oldest=order.Dequeue();bytes-=values[oldest].Values.LongLength*4;values.Remove(oldest);}
                values.Add(entry,plane);order.Enqueue(entry);bytes+=size;
            }
            return plane;
        }
    }
    PixelCache pixels=new();
    PixelPlane NativePlane(DicomEntry entry)=>pixels.Get(entry,volume,stack,token);
}
