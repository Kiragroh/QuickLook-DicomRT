using System;
namespace QuickLook.DicomRT
{
    public sealed class Matrix4
    {
        readonly double[] values;
        public double[] Values => (double[])values.Clone();
        public Matrix4(double[] values)
        {
            if (values == null || values.Length != 16) throw new ArgumentException("Expected a 4 x 4 matrix.");
            foreach (double v in values) if (double.IsNaN(v) || double.IsInfinity(v)) throw new ArgumentException("Nonfinite matrix.");
            if (Math.Abs(values[12])+Math.Abs(values[13])+Math.Abs(values[14])+Math.Abs(values[15]-1)>1e-9) throw new ArgumentException("Expected affine matrix.");
            this.values = (double[])values.Clone();
        }
        public static Matrix4 Identity => new Matrix4(new double[] {1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1});
        public Vec3 Transform(Vec3 p) => new Vec3(values[0]*p.X+values[1]*p.Y+values[2]*p.Z+values[3], values[4]*p.X+values[5]*p.Y+values[6]*p.Z+values[7], values[8]*p.X+values[9]*p.Y+values[10]*p.Z+values[11]);
        public Matrix4 Inverse()
        {
            var a = new double[4,8];
            for(int i=0;i<4;i++) { for(int j=0;j<4;j++) a[i,j]=values[i*4+j]; a[i,i+4]=1; }
            for(int c=0;c<4;c++)
            {
                int pivot=c; for(int r=c+1;r<4;r++) if(Math.Abs(a[r,c])>Math.Abs(a[pivot,c])) pivot=r;
                if(Math.Abs(a[pivot,c])<1e-12) throw new ArgumentException("Singular matrix.");
                for(int k=0;k<8;k++) { double tmp=a[c,k]; a[c,k]=a[pivot,k]; a[pivot,k]=tmp; }
                double d=a[c,c]; for(int k=0;k<8;k++) a[c,k]/=d;
                for(int r=0;r<4;r++) if(r!=c) { double m=a[r,c]; for(int k=0;k<8;k++) a[r,k]-=m*a[c,k]; }
            }
            var result=new double[16]; for(int i=0;i<4;i++) for(int j=0;j<4;j++) result[i*4+j]=a[i,j+4];
            return new Matrix4(result);
        }
        public static Matrix4 Multiply(Matrix4 a, Matrix4 b)
        {
            var result=new double[16];
            for(int i=0;i<4;i++) for(int j=0;j<4;j++) for(int k=0;k<4;k++) result[i*4+j]+=a.values[i*4+k]*b.values[k*4+j];
            return new Matrix4(result);
        }
    }
}
