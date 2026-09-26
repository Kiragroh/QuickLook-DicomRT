using System;
using System.Collections.Generic;
using System.Text;
using Dicom;

namespace QuickLook.DicomRT
{
    public static class TagReader
    {
        public static List<TagRow> Read(DicomDataset dataset)
        {
            var result = new List<TagRow>();
            if (dataset != null) Append(dataset, "", 0, result);
            return result;
        }
        private static void Append(DicomDataset dataset, string parent, int depth, List<TagRow> rows)
        {
            foreach (var item in dataset)
            {
                string tag = item.Tag.ToString();
                string path = parent.Length == 0 ? tag : parent + "/" + tag;
                string name = new string(' ', depth * 2) + item.Tag.DictionaryEntry.Name;
                var sequence = item as DicomSequence;
                if (sequence != null)
                {
                    rows.Add(new TagRow { Path = path, Tag = tag, Name = name, VR = "SQ", Value = sequence.Items.Count + " item(s)" });
                    for (int i = 0; i < sequence.Items.Count; i++)
                    {
                        string child = path + "[" + i + "]";
                        rows.Add(new TagRow { Path = child, Tag = "", Name = new string(' ', (depth + 1) * 2) + "Item [" + i + "]", VR = "", Value = "" });
                        Append(sequence.Items[i], child, depth + 2, rows);
                    }
                    continue;
                }
                string value;
                var element = item as DicomElement;
                string vr = item.ValueRepresentation.Code;
                if (item.Tag == DicomTag.PixelData || item.Tag == DicomTag.FloatPixelData || item.Tag == DicomTag.DoubleFloatPixelData)
                    value = "[pixel payload omitted]";
                else if (element == null) value = "[binary payload omitted]";
                else if (vr == "OB" || vr == "OW" || vr == "UN" || vr == "OF" || vr == "OD" || vr == "OL" || vr == "OV")
                    value = "[" + element.Length + " bytes; binary payload omitted]";
                else
                {
                    // Cap display only; never truncate the sequence tree or load pixel buffers.
                    const int limit = 8192;
                    var text = new StringBuilder();
                    try
                    {
                        for (int i = 0; i < element.Count; i++)
                        {
                            if (i > 0) text.Append('\\');
                            string part = element.Get<string>(i) ?? "";
                            int room = limit - text.Length;
                            if (room <= 0 || part.Length > room)
                            {
                                if (room > 0) text.Append(part, 0, room);
                                text.Append(" … [truncated]"); break;
                            }
                            text.Append(part);
                        }
                        value = text.ToString();
                    }
                    catch (Exception ex) when (ex is DicomException || ex is InvalidCastException || ex is FormatException)
                    { value = "[value unavailable]"; }
                }
                rows.Add(new TagRow { Path = path, Tag = tag, Name = name, VR = vr, Value = value });
            }
        }
    }
}
