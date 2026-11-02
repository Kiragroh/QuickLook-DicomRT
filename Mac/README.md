# QuickLookRT for macOS (development)

Native AppKit application and Finder Quick Look preview extension, using the original Windows DICOM/RT algorithms with a local WKWebView interface. No network service is involved. The application uses the original QuickLookRT icon and viewer colors/layout.

## Build and install

Requires macOS 13+, Apple Command Line Tools and .NET SDK 10. Set `DOTNET` if the SDK is not on the default project-local tool path. Run `Mac/scripts/build.sh`, then `Mac/scripts/install.sh`. Builds are self-contained; the installed application does not need a separate .NET installation. `ARCH=arm64` and `ARCH=x86_64` are supported build targets. A Developer ID identity can be supplied with `SIGN_IDENTITY`; the default local build is ad-hoc signed.

The application is installed in `~/Applications/QuickLookRT.app`. The installer backs up the previous application and registers the bundled Quick Look extension. Close an existing preview before reopening it with Space. A sandboxed Finder preview can request access to the containing folder to discover related images, plans, structures and doses; folder authorization is retained in a security-scoped bookmark.

## Current scope and validation

The development port includes linked image/MPR views, slice scrolling, plan selection, structures and contour-description labels, DVH calculation, MLC control-point playback, DRRs, 3D surfaces, dose/fusion controls, preferences and native export dialogs. Original core and RT regression tests can be run through `Mac/Tests/Core` and `Mac/Tests/Rt`. `Mac/Tests/test_worker.py` checks the JSON worker against synthetic DICOM fixtures. `Mac/scripts/smoke.sh` checks the embedded native/WebKit rendering path. `Mac/Tests/PrivateAcceptance.swift` is a local UI acceptance harness for independently supplied examples and emits aggregate results only.

Full Windows feature parity and Finder integration across all dataset arrangements are still under verification. This branch is a development preview, not a completed parity release. Multiple simultaneous dose overlays, full preparation/loading behavior and some detailed Windows interaction controls still need comparison. Private DICOM files and test artifacts must remain outside tracked source.

The bundled `Web/graphics.js` is built from `Graphics/renderer.js` with `npm ci --prefix Mac` and `npm run build --prefix Mac`. Its Three.js license is included in `Web/THREE-LICENSE.txt`.
