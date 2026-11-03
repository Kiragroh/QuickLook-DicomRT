#!/usr/bin/env python3
"""Exercise the actual preview entitlements, without bookmarks or file-panel grants."""
import json
import pathlib
import plistlib
import subprocess
import tempfile

ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE = r'''import Foundation
let root=URL(fileURLWithPath:CommandLine.arguments[1])
let file=root.appendingPathComponent("sibling.dcm")
var readable=false,enumerated=false,writable=false,created=false
do { readable = try Data(contentsOf:file) == Data("fixture".utf8) } catch {}
do { enumerated = try FileManager.default.contentsOfDirectory(at:root,includingPropertiesForKeys:nil).contains { $0.lastPathComponent == "sibling.dcm" } } catch {}
do { let h=try FileHandle(forWritingTo:file);writable=true;try h.close() } catch {}
do { try Data([0]).write(to:root.appendingPathComponent("must-not-create"));created=true } catch {}
let result=["readable":readable,"enumerated":enumerated,"writable":writable,"created":created]
print(String(data:try JSONSerialization.data(withJSONObject:result),encoding:.utf8)!)
'''


def main():
    entitlements = ROOT / "Mac/Native/Preview.entitlements"
    with tempfile.TemporaryDirectory(prefix="quicklookrt-folder-access-") as work:
        work = pathlib.Path(work)
        app = work / "AccessProbe.app"
        executable = app / "Contents/MacOS/probe"
        executable.parent.mkdir(parents=True)
        with (app / "Contents/Info.plist").open("wb") as output:
            plistlib.dump({"CFBundleExecutable": "probe", "CFBundleIdentifier":
                          "de.kiragroh.QuickLookRT.FolderAccessProbe"}, output)
        source = work / "probe.swift"
        source.write_text(SOURCE)
        subprocess.run(["xcrun", "swiftc", str(source), "-o", str(executable)], check=True)
        subprocess.run(["codesign", "--force", "--sign", "-", "--entitlements",
                        str(entitlements), str(app)], check=True, capture_output=True)
        # Neither location is the dedicated library or covered by a saved bookmark.
        with tempfile.TemporaryDirectory(prefix="quicklookrt-ungranted-", dir=ROOT.parent) as home_folder:
            for folder in [work / "arbitrary folder with spaces", pathlib.Path(home_folder)]:
                folder.mkdir(exist_ok=True)
                fixture = folder / "sibling.dcm"
                fixture.write_bytes(b"fixture")
                result = subprocess.run([str(executable), str(folder)], check=True,
                                        capture_output=True, text=True)
                state = json.loads(result.stdout)
                assert state == {"readable": True, "enumerated": True,
                                 "writable": False, "created": False}, state
                assert fixture.read_bytes() == b"fixture"
                assert not (folder / "must-not-create").exists()
        print("PASS folder access: two ungranted locations readable; writes denied; fixtures unchanged")


if __name__ == "__main__":
    main()
