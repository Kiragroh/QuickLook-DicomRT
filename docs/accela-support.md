# Machine identity and Enhanced dual-layer RTPLAN support

Version 0.2.33 supports the paired-leaf subset of Enhanced RT beam limiting devices in RTPLAN, independently of whether the delivery machine is a C-arm or a ring.

## What is read

- Enhanced flag and device/opening sequences `(3008,00A3/A1/A2)`.
- Stable `DeviceIndex` / `ReferencedDeviceIndex` association, independent of sequence order. Omitted device updates inherit by index.
- VARIABLE paired leaves, ascending boundaries, finite 2N positions, IEC X/Y (0/90 degrees), zero offsets.
- Existing classic MLCX/MLCY, numeric-suffix vendor forms and duplicate-device rules.
- Gantry, couch, collimator and explicit rotation directions. Playback interpolates both layers and the collimator across zero degrees.

Other Enhanced device types, nonzero offsets, arbitrary orientations, binary/single-leaf devices, malformed references and incomplete initial openings are rejected. This is not support for all second-generation RT Radiation IODs. Public dictionary additions run before decoding to support Implicit VR.

## Machine display

Beam TreatmentMachineName, Manufacturer and ManufacturerModelName are retained. Explicit model tags take precedence. Accela/TrueBeam/Clinac/ARTISTE and other recognized C-arm models use the C-arm illustration; Halcyon/Ethos use a translucent cutaway ring. Since 0.2.34, the user-confirmed local `Hal*` machine-name convention provides a visibly labeled ring fallback when model metadata is unrecognized. It does not establish a manufacturer/model, modify beam coordinates or infer anything from layer count. Other unknown metadata keeps a generic schematic. These are not machine CAD models or collision checks. Meshes are reused across control points. [Legacy archive and ROI grouping checks](linac-compatibility.md).

## Evidence and limits

Ten local RTPLAN fixtures were inspected: six reconstructions from public delivery data and four wholly synthetic plans. None is an original Accela TPS export. They contain 3,277 control points; 6,008 encoded leaf-position arrays compare exactly after parsing. Native Accela export compatibility still requires a genuine export or conformance statement.

A separate wholly synthetic phantom contains 73 CT slices, seven ROIs, two dynamic arcs (242 control points, 46/47 leaf pairs per layer), and matching analytic RTDOSE. Integration tests check exact CT/RT references, 240 intermediate geometries, PTV DVHs/surfaces, and actual viewer DRR/outline rendering at three poses. The analytic dose is **not calculated from the beams**; machine tags explicitly say synthetic. No patient material is used. These captures must not be presented as clinical examples.

Generate into a new, empty directory:

```powershell
python scripts/CreateAccelaPhantom.py C:\Temp\AccelaPhantom
dotnet build Interaction.Tests -c Release
Interaction.Tests\bin\Release\net462\Interaction.Tests.exe --accela-phantom C:\Temp\AccelaPhantom
```

Open `RTPLAN_SYNTHETIC_DMAT.dcm` with Space. Enable DRR/PTV/Organ outlines, then use the shared field and CP controls in 2 × 2 or 3D. The fixture includes a README and SHA-256 manifest. Playback is a CP preview, not delivery timing. Angular modulation is MU/degree, not delivered MU/min.

## Primary references

- [Enhanced RTPLAN coordinates](https://dicom.nema.org/medical/dicom/current/output/chtml/part03/sect_C.8.8.14.17.html)
- [Device definitions](https://dicom.nema.org/medical/dicom/current/output/chtml/part03/sect_C.36.2.2.19.html) and [openings](https://dicom.nema.org/medical/dicom/current/output/chtml/part03/sect_C.36.2.2.20.html)
- [Varian public DMAT case library](https://github.com/Varian-MedicalAffairsAppliedSolutions/dmat-case-library) (source of the separate local reconstructions, not bundled or executed by this plugin)
