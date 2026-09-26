using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using QuickLook.DicomRT;

static class Acceptance
{
    // Opt-in local, read-only acceptance. Only aggregate output, never source paths or identifiers.
    public static int Run(string root)
    {
        try
        {
            var timer = Stopwatch.StartNew();
            var folders = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(Path.GetDirectoryName).Distinct().ToList();
            int files = 0, skipped = 0, stacks = 0, mpr = 0, planes = 0, volumes = 0, failures = 0, tags = 0;
            long firstPlaneMs = -1, scanMs = 0, volumeMs = 0;
            var modalities = new Dictionary<string, int>();
            foreach (string folder in folders)
            {
                var watch = Stopwatch.StartNew();
                var catalog = DicomCatalog.Scan(Directory.EnumerateFiles(folder).First(), CancellationToken.None);
                scanMs += watch.ElapsedMilliseconds;
                files += catalog.Files.Count; skipped += catalog.SkippedFiles; stacks += catalog.Stacks.Count;
                foreach (var e in catalog.Files)
                {
                    if (!modalities.ContainsKey(e.Modality)) modalities[e.Modality] = 0;
                    modalities[e.Modality]++;
                    tags += TagReader.Read(e.Dataset).Count;
                }
                foreach (var s in catalog.Stacks)
                {
                    try
                    {
                        watch.Restart(); var plane = PixelPlane.Load(s.Entries[s.Entries.Count / 2]); planes++;
                        if (firstPlaneMs < 0) firstPlaneMs = watch.ElapsedMilliseconds;
                        if (s.CanMpr)
                        {
                            mpr++; watch.Restart();var volume = VolumeData.Load(s, CancellationToken.None); volumeMs += watch.ElapsedMilliseconds;
                            if (float.IsNaN(volume.Sample(volume.Center))) throw new InvalidOperationException();
                            volumes++;
                        }
                    }
                    catch { failures++; }
                }
            }
            Console.WriteLine("Acceptance aggregate: files="+files+", skipped="+skipped+", stacks="+stacks+", mprStacks="+mpr+", decodedPlanes="+planes+", loadedVolumes="+volumes+", failures="+failures+", tagRows="+tags);
            Console.WriteLine("Timings ms: catalog="+scanMs+", firstPlane="+firstPlaneMs+", volumes="+volumeMs+", total="+timer.ElapsedMilliseconds);
            foreach (var m in modalities.OrderBy(x=>x.Key)) Console.WriteLine("Modality "+m.Key+": "+m.Value);
            return failures==0&&files>0?0:1;
        }
        catch (Exception e) { Console.WriteLine("Acceptance failed ("+e.GetType().Name+")"); return 1; }
    }
}
