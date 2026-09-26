# RT interpretation

The readers operate on local DICOM data and do not modify source files. They do not log identifiers or paths.

- Spatial REG matrices map each source RCS into the registered RCS. Sequence matrices are applied in listed order (`M3 * M2 * M1`). Mapping between two items of the same REG is `inverse(targetToRegistered) * sourceToRegistered`. No paths are composed through different REG objects. A transform resolves only for known equal frames or a unique direct mapping; conflicting transforms and empty frames do not resolve. Exact referenced image instances may establish an otherwise absent source frame. Deformable registrations are unsupported.
- Dose pixels are decoded as signed or unsigned 16/32-bit values; `DoseGridScaling` is applied before conversion to float. Units remain the DICOM units, including `RELATIVE`. Dose sampling supports irregular and descending frame offsets, oblique axes, and the legacy absolute axial offset convention. A uniform `VolumeData` is exposed only for regular multiframes. Allocation is limited to 512 MiB of dose samples.
- Structure contours retain geometric type, reference images, and original points. ROI navigation chooses a representative real component, using an interior point when possible and a point on its boundary otherwise. It does not jump into empty space between disjoint components. Open contours do not gain a fictitious closing edge.
- Conventional RTPLAN beam control points retain inherited angles, isocenter, jaws, and MLC positions. Leaf boundaries come from beam device definitions. Rotation directions and final cumulative meterset weight are retained for playback. Omitted device positions inherit; explicitly malformed positions reject parsing rather than displaying a stale aperture. Multiple MLC layers, unknown limiting devices, and ion plans are outside the supported aperture model. Missing scalar parameters remain NaN. Different metersets for one beam across fraction groups remain NaN instead of silently choosing one.

Normative geometry references:

- [DICOM PS3.3 C.20.2 Spatial Registration Module](https://dicom.nema.org/medical/dicom/current/output/chtml/part03/sect_C.20.2.html)
- [DICOM PS3.3 C.8.8.3.2 Grid Frame Offset Vector](https://dicom.nema.org/medical/dicom/current/output/chtml/part03/sect_C.8.8.3.2.html)

`Rt.Tests` contains synthetic assertions and an optional `--private <folder>` mode. Private mode prints only aggregate counts. Neither synthetic tests nor file-level read-only acceptance constitutes clinical validation.
