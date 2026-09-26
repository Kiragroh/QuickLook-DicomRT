using System;
using Dicom;
using Dicom.Imaging;

namespace QuickLook.DicomRT
{
    public class PixelPlane
    {
        public int Width, Height;
        public float[] Values;
        public float Min, Max;
        public bool Invert;

        public static PixelPlane Load(DicomEntry entry, int frame = 0)
        {
            if (entry == null || entry.Dataset == null) throw new ArgumentNullException(nameof(entry));
            var d = entry.Dataset;
            if (d.Contains(DicomTag.ModalityLUTSequence))
                throw new NotSupportedException("Modality LUT intensity transformations are not supported.");
            if (d.Contains(DicomTag.PixelValueTransformationSequence) ||
                HasFunctionalTransform(d, DicomTag.SharedFunctionalGroupsSequence) ||
                HasFunctionalTransform(d, DicomTag.PerFrameFunctionalGroupsSequence))
                throw new NotSupportedException("Enhanced shared or per-frame intensity transformations are not supported.");
            if (d.InternalTransferSyntax.IsEncapsulated)
                throw new NotSupportedException("Compressed pixel transfer syntax is not supported by this decoder.");
            var pixel = DicomPixelData.Create(d);
            string photo = DicomCatalog.Text(d, DicomTag.PhotometricInterpretation).Trim();
            if (pixel.SamplesPerPixel != 1 || (photo != "MONOCHROME1" && photo != "MONOCHROME2"))
                throw new NotSupportedException("Only scalar MONOCHROME1/2 pixels are supported; color images require a color decoder.");
            if (frame < 0 || frame >= pixel.NumberOfFrames) throw new ArgumentOutOfRangeException(nameof(frame));
            int allocated = pixel.BitsAllocated, bits = pixel.BitsStored, high = pixel.HighBit;
            if ((allocated != 8 && allocated != 16 && allocated != 32) || bits < 1 || bits > allocated || high < bits - 1 || high >= allocated)
                throw new NotSupportedException("Unsupported or invalid scalar pixel bit layout.");
            int count = checked(pixel.Width * pixel.Height);
            if (count <= 0 || (long)count * sizeof(float) > 512L * 1024 * 1024)
                throw new InvalidOperationException("Pixel plane exceeds the 512 MiB intensity payload limit.");
            var bytes = pixel.GetFrame(frame).Data;
            int bytesPerPixel = allocated / 8;
            if (bytes.LongLength < (long)count * bytesPerPixel) throw new InvalidOperationException("Truncated pixel frame.");
            double slope = DicomCatalog.Real(d, DicomTag.RescaleSlope, 1), intercept = DicomCatalog.Real(d, DicomTag.RescaleIntercept, 0);
            if (!DicomCatalog.Finite(slope) || !DicomCatalog.Finite(intercept) || slope == 0)
                throw new InvalidOperationException("Invalid image rescale values.");
            // fo-dicom's endian buffer normalizes OW data to the platform byte order.
            bool signed = pixel.PixelRepresentation == PixelRepresentation.Signed;
            ulong mask = (1UL << bits) - 1UL, sign = 1UL << (bits - 1);
            int shift = high - bits + 1;
            var result = new PixelPlane { Width = pixel.Width, Height = pixel.Height, Values = new float[count], Min = float.PositiveInfinity, Max = float.NegativeInfinity, Invert = photo == "MONOCHROME1" };
            for (int i = 0, p = 0; i < count; i++, p += bytesPerPixel)
            {
                ulong raw = bytesPerPixel == 1 ? bytes[p] : bytesPerPixel == 2 ? BitConverter.ToUInt16(bytes, p) : BitConverter.ToUInt32(bytes, p);
                raw = (raw >> shift) & mask;
                long value = signed && (raw & sign) != 0 ? (long)raw - (1L << bits) : (long)raw;
                float physical = (float)(value * slope + intercept);
                if (float.IsNaN(physical) || float.IsInfinity(physical)) throw new InvalidOperationException("Non-finite rescaled pixel intensity.");
                result.Values[i] = physical;
                if (physical < result.Min) result.Min = physical;
                if (physical > result.Max) result.Max = physical;
            }
            return result;
        }

        private static bool HasFunctionalTransform(DicomDataset dataset, DicomTag groupTag)
        {
            DicomSequence sequence;
            if (!dataset.TryGetSequence(groupTag, out sequence)) return false;
            foreach (var item in sequence.Items)
                if (item.Contains(DicomTag.PixelValueTransformationSequence) || item.Contains(DicomTag.ModalityLUTSequence)) return true;
            return false;
        }
    }
}
