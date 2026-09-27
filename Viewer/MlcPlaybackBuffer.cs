using System.Collections.Generic;
using System.Threading;

namespace QuickLook.DicomRT
{
    // Independent from recorded-CP preload counts. A playback view is complete
    // only after all requested outlines (and, when enabled, its DRR) were prepared.
    internal sealed class MlcPlaybackBuffer
    {
        public readonly MlcProjectionCache.WarmView[] Views;
        readonly bool images,contours;
        readonly HashSet<string> expected,doneImages=new HashSet<string>(),doneContours=new HashSet<string>();
        int imageCount,contourCount;
        public MlcPlaybackBuffer(MlcProjectionCache.WarmView[] views,bool images,bool contours)
        {Views=views;this.images=images;this.contours=contours;expected=new HashSet<string>();foreach(var view in views)expected.Add(view.Key);}
        public bool Ready=>(!images||Volatile.Read(ref imageCount)==Views.Length)&&(!contours||Volatile.Read(ref contourCount)==Views.Length);
        public void Mark(string key,bool image)
        {
            lock(expected){if(!expected.Contains(key))return;if(image){if(doneImages.Add(key))Interlocked.Increment(ref imageCount);}else if(doneContours.Add(key))Interlocked.Increment(ref contourCount);}
        }
        public string Text=>"Playback buffer · "+(images?"DRRs "+Volatile.Read(ref imageCount)+"/"+Views.Length:"DRR off")+" · "+(contours?"outline views "+Volatile.Read(ref contourCount)+"/"+Views.Length:"outlines off")+(Ready?" · prepared":" · preparing field…");
    }
}
