using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QuickLook.DicomRT
{
    public sealed partial class ViewerControl
    {
        // One worker per viewer; search, checkbox changes and plan revisits reuse results.
        private readonly SemaphoreSlim roiVolumeWorker = new SemaphoreSlim(1,1);
        private readonly Dictionary<StructureRoi,Task<DvhResult>> roiVolumes = new Dictionary<StructureRoi,Task<DvhResult>>();
        private async void UpdateRoiVolumeAsync(RoiOutlineRow row)
        {
            try
            {
                Task<DvhResult> work;
                if(!roiVolumes.TryGetValue(row.Roi,out work))
                {
                    var token=lifetime.Token;
                    work=Task.Run(async()=>
                    {
                        await roiVolumeWorker.WaitAsync(token).ConfigureAwait(false);
                        try{return DvhCalculator.EstimateVolume(row.Roi,token);}
                        finally{roiVolumeWorker.Release();}
                    },token);
                    roiVolumes.Add(row.Roi,work);
                }
                var result=await work;
                if(disposed||!roiOutlineRows.Contains(row))return;
                bool available=result.Status==DvhStatus.Complete;
                row.Volume.Text=available?(result.EstimatedVolumeCc<.01?"<0.01":result.EstimatedVolumeCc.ToString("0.##"))+" cm³":"—";
                row.Volume.ToolTip=available?result.Message:"Volume unavailable: "+result.Message;
            }
            catch(OperationCanceledException) { }
            catch(Exception)
            {
                if(disposed||!roiOutlineRows.Contains(row))return;
                row.Volume.Text="—";row.Volume.ToolTip="Contour volume could not be calculated.";
            }
        }
    }
}
