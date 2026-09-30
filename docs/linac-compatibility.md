# Machine hints and structure groups — 0.2.34

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
