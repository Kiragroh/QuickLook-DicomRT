using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuickLook.DicomRT
{
    public sealed partial class ViewerControl
    {
        private VolumeData rtSliceBounds;
        private string rtSliceKey,rtSliceFrame;
        private Task rtSliceCompletion=Task.CompletedTask;
        private VolumeData NavigationBounds=>volume??(!HasImage?rtSliceBounds:null);
        private void EnsureRtSliceBounds(List<RoiOverlay> rois)
        {
            if(HasImage){rtSliceKey=null;rtSliceBounds=null;return;}
            string frame=SceneFrameUid;
            var selected=SelectedDoses.Select(d=>Tuple.Create(d,TransformToImage(d.FrameUid))).Where(p=>p.Item2!=null).ToArray();
            var key=frame+"|"+loadRevision+"|"+string.Join(";",SelectedStructures.Select(s=>s.Entry.SopUid))+"|"+string.Join(";",selected.Select(p=>p.Item1.Entry?.SopUid))+"|"+summedRevision;
            if(key==rtSliceKey)return;
            rtSliceKey=key;if(rtSliceFrame!=frame){rtSliceBounds=null;rtSliceFrame=frame;}
            rtSliceCompletion=PrepareRtSliceBoundsAsync(key,rois.ToArray(),selected);
        }
        private async Task PrepareRtSliceBoundsAsync(string key,RoiOverlay[] rois,Tuple<DoseGrid,Matrix4>[] selected)
        {
            try{
                var bounds=await Task.Run(()=>RtSliceBounds.Create(rois,selected,lifetime.Token),lifetime.Token);
                if(disposed||HasImage||rtSliceKey!=key)return;
                bool first=rtSliceBounds==null;rtSliceBounds=bounds;
                if(bounds!=null&&!userNavigatedImage){if(first){focus=bounds.Center;viewportCenter=null;}await JumpToPlanDoseAsync();}
                if(bounds!=null)status.Text="RT-only slices · scroll structures and dose without CT/MR · Search more images to add anatomy";
                Redraw();
            }catch(OperationCanceledException){}
        }
        private void SyncRtSliceSlider()
        {
            if(HasImage||rtSliceBounds==null)return;
            var b=rtSliceBounds;string plane=(string)planes.SelectedItem;
            double at=plane=="Sagittal"?(focus.X-b.Origin.X)/b.SpacingX:plane=="Coronal"?(focus.Y-b.Origin.Y)/b.SpacingY:(focus.Z-b.Origin.Z)/b.SpacingZ;
            bool previous=changing;changing=true;sliceSlider.Maximum=(plane=="Sagittal"?b.Width:plane=="Coronal"?b.Height:b.Depth)-1;sliceSlider.Value=at;changing=previous;
        }
        private void NavigateRtSlider(double index)
        {
            var b=rtSliceBounds;if(HasImage||b==null)return;userNavigatedImage=true;string plane=(string)planes.SelectedItem;
            focus=plane=="Sagittal"?new Vec3(b.Origin.X+index*b.SpacingX,focus.Y,focus.Z):plane=="Coronal"?new Vec3(focus.X,b.Origin.Y+index*b.SpacingY,focus.Z):new Vec3(focus.X,focus.Y,b.Origin.Z+index*b.SpacingZ);
            RedrawNavigation();
        }
    }
}
