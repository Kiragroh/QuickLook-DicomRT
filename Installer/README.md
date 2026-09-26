# Windows setup application

The standalone .NET Framework 4.6.2 WPF executable embeds a verified `.qlplugin` archive and its SHA-256 hash. It uses an explicit **Install / Update** button and needs no administrator rights. Build after packaging the plugin:

```powershell
./scripts/Package.ps1
./scripts/BuildInstaller.ps1
```

`BuildInstaller.ps1 -PackagePath <file.qlplugin>` selects a specific package. The default is the most recently written top-level `.qlplugin` in `artifacts`. Output: `artifacts/release/QuickLook-DicomRT-Setup-0.2.1.exe` and its checksum.

The installer supports the standard desktop QuickLook installation. It discovers the executable in the current session or standard program folders. Portable (`portable.lock`) and Microsoft Store hosts are rejected before writes because they use different plugin locations; install the `.qlplugin` through those hosts instead. The **Get QuickLook** button opens the official release page and does not download or install QuickLook automatically.

Setup updates only `%APPDATA%\pooi.moe\QuickLook\QuickLook.Plugin\QuickLook.Plugin.DicomRT`. It backs up an existing plugin folder under `%LOCALAPPDATA%\DicomRT\Backups`, skips identical files, and verifies copied file hashes. It stops only a QuickLook process in the current session whose executable matches the discovered host, waits for exit, and restarts that host hidden. An incomplete rollback leaves QuickLook stopped and reports the backup path. Other plugin folders are untouched.

The payload verifier rejects unknown or duplicate entries, paths and alternate streams, missing required files, oversized data, a different package version, and whole-archive or individual-file hash mismatches. The build runs synthetic archive/path/host-mode tests, then launches the produced EXE with `--verify-payload`. This command verifies the embedded package, reports a count and hash, and exits without opening the setup UI, installing files or changing processes.

Automated verification is deliberately separate from executing installation on the current computer.
