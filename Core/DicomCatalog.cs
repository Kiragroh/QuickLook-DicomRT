using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Dicom;

namespace QuickLook.DicomRT
{
    public class DicomCatalog
    {
        public List<DicomEntry> Files = new List<DicomEntry>();
        public List<DicomEntry> DeferredImages = new List<DicomEntry>();
        public List<ImageStack> Stacks = new List<ImageStack>();
        public int SkippedFiles;

        public static DicomEntry ReadEntry(string path)
        {
            // On-demand buffers retain the complete tag tree without loading pixel payloads.
            var file = DicomFile.Open(path, FileReadOption.ReadLargeOnDemand);
            if (file == null) throw new InvalidOperationException("Not a DICOM instance.");
            var dataset = file.Dataset;
            if (!dataset.Contains(DicomTag.SOPClassUID) || !dataset.Contains(DicomTag.SOPInstanceUID))
                throw new InvalidOperationException("Not a DICOM instance.");
            var e = new DicomEntry
            {
                Path = path, Dataset = dataset,
                SopUid = Text(dataset, DicomTag.SOPInstanceUID), SeriesUid = Text(dataset, DicomTag.SeriesInstanceUID),
                StudyUid = Text(dataset, DicomTag.StudyInstanceUID), FrameUid = Text(dataset, DicomTag.FrameOfReferenceUID),
                Modality = Text(dataset, DicomTag.Modality), Description = Text(dataset, DicomTag.SeriesDescription),
                PatientKey = Text(dataset, DicomTag.PatientID) + "|" + Text(dataset, DicomTag.IssuerOfPatientID),
                Rows = Number(dataset, DicomTag.Rows, 0), Columns = Number(dataset, DicomTag.Columns, 0),
                Frames = Number(dataset, DicomTag.NumberOfFrames, 1),
                WindowCenter = Real(dataset, DicomTag.WindowCenter, double.NaN),
                WindowWidth = Real(dataset, DicomTag.WindowWidth, double.NaN)
            };
            try
            {
                double[] origin = dataset.GetValues<double>(DicomTag.ImagePositionPatient);
                double[] orientation = dataset.GetValues<double>(DicomTag.ImageOrientationPatient);
                double[] spacing = dataset.GetValues<double>(DicomTag.PixelSpacing);
                if (origin.Length != 3 || orientation.Length != 6 || spacing.Length != 2 ||
                    origin.Concat(orientation).Concat(spacing).Any(x => !Finite(x))) return e;
                var x = new Vec3(orientation[0], orientation[1], orientation[2]);
                var y = new Vec3(orientation[3], orientation[4], orientation[5]);
                if (Math.Abs(x.Length - 1) > 0.001 || Math.Abs(y.Length - 1) > 0.001 || Math.Abs(x.Dot(y)) > 0.001 ||
                    spacing[0] <= 0 || spacing[1] <= 0 || e.Rows <= 0 || e.Columns <= 0) return e;
                e.Origin = new Vec3(origin[0], origin[1], origin[2]);
                e.AxisX = x.Normalized();
                e.AxisY = (y - e.AxisX * y.Dot(e.AxisX)).Normalized();
                e.SpacingX = spacing[1]; e.SpacingY = spacing[0]; e.HasGeometry = true;
            }
            catch (DicomDataException) { }
            catch (ArgumentException) { }
            catch (FormatException) { }
            return e;
        }

        /// <summary>
        /// Discover RT/registration objects before full image metadata. Callbacks run on the caller's thread.
        /// progress counts complete files; phaseProgress reports headers/catalog/complete and their bounded totals.
        /// A seed is an already-read instance from this folder (normally the initially displayed image).
        /// </summary>
        public static DicomCatalog Scan(string selectedFile, CancellationToken token, Action<int> progress = null,
            Action<DicomEntry> entryFound = null, Action<string,int,int> phaseProgress = null, DicomEntry seed = null, string searchRoot = null, bool recursive = false, Func<DicomEntry,bool> imageFilter = null, IEnumerable<string> knownPaths = null)
        {
            token.ThrowIfCancellationRequested();
            var result = new DicomCatalog();
            string folder = searchRoot==null?System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(selectedFile)):System.IO.Path.GetFullPath(searchRoot);
            var groups = new Dictionary<string, List<ImageStack>>(StringComparer.Ordinal);
            var paths = new List<string>();
            var folders=new Stack<string>();if(knownPaths!=null){foreach(var path in knownPaths){token.ThrowIfCancellationRequested();paths.Add(path);}}else folders.Push(folder);
            while(folders.Count>0){token.ThrowIfCancellationRequested();var directory=folders.Pop();
                try{foreach(var path in Directory.EnumerateFiles(directory)){token.ThrowIfCancellationRequested();paths.Add(path);}if(recursive)foreach(var child in Directory.EnumerateDirectories(directory)){token.ThrowIfCancellationRequested();if((File.GetAttributes(child)&FileAttributes.ReparsePoint)==0)folders.Push(child);}}
                catch(UnauthorizedAccessException){result.SkippedFiles++;}catch(IOException){result.SkippedFiles++;}
            }
            var deferred = new List<string>();var identities=new Dictionary<string,DicomEntry>(StringComparer.OrdinalIgnoreCase);
            string seedPath = string.IsNullOrEmpty(seed?.Path) ? null : System.IO.Path.GetFullPath(seed.Path);
            int processed = 0;
            void Load(string path)
            {
                token.ThrowIfCancellationRequested();
                DicomEntry e;
                try { e = string.Equals(path, seedPath, StringComparison.OrdinalIgnoreCase) ? seed : ReadEntry(path); }
                catch (Exception ex) when (ex is DicomException || ex is IOException || ex is UnauthorizedAccessException ||
                                           ex is ArgumentException || ex is InvalidOperationException || ex is FormatException || ex is OverflowException)
                { result.SkippedFiles++; ++processed; progress?.Invoke(processed); return; }
                result.Files.Add(e);
                // This is a complete entry, including the on-demand tag tree, never the short header probe.
                entryFound?.Invoke(e);
                // RT dose grids have separate geometry and are never background image stacks.
                if (e.Rows > 0 && e.Columns > 0 && e.Dataset.Contains(DicomTag.PixelData) && e.Modality != "RTDOSE")
                {
                    string key = string.Join("|", e.StudyUid, e.SeriesUid, e.FrameUid, e.PatientKey, e.Modality,
                        e.Rows, e.Columns, e.Frames,
                        Text(e.Dataset, DicomTag.EchoNumbers), Text(e.Dataset, DicomTag.EchoTime),
                        Text(e.Dataset, DicomTag.TemporalPositionIdentifier), Text(e.Dataset, DicomTag.TriggerTime),
                        Text(e.Dataset, DicomTag.AcquisitionNumber), Text(e.Dataset, DicomTag.ImageType),
                        Text(e.Dataset, DicomTag.DiffusionBValue));
                    // Missing series IDs must not join unrelated instances.
                    if (string.IsNullOrWhiteSpace(e.SeriesUid)) key += "|" + e.SopUid;
                    List<ImageStack> candidates;
                    if (!groups.TryGetValue(key, out candidates)) groups.Add(key, candidates = new List<ImageStack>());
                    var stack = candidates.FirstOrDefault(s => Compatible(s.Entries[0], e));
                    if (stack == null)
                    {
                        stack = new ImageStack { Key = key + "|" + candidates.Count, Description = e.Description, Modality = e.Modality, FrameUid = e.FrameUid };
                        candidates.Add(stack); result.Stacks.Add(stack);
                    }
                    stack.Entries.Add(e);
                }
                ++processed; progress?.Invoke(processed);
            }
            phaseProgress?.Invoke("headers", 0, paths.Count);
            for (int i = 0; i < paths.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var path = paths[i]; bool priority = false;
                try
                {
                    if (string.Equals(path, seedPath, StringComparison.OrdinalIgnoreCase)){priority = IsRtOrRegistration(seed.Dataset);if(imageFilter!=null&&!priority)identities[path]=seed;}
                    else
                    {
                        // Use the DICOM reader's stop condition, including raw/no-preamble datasets and all
                        // supported transfer syntaxes. No private parser or filename convention is involved.
                        // Deferred discovery also keeps series/frame identity from this same header pass.
                        // Both probes stop before nested RT data and pixel payloads.
                        var header = DicomFile.Open(path, Encoding.UTF8,
                            state => state.SequenceDepth == 0 && (imageFilter==null?state.Tag.CompareTo(DicomTag.Modality)>0:state.Tag.Group>0x0020),
                            FileReadOption.ReadLargeOnDemand);
                        priority = header != null && IsRtOrRegistration(header.Dataset);
                        if(imageFilter!=null&&!priority&&header!=null)identities[path]=ImageIdentity(header.Dataset,path);
                    }
                }
                catch (Exception ex) when (ex is DicomException || ex is IOException || ex is UnauthorizedAccessException ||
                                           ex is ArgumentException || ex is InvalidOperationException || ex is FormatException || ex is OverflowException)
                { /* A failed probe still receives the ordinary full read, preserving prior scan behavior. */ }
                if (priority) Load(path); else deferred.Add(path);
                phaseProgress?.Invoke("headers", i + 1, paths.Count);
            }
            phaseProgress?.Invoke("catalog", processed, paths.Count);
            foreach (var path in deferred)
            {
                if(imageFilter!=null){
                    try{DicomEntry identity;if(!identities.TryGetValue(path,out identity))identity=ReadImageIdentity(path);if(!imageFilter(identity)){result.DeferredImages.Add(identity);++processed;progress?.Invoke(processed);phaseProgress?.Invoke("catalog",processed,paths.Count);continue;}}
                    catch(Exception ex) when(ex is DicomException||ex is IOException||ex is UnauthorizedAccessException||ex is ArgumentException||ex is InvalidOperationException||ex is FormatException||ex is OverflowException){}
                }
                Load(path); phaseProgress?.Invoke("catalog", processed, paths.Count);
            }
            foreach (var stack in result.Stacks)
            {
                token.ThrowIfCancellationRequested();
                var e = stack.Entries[0];
                if (e.HasGeometry)
                {
                    Vec3 n = e.AxisX.Cross(e.AxisY);
                    stack.Entries.Sort((a, b) => a.Origin.Dot(n).CompareTo(b.Origin.Dot(n)));
                }
                double step;
                stack.CanMpr = ValidateStack(stack, out step, out string warning);
                stack.GeometryWarning = warning;
            }
            result.Stacks = result.Stacks.OrderByDescending(s => s.Entries.Count).ToList();
            phaseProgress?.Invoke("complete", processed, paths.Count);
            return result;
        }

        // Stop before pixel data and retain only discovery identity, never an image buffer.
        static DicomEntry ReadImageIdentity(string path)
        {
            var file=DicomFile.Open(path,Encoding.UTF8,state=>state.SequenceDepth==0&&state.Tag.CompareTo(new DicomTag(0x0021,0))>=0,FileReadOption.ReadLargeOnDemand);
            return ImageIdentity(file.Dataset,path);
        }
        static DicomEntry ImageIdentity(DicomDataset d,string path)
        {
            if(!d.Contains(DicomTag.SOPInstanceUID))throw new ArgumentException("Missing instance identity");
            return new DicomEntry{Path=path,SopUid=Text(d,DicomTag.SOPInstanceUID),SeriesUid=Text(d,DicomTag.SeriesInstanceUID),StudyUid=Text(d,DicomTag.StudyInstanceUID),FrameUid=Text(d,DicomTag.FrameOfReferenceUID),Modality=Text(d,DicomTag.Modality),Description=Text(d,DicomTag.SeriesDescription),PatientKey=Text(d,DicomTag.PatientID)+"|"+Text(d,DicomTag.IssuerOfPatientID)};
        }

        private static bool IsRtOrRegistration(DicomDataset dataset)
        {
            string modality = Text(dataset, DicomTag.Modality), sop = Text(dataset, DicomTag.SOPClassUID);
            return modality.StartsWith("RT", StringComparison.Ordinal) || modality == "REG" ||
                sop.StartsWith("1.2.840.10008.5.1.4.1.1.481.", StringComparison.Ordinal) ||
                sop == "1.2.840.10008.5.1.4.1.1.66.1" || sop == "1.2.840.10008.5.1.4.1.1.66.3";
        }

        internal static bool Compatible(DicomEntry a, DicomEntry b)
        {
            if (a.Rows != b.Rows || a.Columns != b.Columns || a.Frames != b.Frames || a.HasGeometry != b.HasGeometry) return false;
            if (!a.HasGeometry) return true;
            return (a.AxisX - b.AxisX).Length < 0.0001 && (a.AxisY - b.AxisY).Length < 0.0001 &&
                Math.Abs(a.SpacingX - b.SpacingX) < Math.Max(0.00001, a.SpacingX * 0.0001) &&
                Math.Abs(a.SpacingY - b.SpacingY) < Math.Max(0.00001, a.SpacingY * 0.0001);
        }

        internal static bool ValidateStack(ImageStack stack, out double step, out string warning)
        {
            step = 0; warning = null;
            if (stack == null || stack.Entries.Count < 2) { warning = "MPR requires at least two image planes."; return false; }
            var first = stack.Entries[0];
            if (!first.HasGeometry) { warning = "Missing or invalid patient geometry."; return false; }
            Vec3 normal = first.AxisX.Cross(first.AxisY);
            foreach (var e in stack.Entries)
            {
                if (!Finite(e.AxisX.Length) || !Finite(e.AxisY.Length) || Math.Abs(e.AxisX.Length - 1) > 1e-6 ||
                    Math.Abs(e.AxisY.Length - 1) > 1e-6 || Math.Abs(e.AxisX.Dot(e.AxisY)) > 1e-6 ||
                    !Finite(e.SpacingX) || !Finite(e.SpacingY) || e.SpacingX <= 0 || e.SpacingY <= 0 || e.Rows <= 0 || e.Columns <= 0)
                { warning = "Invalid image axes, pixel spacing, or dimensions."; return false; }
                if (!e.HasGeometry || !Compatible(first, e) || e.SeriesUid != first.SeriesUid || e.FrameUid != first.FrameUid || e.PatientKey != first.PatientKey)
                { warning = "Inconsistent image geometry or frame of reference."; return false; }
                if (e.Frames != 1) { warning = "Enhanced or multiframe image geometry is not supported for MPR."; return false; }
            }
            step = (stack.Entries[stack.Entries.Count - 1].Origin - first.Origin).Dot(normal) / (stack.Entries.Count - 1);
            if (!Finite(step) || step <= 0.0001) { warning = "Duplicate or unordered image positions."; return false; }
            double tolerance = Math.Max(0.01, step * 0.005);
            for (int i = 0; i < stack.Entries.Count; i++)
            {
                Vec3 delta = stack.Entries[i].Origin - first.Origin;
                double offset = delta.Dot(normal);
                if (!Finite(offset) || (delta - normal * offset).Length > 0.02)
                { warning = "In-plane image drift or gantry tilt prevents safe MPR."; return false; }
                if (Math.Abs(offset - step * i) > tolerance || (i > 0 && (stack.Entries[i].Origin - stack.Entries[i - 1].Origin).Dot(normal) <= 0.0001))
                { warning = "Irregular or duplicate image positions prevent safe MPR."; return false; }
            }
            return true;
        }

        internal static string Text(DicomDataset d, DicomTag tag) { string s; return d.TryGetString(tag, out s) ? s ?? "" : ""; }
        internal static int Number(DicomDataset d, DicomTag tag, int fallback) { int v; return d.TryGetValue(tag, 0, out v) ? v : fallback; }
        internal static double Real(DicomDataset d, DicomTag tag, double fallback) { double v; return d.TryGetValue(tag, 0, out v) ? v : fallback; }
        internal static bool Finite(double v) => !double.IsInfinity(v) && !double.IsNaN(v);
    }
}
