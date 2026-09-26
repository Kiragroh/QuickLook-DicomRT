using System;
namespace QuickLook.DicomRT
{
    public static class MlcTimeline
    {
        // Each beam owns its CP range; never interpolate across a field boundary.
        public static void Locate(int[] counts,double cursor,out int beam,out double local)
        {
            beam=-1;local=0;int offset=0;
            for(int i=0;i<counts.Length;i++)
            {
                int n=Math.Max(0,counts[i]);if(n>0){beam=i;local=Math.Max(0,Math.Min(n-1,cursor-offset));if(cursor<offset+n)return;}offset+=n;
            }
        }
        public static double Angle(double a, double b, double fraction, string direction, bool clockwiseIncreases)
        {
            if(double.IsNaN(a)||double.IsNaN(b))return double.NaN;
            double t=Math.Max(0,Math.Min(1,fraction));
            if(t==0)return (a%360+360)%360;if(t==1)return (b%360+360)%360;
            if(direction=="NONE")return Math.Abs(((b-a)%360+540)%360-180)<1e-6?(a%360+360)%360:double.NaN;
            if(direction!="CW"&&direction!="CC")return double.NaN;
            int sign=(direction=="CW")==clockwiseIncreases?1:-1;
            double distance=((b-a)*sign%360+360)%360;if(distance<1e-9)distance=360;
            return ((a+sign*distance*t)%360+360)%360;
        }
        public static double Angle(double a, double b, double fraction)
        {
            double delta = ((b-a)%360+540)%360-180;
            return ((a+delta*Math.Max(0,Math.Min(1,fraction)))%360+360)%360;
        }
        public static double[] Positions(double[] a, double[] b, double fraction)
        {
            a = a ?? new double[0]; b = b ?? new double[0];
            if (a.Length != b.Length) throw new ArgumentException("Leaf/jaw arrays differ in length.");
            var result = new double[a.Length]; double t = Math.Max(0,Math.Min(1,fraction));
            for (int i=0;i<result.Length;i++) result[i]=a[i]+(b[i]-a[i])*t;
            return result;
        }
    }
}
