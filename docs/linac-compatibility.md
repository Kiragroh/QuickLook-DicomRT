# LINAC compatibility and test coverage

Last reviewed: **1 October 2026** · Viewer **0.2.35**.

This list records the machines and export features exercised so far. Compatibility depends on the exported DICOM geometry and the TPS/export version as well as the LINAC. The status column distinguishes real-file inspection, parser checks and synthetic tests; it is not a certification of every technique or software version.

## Machines with test evidence

| LINAC / model | Manufacturer | Test evidence | Scope and remaining limits |
|---|---|---|---|
| **Accela** | **Siemens Healthineers / Varian** | **Reconstructed delivery data + synthetic integration tests** | Dual-layer C-arm, indexed paired leaves, dynamic collimator and interpolated arc geometry. Ten reconstructed/synthetic RTPLAN fixtures: 3,277 CPs and 6,008 leaf-position arrays checked. Complete synthetic CT/RTSTRUCT/RTDOSE/RTPLAN set exercised DRRs, target outlines, DVHs and surfaces. **No original Accela TPS export tested yet.** [Detailed evidence](accela-support.md#evidence-and-limits). |
| **Halcyon** | **Varian (Siemens Healthineers)** | **User-identified real viewer cases + automated ring-model tests** | Dual-layer MLC inspection and ring orientation display exercised. The archive also contained 80 beams with the user-confirmed local `Hal*` naming convention; that alias alone does not establish the exact model or export version. Automated tests verify ring selection and gantry/collimator updates. |
| **Ethos** | **Varian (Siemens Healthineers)** | **Synthetic model/display tests** | Model-tag recognition, cutaway ring selection and gantry/collimator updates tested. The retained reports do not independently identify a genuine Ethos export, so full native-export compatibility is not claimed. |
| **ARTISTE** | **Siemens Healthcare / Siemens Healthineers** | **Real archive parser coverage + model-recognition tests** | 28 beams with explicit ARTISTE metadata encountered in the legacy archive audit. C-arm model recognition checked. Archive results are aggregate parser/projection evidence, not an ARTISTE-specific rendered-frame or TPS comparison. |

For Halcyon, Ethos and Accela, automated orientation-widget checks exercise 120 angle updates per named model while retaining the machine meshes. See [machine-display tests](../Interaction.Tests/AccelaMachineScenarios.cs), [device/parser tests](../Rt.Tests/EnhancedDeviceScenarios.cs) and [complete phantom integration](../Interaction.Tests/AccelaPhantomAcceptance.cs).

## Recognized names awaiting model-specific export evidence

These names select a C-arm illustration in the current code. They are listed separately because recognizing a model name is not a compatibility test of its DICOM exports.

| LINAC / model | Manufacturer | Current evidence status |
|---|---|---|
| TrueBeam | Varian (Siemens Healthineers) | Model-name recognition implemented; no separately attributable TrueBeam export result in the retained audit. |
| Clinac | Varian (Siemens Healthineers) | Model-name recognition implemented; no separately attributable Clinac export result in the retained audit. |
| Versa / Versa HD | Elekta | `Versa` model-name recognition implemented; model-specific export testing not documented yet. |
| Synergy | Elekta | Model-name recognition implemented; model-specific export testing not documented yet. |

Other/unspecified machines are handled through their DICOM geometry with a generic illustration. The archive contained many such beams; they cannot reliably be assigned to commercial model names. An unlisted machine is **not automatically incompatible**. Robot, helical and ion delivery must not be inferred from successful conventional RTPLAN parsing.

Product/manufacturer references: [Accela](https://www.siemens-healthineers.com/press/releases/accela), [Varian Halcyon, Ethos and TrueBeam](https://www.varian.com/about-varian/newsroom/press-releases/together-siemens-healthineers-varian-showcases-comprehensive-0), [Siemens legacy LINACs / ARTISTE](https://www.siemens-healthineers.com/br/radiation-oncology/early-ro-systems), [Elekta Versa HD](https://www.elekta.com/products/radiation-therapy/versa-hd/), [Elekta Synergy](https://ir.elekta.com/investors/press-releases/2013/elekta-receives-us-fda-510k-clearance-following-launch-of-new-versa-hd-radiation-therapy-system-for-cancer-treatment/). These identify the products, not endorsement or validation of this viewer.

## Add a tested machine

Record manufacturer/model, TPS and export version when known, viewer version, tested objects/techniques, observed result and limitations. Distinguish original exports from reconstructed or synthetic fixtures. Only publish aggregate findings or approved anonymized test material; keep patient identifiers and internal paths out of issues and compatibility reports.

## Machine illustration rules

The viewer reads beam geometry independently of its schematic machine illustration. Explicit beam ManufacturerModelName is the preferred indication of machine form. A local machine-name convention, `Hal*`, is used only as a labeled ring fallback. Recognized C-arm metadata overrides a conflicting alias. Unrecognized equipment remains a generic schematic; neither two leaf layers nor the top-level TPS manufacturer identifies a ring accelerator.

## Display groups

- **Targets:** explicitly referenced VOLUME/TARGET ROIs in the selected RTPLAN; PTV, CTV, GTV, ITV and TREATED_VOLUME types; names containing PTV.
- **Organs:** ORGAN type, explicitly referenced VOLUME/ORGAN_AT_RISK ROIs, or recognized English/German organ names with conventional laterality/PRV suffixes when type metadata is blank, NONE, UNDEFINED or AVOIDANCE.
- **Other:** unclassified and auxiliary ROIs. Organ-looking helper names such as `Ring_Brainstem` and `Lung_minus_Target` are not automatically organs.
- External/body/skin and support/table/couch contours stay separate. Individual visibility checkboxes remain authoritative. MLC Other can still be enabled to inspect additional outlines; 3D Other excludes external and support surfaces.

Plan references must match both structure-set SOP Instance UID and ROI number; ROI numbers alone are not globally unique. Point/site dose references are not target volume references. Plan-independent structure viewing retains type/name classification. Original names/types and exported DICOM metadata are unchanged. These are display groups, not clinical identification or prescription decisions. Tooltips show the grouping reason.

## Read-only legacy archive test

On 30 September 2026, an inventory counted 244,481 files in 3,825 directories. A bounded scan inspected all 648 RP-prefix/RTPLAN-name candidates, finding 645 RTPLAN files. Archives and differently named plans were not comprehensively examined.

- 644 plan files parsed; 7,724 beams and 152,093 control points examined.
- One plan was rejected for unsupported/missing block contour data. One candidate header was unreadable. No source files were changed or copied into this repository.
- 145,265 CPs admitted the existing projection model. 6,730 lacked usable source-axis distance; 98 had other unsupported/incomplete geometry. Those numbers are parser/projection checks, not rendered-frame or TPS-parity validation.
- 80 beams matched the local Hal prefix; 28 had recognized ARTISTE metadata. Most beam model fields were unspecified/unrecognized, so a generic illustration is retained without affecting readable apertures.
- A separate evenly spaced sample of 32 of 536 RS/RTSTRUCT-name candidates contained 807 ROIs: 226 Targets, 410 Organs, 107 Other, 35 External and 29 Support. Name fallback recovered 24 Targets and 9 Organs beyond type-only grouping. This sample did not infer additional plan-reference groups; exact SOP/ROI association and plan changes are covered by synthetic tests.

Run locally; only aggregate counts are printed:

```powershell
dotnet build Rt.Tests -c Release
Rt.Tests\bin\Release\net462\Rt.Tests.exe --linac-plan-candidates <private-root>
Rt.Tests\bin\Release\net462\Rt.Tests.exe --roi-name-candidates <private-root>
```

The optional `--linac-archive` mode probes DICOM/extensionless headers more broadly and can be slow on network storage. It does not unpack archives. Do not interpret a successful parse or a generic model as support for every vendor technique (for example robot or helical delivery).

References: [DICOM RT Prescription Module](https://dicom.nema.org/medical/dicom/current/output/chtml/part03/sect_c.8.8.10.html), [Siemens ARTISTE](https://www.siemens-healthineers.com/radiation-oncology/upgrades-and-options-for-your-linac/diagnosis-and-follow-up).
