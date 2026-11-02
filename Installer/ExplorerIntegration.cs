using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace QuickLook.DicomRT.Installer
{
    public static class ExplorerIntegration
    {
        public const string Label = "Open with QuickLookRT";
        // Classic verbs work for extensionless DICOM files as well as named extensions.
        // Windows 11 exposes these under Show more options (Shift+F10).
        static readonly string[] Scopes = { @"*\shell\QuickLookRT", @"Directory\shell\QuickLookRT", @"Directory\Background\shell\QuickLookRT" };

        public static string Command(string executable, bool background = false)
        {
            if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathRooted(executable) || Path.GetPathRoot(executable).Length < 3 ||
                executable.IndexOfAny(new[] { '"', '\r', '\n', '\0' }) >= 0 ||
                !string.Equals(Path.GetFileName(executable), "QuickLook.exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("An absolute QuickLook.exe path is required.");
            // Invoke the host directly, never cmd.exe or PowerShell. Explorer quotes the
            // selected path; characters such as & and parentheses remain filename data.
            return "\"" + Path.GetFullPath(executable) + "\" \"" + (background ? "%V" : "%1") + "\"";
        }

        public static void Register(string executable)
        {
            using (var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes"))
                RegisterAt(classes, executable);
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        }

        // A supplied root permits tests in a private, non-shell registry subtree.
        public static void RegisterAt(RegistryKey classes, string executable)
        {
            string command = Command(executable), backgroundCommand = Command(executable, true);
            if (!File.Exists(executable)) throw new FileNotFoundException("QuickLook is not installed at this path.", executable);
            var undo = new List<Action>();
            try
            {
                foreach (string path in Scopes)
                {
                    Set(classes, path, "", Label, undo);
                    Set(classes, path, "Icon", "\"" + Path.GetFullPath(executable) + "\",0", undo);
                    Set(classes, path, "MultiSelectModel", "Single", undo);
                    Set(classes, path + @"\command", "", path.Contains(@"\Background\") ? backgroundCommand : command, undo);
                }
            }
            catch (Exception failure)
            {
                var errors = new List<Exception> { failure };
                for (int i = undo.Count - 1; i >= 0; i--)
                    try { undo[i](); } catch (Exception restore) { errors.Add(restore); }
                if (errors.Count > 1) throw new AggregateException("Explorer menu registration could not be fully restored.", errors);
                throw;
            }
        }

        static void Set(RegistryKey root, string path, string name, string value, List<Action> undo)
        {
            bool existed;
            using (var prior = root.OpenSubKey(path)) existed = prior != null;
            using (var key = root.CreateSubKey(path))
            {
                object old = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                var kind = old == null ? RegistryValueKind.String : key.GetValueKind(name);
                undo.Add(() =>
                {
                    using (var restore = root.OpenSubKey(path, true))
                    {
                        if (restore == null) return;
                        if (old == null) restore.DeleteValue(name, false); else restore.SetValue(name, old, kind);
                    }
                    if (!existed) root.DeleteSubKey(path, false);
                });
                key.SetValue(name, value, RegistryValueKind.String);
                if (!string.Equals(key.GetValue(name) as string, value, StringComparison.Ordinal))
                    throw new IOException("Explorer menu verification failed.");
            }
        }

        [DllImport("shell32.dll")]
        static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
    }
}
