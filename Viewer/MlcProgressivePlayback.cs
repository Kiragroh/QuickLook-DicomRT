using System;
using System.Linq;
namespace QuickLook.DicomRT
{
 public sealed partial class MlcPlaybackControl
 {
  double progressiveTicks;
  bool AdvanceProgressivePlayback()
  {
   if(!IsVisible||projectionSuspended||!ViewerPreferences.Current.ProgressiveDrr||showDrr.IsChecked!=true||anatomy?.Entry?.Modality!="CT"||anatomy.Volume==null||!HasProgressiveProjection())return false;
   if(cursor.Maximum<=0){Pause();return true;}
   progressiveTicks+=PlaybackStep;if(progressiveTicks<1)return true;progressiveTicks-=Math.Floor(progressiveTicks);
   int at=FindPreparedControlPoint();
   if(at>=0){play.Content="Ⅱ Pause";play.ToolTip="Progressive DRR preview · prepared control points, gaps shrink as preparation completes";cursor.Value=at;return true;}
   play.Content="Ⅱ Preparing…";projectionStatus.Text="Progressive DRRs · waiting for another exact view · navigation remains available";return true;
  }
  bool HasProgressiveProjection()
  {
   if(beam==null||interpolated==null)return false;
   string reason;try{return BeamProjection.Create(beam,interpolated,planMap,out reason)!=null;}catch(ArgumentException){return false;}
  }
  int FindPreparedControlPoint()
  {
   var rois=SelectedOutlines();int start=(int)Math.Floor(cursor.Value)+1;
   // Only advance the entire shared CP state to a fully prepared exact view.
   // A coarse DRR is never overlaid on a different CP's aperture or contours.
   for(int n=0;n<=cursor.Maximum;n++)
   {
    int at=(start+n)%((int)cursor.Maximum+1);if(Math.Abs(at-cursor.Value)<.0001)continue;
    int index;double local;MlcTimeline.Locate(counts,at,out index,out local);if(index<0)continue;
    var candidate=playbackBeams[index];string reason;BeamProjection projection;
    try{projection=BeamProjection.Create(candidate,candidate.ControlPoints[(int)local],planMap,out reason);}catch(ArgumentException){continue;}
    if(projection==null||!projectionCache.HasDrr(projection,anatomy.Volume,extents[candidate]))continue;MlcProjectionFrame frame;
    if(!projectionCache.TryGet(projection,anatomy.Volume,rois,extents[candidate],true,out frame))continue;
    return at;
   }
   return -1;
  }
 }
}
