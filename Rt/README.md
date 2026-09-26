# RT interpretation — 0.2.2

The readers operate on local DICOM data and do not modify source files. They do not log identifiers or paths.

- Spatial REG matrices map each source RCS into the registered RCS. Sequence matrices are applied in listed order (`M3 * M2 * M1`). Mapping between two items of the same REG is `inverse(targetToRegistered) * sourceToRegistered`. No paths are composed through different REG objects. A transform resolves only for known equal frames or a unique direct mapping; conflicting transforms and empty frames do not resolve. Exact referenced image instances may establish an otherwise absent source frame. Deformable registrations are unsupported.
- Dose pixels are decoded as signed or unsigned 16/32-bit values; `DoseGridScaling` is applied before conversion to float. Units remain the DICOM units, including `RELATIVE`. Dose sampling supports irregular and descending frame offsets, oblique axes, and the legacy absolute axial offset convention. A uniform `VolumeData` is exposed only for regular multiframes. Allocation is limited to 512 MiB of dose samples.
- Structure contours retain geometric type, reference images, and original points. ROI navigation chooses a representative real component, using an interior point when possible and a point on its boundary otherwise. It does not jump into empty space between disjoint components. Open contours do not gain a fictitious closing edge.
- Conventional RTPLAN control points retain inherited angles, isocenter, jaws and independent MLC layers. Boundaries come from beam device definitions. Numeric-suffix vendor forms such as `MLCX1` / `MLCX2` and duplicate classic device occurrences are supported; suffixes do not imply proximal/distal ordering. Whole omitted device groups inherit; partial duplicate groups require an unambiguous pair count. Initial positions are required for every defined layer. Malformed updates reject parsing. Legacy single-layer fields expose the first layer. Enhanced `(3008,00A1/A2)` devices, unknown limiting devices and ion plans remain unsupported. Missing scalars and conflicting fraction-group beam metersets remain NaN.

RT readers are independent of image loading: MLC needs no image or dose; structure-only geometry can populate 3D; matching structures and dose can populate DVH without a plan or CT. Dose sums remain explicit user selections. DVH uses 2,048 dose intervals, contour slabs with half-spacing end caps and adaptive trilinear sampling. Linear display interpolation does not establish TPS equivalence.

Normative geometry references:

- [DICOM PS3.3 C.20.2 Spatial Registration Module](https://dicom.nema.org/medical/dicom/current/output/chtml/part03/sect_C.20.2.html)
- [DICOM PS3.3 C.8.8.14 RT Beams Module](https://dicom.nema.org/medical/dicom/current/output/chtml/part03/sect_C.8.8.14.html)
- [DICOM PS3.3 C.8.8.14.8 Machine Rotations](https://dicom.nema.org/medical/dicom/current/output/chtml/part03/sect_C.8.8.14.8.html)
- [DICOM PS3.3 C.8.8.3.2 Grid Frame Offset Vector](https://dicom.nema.org/medical/dicom/current/output/chtml/part03/sect_C.8.8.3.2.html)

`Rt.Tests` contains synthetic assertions and an optional `--private <folder>` mode. Private mode prints only aggregate counts. Neither synthetic tests nor file-level read-only acceptance constitutes clinical validation.

`--private-mlc <folder>` limits acceptance to plan/MLC interpretation and reports aggregate plan, beam, control-point and dual-layer counts. It creates no images or DICOM copies.
