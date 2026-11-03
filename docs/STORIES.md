# macOS completion stories

## Find a split export automatically

Acceptance: one selected image or RT object finds referenced CT, plan, structures and dose in nearby nested folders without an OS folder picker. Multiple matching plans and large search scopes ask before guessing or expanding.

UX:
- Flow: Finder Space / Open DICOM → selected-file preview → bounded reference search → full context, or choose a plan/search scope → full context. Escape/Keep current view cancels.
- Surface: existing loading strip and one existing HTML sheet for a short choice; no new toolbar or folder-permission button.
- States: selected content remains visible while searching; no references and denied access stay inline; many choices scroll and include labels/counts. Success uses the normal context controls.
- Words: Plan auswählen, Suchbereich auswählen, Weiter suchen, Aktuelle Ansicht behalten. A search scope selects existing discovered folders, not a filesystem permission.
- Access: labeled select and action buttons, initial keyboard focus, Enter submits, Escape cancels; visible text supplements colors. Stale selections cannot apply to another document.

## Compare associated doses

Acceptance: associated dose grids can be independently displayed; DVH selection changes numerical results without resetting ROI visibility, image navigation or display references. Exact opened doses and missing registrations remain explicit.

UX:
- Flow: Doses tab → toggle source grids; DVH workspace → DVH-Dosis selector → matching curves/metrics.
- Surface: inline toggles and selector; values change within the existing workspaces.
- States: pending reference, no association and no registration have inline reasons; ambiguous source doses are distinguishable choices.
- Words: Dosis, DVH-Dosis and Dosisreferenz remain distinct.
- Access: labeled native HTML controls, keyboard selection and visible focus; colors supplemented by labels/visibility states.

## Navigate and inspect source geometry

Acceptance: RT-only slices, all mapped isocenters, dose maxima, neutral field selection, shared CP playback, selectable orientation figures and RT tag sources are available.

UX:
- Flow: view control / shortcut → chosen plane; ISO → choose source point if needed; Dose maximum → actual mapped maximum; Tags → source → search.
- Surface: existing toolbars/sidebar; small anchored ISO choice; inspector for metadata.
- States: empty/unsupported geometry is explicit; staged opening retains first useful content while the context is discovered.
- Words: Native, Axial, Coronal, Sagittal, 2 × 2, 3D; SVG symbols supplement visible text.
- Access: guarded Command 1–6, Command I/F, Home and Option workspace shortcuts. Text editing and settings keep their keyboard input.

## Animate MLC with orientation context

Acceptance: actual jaws/leaf tips, exact-angle DRRs/contours, source LINAC identity and grouped move/resize/reset orientation context work without regenerating CT for leaf-only motion.

UX:
- Flow: MLC → field/CP/playback → synchronized aperture and orientation; drag header or keyboard arrows to move; grip or Shift+arrows to resize; reset restores position.
- Surface: existing MLC tile and its compact orientation card; axial arrangement appears when Fields is enabled.
- States: immediate aperture, progressive preparation/counts, unavailable projection reason and final refinement.
- Words: Blenden, Konturen, Fields, CP/s and source machine label.
- Access: explicit control names, visible jaw labels, dark-backed screen-space edges and keyboard alternative for moving/resizing.

## Export inspection results

Acceptance: whole-view PNG, individual-view PNG, dedicated full DVH chart PNG, detailed/status CSV, curve-only CSV and reference-resolved ZIP remain separate actions.

UX:
- Flow: existing export button/context action → native save sheet → saved status; Escape cancels.
- Surface: native asynchronous save sheet, existing ZIP part choices.
- States: uncomputed rows are NotCalculated in detailed CSV; dedicated DVH chart preserves partial/unsupported states and equal curve opacity.
- Words: explicit output type and scope; identifiers remain local export content.
- Access: standard save sheet keyboard behavior and Command Shift S for whole-view PNG.
