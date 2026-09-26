using System;
using System.Threading;

namespace QuickLook.DicomRT
{
    public class VolumeData
    {
        public int Width, Height, Depth;
        public float[] Values;
        public Vec3 Origin, AxisX, AxisY, AxisZ;
        public double SpacingX, SpacingY, SpacingZ;
        public float Min, Max;
        public bool Invert;

        public static VolumeData Load(ImageStack stack, CancellationToken token, Action<int> progress = null)
        {
            token.ThrowIfCancellationRequested();
            double step;
            if (!DicomCatalog.ValidateStack(stack, out step, out string warning)) throw new InvalidOperationException(warning);
            var e = stack.Entries[0];
            long count = (long)e.Columns * e.Rows * stack.Entries.Count;
            if (count <= 0 || count > (512L * 1024 * 1024 / sizeof(float)))
                throw new InvalidOperationException("Volume exceeds the 512 MiB intensity payload limit.");
            var result = new VolumeData
            {
                Width = e.Columns, Height = e.Rows, Depth = stack.Entries.Count, Values = new float[(int)count],
                Origin = e.Origin, AxisX = e.AxisX, AxisY = e.AxisY, AxisZ = e.AxisX.Cross(e.AxisY).Normalized(),
                SpacingX = e.SpacingX, SpacingY = e.SpacingY, SpacingZ = step, Min = float.PositiveInfinity, Max = float.NegativeInfinity
            };
            for (int i = 0; i < result.Depth; i++)
            {
                token.ThrowIfCancellationRequested();
                var plane = PixelPlane.Load(stack.Entries[i]);
                if (plane.Width != result.Width || plane.Height != result.Height || (i > 0 && plane.Invert != result.Invert))
                    throw new InvalidOperationException("Pixel planes have inconsistent dimensions or photometric interpretation.");
                result.Invert = plane.Invert;
                Array.Copy(plane.Values, 0, result.Values, i * result.Width * result.Height, plane.Values.Length);
                result.Min = Math.Min(result.Min, plane.Min); result.Max = Math.Max(result.Max, plane.Max);
                progress?.Invoke(i + 1);
            }
            return result;
        }

        public Vec3 WorldAt(double x, double y, double z) => Origin + AxisX * (x * SpacingX) + AxisY * (y * SpacingY) + AxisZ * (z * SpacingZ);
        public Vec3 Center => WorldAt((Width - 1) * 0.5, (Height - 1) * 0.5, (Depth - 1) * 0.5);

        private bool Coordinates(Vec3 world, out double x, out double y, out double z)
        {
            Vec3 delta = world - Origin;
            x = delta.Dot(AxisX) / SpacingX; y = delta.Dot(AxisY) / SpacingY; z = delta.Dot(AxisZ) / SpacingZ;
            const double epsilon = 1e-6;
            if (!DicomCatalog.Finite(x) || !DicomCatalog.Finite(y) || !DicomCatalog.Finite(z) || Values == null ||
                x < -epsilon || x > Width - 1 + epsilon || y < -epsilon || y > Height - 1 + epsilon || z < -epsilon || z > Depth - 1 + epsilon) return false;
            x = Math.Max(0, Math.Min(Width - 1, x)); y = Math.Max(0, Math.Min(Height - 1, y)); z = Math.Max(0, Math.Min(Depth - 1, z));
            return true;
        }
        public float SampleNearest(Vec3 world)
        {
            if (!Coordinates(world, out double x, out double y, out double z)) return float.NaN;
            return Values[((int)Math.Floor(z + 0.5) * Height + (int)Math.Floor(y + 0.5)) * Width + (int)Math.Floor(x + 0.5)];
        }
        public float Sample(Vec3 world)
        {
            if (!Coordinates(world, out double x, out double y, out double z)) return float.NaN;
            int x0 = (int)x, y0 = (int)y, z0 = (int)z;
            int x1 = Math.Min(x0 + 1, Width - 1), y1 = Math.Min(y0 + 1, Height - 1), z1 = Math.Min(z0 + 1, Depth - 1);
            double fx = x - x0, fy = y - y0, fz = z - z0;
            int a = (z0 * Height + y0) * Width, b = (z0 * Height + y1) * Width;
            int c = (z1 * Height + y0) * Width, d = (z1 * Height + y1) * Width;
            double top = (Values[a + x0] * (1 - fx) + Values[a + x1] * fx) * (1 - fy) + (Values[b + x0] * (1 - fx) + Values[b + x1] * fx) * fy;
            double bottom = (Values[c + x0] * (1 - fx) + Values[c + x1] * fx) * (1 - fy) + (Values[d + x0] * (1 - fx) + Values[d + x1] * fx) * fy;
            return (float)(top * (1 - fz) + bottom * fz);
        }
    }
}
