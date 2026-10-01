# LINAC test evidence

[Short compatibility list](linac-compatibility.md) · reviewed **1 October 2026**, viewer **0.2.35**.

## Read-only archive recheck

The archive contains 244,481 files. The bounded recheck inspected 648 RP-prefix/RTPLAN-name candidates: 645 were RTPLANs and 644 parsed successfully. One plan failed parsing; three candidates could not be read by the inventory reader. Other filenames and packed archives were not exhaustively tested. No source files were changed or published.

The earlier inventory searched beam model tags for a limited set of literal names. The recheck also recognized numeric Clinac model identifiers and examined TreatmentMachineName separately. Top-level manufacturer/model fields were inspected separately because they may identify the TPS rather than the treatment machine. No additional LINAC family was established from the top-level model fields.

| Family | Explicit beam model evidence | Device-name evidence in successfully parsed plans |
|---|---:|---:|
| ARTISTE | 28 beams, manufacturer Siemens | 1,148 beams |
| Clinac | 109 beams, manufacturer Varian | 356 beams |
| Ethos | — | 10 beams |
| TrueBeam | — | 42 beams |
| PRIMUS | — | 350 beams |
| Synergy | — | 11 beams |
| Versa | — | 33 beams |

Counts are beams, not independent plans or devices. The two columns can overlap and must not be added. Device names support a named-family inventory but do not independently establish the precise hardware variant or export version. The existing user-identified Halcyon viewer cases provide separate evidence; local naming conventions alone are not model verification.

For the explicitly identified ARTISTE beams, 140 control-point projections were accepted. For Clinac, 5,373 were accepted and two were unavailable. The remaining 7,587 beams did not have a recognized explicit model. In the preceding aggregate audit, 145,265 of 152,093 control points admitted the projection model; 6,730 lacked usable source-axis distance and 98 had other unsupported/incomplete geometry. These are parser/projection checks, not rendered-frame comparisons against a TPS. The parse failure involved unsupported/missing block contour data.

The new device-name findings extend **parser evidence**, not full model-specific rendering or delivery validation. Successful conventional RTPLAN parsing does not establish robot, helical or ion-delivery support.

## Accela and orientation models

Ten reconstructed/synthetic Accela-related RTPLAN fixtures contained 3,277 control points and 6,008 leaf-position arrays compared after parsing. No original Accela TPS export has been tested. A complete synthetic set contains 73 CT slices, seven ROIs, two dynamic dual-layer arcs and analytic test dose; integration tests exercised DRRs, outlines, DVHs and surfaces. The dose is not calculated from the beams. [Detailed scope](accela-support.md).

Automated orientation-widget tests exercise Accela, Halcyon and Ethos model selection and 120 angle updates per model. See [machine-display tests](../Interaction.Tests/AccelaMachineScenarios.cs), [device/parser tests](../Rt.Tests/EnhancedDeviceScenarios.cs) and [complete phantom integration](../Interaction.Tests/AccelaPhantomAcceptance.cs).

## Reproducible aggregate parser check

```powershell
dotnet build Rt.Tests -c Release
Rt.Tests\bin\Release\net462\Rt.Tests.exe --linac-plan-candidates <private-root>
```

This retained aggregate runner uses the earlier literal model-name buckets. Its unknown-model count must not be interpreted as an exhaustive device inventory. The separate 1 October recheck above expands identity evidence without changing the viewer's model recognition or geometry.

## Manufacturer references

- [Accela — Siemens Healthineers / Varian](https://www.siemens-healthineers.com/press/releases/accela)
- [Halcyon, Ethos and TrueBeam — Varian](https://www.varian.com/about-varian/newsroom/press-releases/together-siemens-healthineers-varian-showcases-comprehensive-0)
- [ARTISTE and PRIMUS — Siemens](https://www.siemens-healthineers.com/br/radiation-oncology/early-ro-systems)
- [Versa HD — Elekta](https://www.elekta.com/products/radiation-therapy/versa-hd/)
- [Synergy — Elekta](https://ir.elekta.com/investors/press-releases/2013/elekta-receives-us-fda-510k-clearance-following-launch-of-new-versa-hd-radiation-therapy-system-for-cancer-treatment/)

Manufacturer references identify products; they are not endorsements of this viewer. No patient identifiers, raw machine aliases, DICOM UIDs or internal paths are included here.
