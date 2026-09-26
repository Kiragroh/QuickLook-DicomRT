# Dependencies and references

- **fo-dicom.Desktop 4.0.8 / Dicom.Core.dll:** Microsoft Public License (MS-PL). [Project source](https://github.com/fo-dicom/fo-dicom). The package includes the license text from [licenses/fo-dicom-MS-PL.html](licenses/fo-dicom-MS-PL.html).
- **HelixToolkit.Wpf.SharpDX / HelixToolkit 2.27.3:** MIT, Direct3D 11 rendering and depth-peeling transparency. License: `licenses/HelixToolkit-LICENSE.txt`.
- **SharpDX 4.2.0:** MIT, managed DirectX bindings. License: `licenses/SharpDX-LICENSE.txt`.
- **Cyotek.Drawing.BitmapFont 2.0.0:** MIT, a HelixToolkit dependency. License: `licenses/Cyotek-LICENSE.txt`.
- **Microsoft.Extensions.Logging.Abstractions 6.0.0, System.Memory 4.5.4, System.Buffers 4.5.1, System.Numerics.Vectors 4.5.0 and System.Runtime.CompilerServices.Unsafe 4.5.3:** MIT; runtime compatibility dependencies. Microsoft licenses and third-party notices are included under `licenses/` and in the plugin package.
- **Microsoft.NETFramework.ReferenceAssemblies 1.0.3:** build-time reference assemblies; not redistributed in the plugin package.
- **QuickLook.Common.dll:** supplied by the installed [QuickLook host](https://github.com/QL-Win/QuickLook), not redistributed. Its interface and the MIQ source were inspected for compatible net462/WPF integration. QuickLook remains a separate installation dependency.
- **DICOM semantics:** [PS3.3](https://dicom.nema.org/medical/dicom/current/output/chtml/part03/PS3.3.html), including C.20.2 spatial registration, C.8.8.3 RT Dose, C.8.8.6.3 inner/outer contours, and C.8.8.14 / C.8.8.14.8 RT beams and control-point rotation directions.
- **DICOM Browser:** a feature reference for the independent C# implementation. No Rust source or application binaries are included.
- **Project icon:** generated with GPT Image. The prompt and provenance are recorded in [assets/README.md](assets/README.md).

The source and plugin package contain no private clinical datasets. Synthetic identifiers are deliberately artificial. Public demonstration data and assets are separate from runtime dependencies.
