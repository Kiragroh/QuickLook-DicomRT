using System;
using System.Linq;
using Dicom;
using Dicom.Imaging;
using Dicom.Imaging.Codec;
namespace QuickLook.DicomRT
{
    public sealed class DoseGrid
    {
        public DicomEntry Entry; public VolumeData Volume;
        public string FrameUid,Units,PlanUid,Label,DoseType,SummationType;
        public int ReferencedPlanCount;
        public float Maximum; public bool Visible=true;
        public Vec3? MaximumPosition {get;private set;}
        int width,height,depth; float[] values; double[] offsets;
        Vec3 origin,xAxis,yAxis,zAxis; double sx,sy;
        // Geometry-only sampling bounds also cover nonuniform frame offsets.
        // Values must still be read through Sample, never through this proxy grid.
        public VolumeData SamplingBounds => Volume ?? (offsets==null||depth<1?null:new VolumeData {
            Width=width,Height=height,Depth=depth,Origin=origin+zAxis*offsets[0],AxisX=xAxis,AxisY=yAxis,AxisZ=zAxis,
            SpacingX=sx,SpacingY=sy,SpacingZ=depth>1?(offsets[depth-1]-offsets[0])/(depth-1):1,Max=Maximum });
        public static DoseGrid Load(DicomEntry entry)
        {
            var d=RtDicom.Full(entry);
            if(d.InternalTransferSyntax!=DicomTransferSyntax.ExplicitVRLittleEndian && d.InternalTransferSyntax!=DicomTransferSyntax.ImplicitVRLittleEndian)
                d=new DicomTranscoder(d.InternalTransferSyntax,DicomTransferSyntax.ExplicitVRLittleEndian).Transcode(d);
            var px=DicomPixelData.Create(d);
            int w=px.Width,h=px.Height,n=px.NumberOfFrames;
            if(w<=0 || h<=0 || n<=0 || (long)w*h*n>128L*1024*1024) throw new ArgumentException("Dose grid dimensions exceed supported memory budget.");
            var pos=RtDicom.Numbers(d,DicomTag.ImagePositionPatient); var ori=RtDicom.Numbers(d,DicomTag.ImageOrientationPatient); var spacing=RtDicom.Numbers(d,DicomTag.PixelSpacing);
            if(pos.Length!=3 || ori.Length!=6 || spacing.Length!=2 || pos.Concat(ori).Concat(spacing).Any(v=>!RtDicom.Finite(v))) throw new ArgumentException("Missing dose geometry.");
            var ax=new Vec3(ori[0],ori[1],ori[2]); var ay=new Vec3(ori[3],ori[4],ori[5]);
            if(Math.Abs(ax.Length-1)>1e-4 || Math.Abs(ay.Length-1)>1e-4 || Math.Abs(ax.Dot(ay))>1e-4 || spacing[0]<=0 || spacing[1]<=0) throw new ArgumentException("Invalid dose axes or spacing.");
            ax=ax.Normalized(); ay=ay.Normalized();
            var off=RtDicom.Numbers(d,DicomTag.GridFrameOffsetVector);
            if(off.Length==0 && n==1) off=new double[]{0};
            if(off.Length!=n || off.Any(v=>!RtDicom.Finite(v))) throw new ArgumentException("Invalid dose frame offsets.");
            // PS3.3 C.8.8.3.2: relative-to-first-plane, or legacy absolute axial z.
            if(Math.Abs(off[0])>1e-6)
            {
                if(Math.Abs(off[0]-pos[2])>1e-5 || (ax-new Vec3(1,0,0)).Length>1e-6 || (ay-new Vec3(0,1,0)).Length>1e-6) throw new ArgumentException("Unsupported dose offset convention.");
                off=off.Select(v=>v-pos[2]).ToArray();
            }
            double direction=n>1?Math.Sign(off[1]-off[0]):1;
            if(direction==0 || Enumerable.Range(1,n-1).Any(i=>(off[i]-off[i-1])*direction<=1e-6)) throw new ArgumentException("Dose frame offsets must be strictly monotonic.");
            double scale=RtDicom.Number(d,DicomTag.DoseGridScaling);
            if(!RtDicom.Finite(scale) || scale<=0) throw new ArgumentException("Invalid dose scaling.");
            int bits=px.BitsAllocated,stored=px.BitsStored,high=px.HighBit;
            if((bits!=16 && bits!=32) || stored<1 || stored>bits || high<stored-1 || high>=bits || px.SamplesPerPixel!=1) throw new ArgumentException("Unsupported dose pixel encoding.");
            bool signed=RtDicom.Int(d,DicomTag.PixelRepresentation)==1;
            var data=new float[checked(w*h*n)];
            float minimum=float.PositiveInfinity,maximum=float.NegativeInfinity;int maximumIndex=0;
            ulong mask=(1UL<<stored)-1,sign=1UL<<(stored-1);
            for(int z=0;z<n;z++)
            {
                byte[] bytes=px.GetFrame(z).Data;
                if(bytes.Length<(long)w*h*(bits/8)) throw new ArgumentException("Incomplete dose frame.");
                int dest=(direction>0?z:n-1-z)*w*h;
                for(int i=0;i<w*h;i++)
                {
                    int at=i*(bits/8); ulong raw=(ulong)bytes[at]|((ulong)bytes[at+1]<<8);
                    if(bits==32) raw|=((ulong)bytes[at+2]<<16)|((ulong)bytes[at+3]<<24);
                    raw=(raw>>(high+1-stored))&mask;
                    long integer=signed && (raw&sign)!=0 ? (long)raw-(1L<<stored) : (long)raw;
                    double scaled=integer*scale;
                    if(!RtDicom.Finite(scaled) || Math.Abs(scaled)>float.MaxValue) throw new ArgumentException("Nonfinite dose samples.");
                    float value=(float)scaled; data[dest+i]=value; minimum=Math.Min(minimum,value); if(value>maximum){maximum=value;maximumIndex=dest+i;}
                }
            }
            if(direction<0) Array.Reverse(off);
            var grid=new DoseGrid {Entry=entry,FrameUid=RtDicom.Text(d,DicomTag.FrameOfReferenceUID),Units=RtDicom.Text(d,DicomTag.DoseUnits,"UNKNOWN"),DoseType=RtDicom.Text(d,DicomTag.DoseType),SummationType=RtDicom.Text(d,DicomTag.DoseSummationType),
                PlanUid=RtDicom.Items(d,DicomTag.ReferencedRTPlanSequence).Select(i=>RtDicom.Text(i,DicomTag.ReferencedSOPInstanceUID)).FirstOrDefault()??"",ReferencedPlanCount=RtDicom.Items(d,DicomTag.ReferencedRTPlanSequence).Count(),
                Label=RtDicom.Text(d,DicomTag.DoseSummationType,"RTDOSE"),Maximum=maximum,width=w,height=h,depth=n,values=data,offsets=off,
                origin=new Vec3(pos[0],pos[1],pos[2]),xAxis=ax,yAxis=ay,zAxis=ax.Cross(ay).Normalized(),sx=spacing[1],sy=spacing[0]};
            grid.MaximumPosition=grid.origin+ax*((maximumIndex%w)*grid.sx)+ay*((maximumIndex/w%h)*grid.sy)+grid.zAxis*off[maximumIndex/(w*h)];
            double dz=n>1?off[1]-off[0]:1;
            if(n>1 && Enumerable.Range(1,n-1).All(i=>Math.Abs((off[i]-off[i-1])-dz)<=1e-5))
                grid.Volume=new VolumeData {Width=w,Height=h,Depth=n,Values=data,Origin=grid.origin+grid.zAxis*off[0],AxisX=ax,AxisY=ay,AxisZ=grid.zAxis,
                    SpacingX=grid.sx,SpacingY=grid.sy,SpacingZ=dz,Min=minimum,Max=maximum};
            return grid;
        }
        public float Sample(Vec3 world)
        {
            var delta=world-origin; double x=delta.Dot(xAxis)/sx,y=delta.Dot(yAxis)/sy,z=delta.Dot(zAxis);
            if(!RtDicom.Finite(x) || !RtDicom.Finite(y) || !RtDicom.Finite(z) || x< -1e-6 || y< -1e-6 || x>width-1+1e-6 || y>height-1+1e-6 || z<offsets[0]-1e-6 || z>offsets[depth-1]+1e-6) return float.NaN;
            x=Math.Max(0,Math.Min(width-1,x)); y=Math.Max(0,Math.Min(height-1,y));
            if(Math.Abs(x-Math.Round(x))<1e-9)x=Math.Round(x);if(Math.Abs(y-Math.Round(y))<1e-9)y=Math.Round(y);
            int lo=0,hi=depth-1; while(hi-lo>1) {int mid=(lo+hi)/2; if(offsets[mid]<=z) lo=mid; else hi=mid;}
            double f=hi==lo?0:Math.Max(0,Math.Min(1,(z-offsets[lo])/(offsets[hi]-offsets[lo])));
            if(f<1e-9)f=0;else if(f>1-1e-9)f=1;
            return (float)Mix(Plane(x,y,lo),Plane(x,y,hi),f);
        }
        double Plane(double x,double y,int z)
        {
            int x0=(int)Math.Floor(x),y0=(int)Math.Floor(y),x1=Math.Min(width-1,x0+1),y1=Math.Min(height-1,y0+1);
            double fx=x-x0,fy=y-y0; int start=z*width*height;
            return Mix(Mix(values[start+y0*width+x0],values[start+y0*width+x1],fx),Mix(values[start+y1*width+x0],values[start+y1*width+x1],fx),fy);
        }
        // Zero-weight neighbours must not erase a valid node beside an unknown dose region.
        static double Mix(double a,double b,double fraction)=>fraction<=0?a:fraction>=1?b:a*(1-fraction)+b*fraction;
        internal static DoseGrid FromDerivedVolume(VolumeData volume,string frameUid,string label)
        {
            if(volume==null || volume.Values==null || volume.Width<1 || volume.Height<1 || volume.Depth<1 || (long)volume.Width*volume.Height*volume.Depth!=volume.Values.Length)
                throw new ArgumentException("Invalid derived dose volume.");
            int maximumIndex=-1;float maximum=float.NegativeInfinity;for(int i=0;i<volume.Values.Length;i++)if(RtDicom.Finite(volume.Values[i])&&volume.Values[i]>maximum){maximum=volume.Values[i];maximumIndex=i;}
            return new DoseGrid {MaximumPosition=maximumIndex<0?(Vec3?)null:volume.WorldAt(maximumIndex%volume.Width,maximumIndex/volume.Width%volume.Height,maximumIndex/(volume.Width*volume.Height)),Volume=volume,Entry=new DicomEntry {Modality="RTDOSE",FrameUid=frameUid},FrameUid=frameUid,Units="GY",DoseType="PHYSICAL",SummationType="MULTI_PLAN",PlanUid="",Label=label,Maximum=volume.Max,
                width=volume.Width,height=volume.Height,depth=volume.Depth,values=volume.Values,offsets=Enumerable.Range(0,volume.Depth).Select(i=>i*volume.SpacingZ).ToArray(),origin=volume.Origin,xAxis=volume.AxisX,yAxis=volume.AxisY,zAxis=volume.AxisZ,sx=volume.SpacingX,sy=volume.SpacingY};
        }
    }
}
