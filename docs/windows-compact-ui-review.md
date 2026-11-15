# EGM review: compact Windows controls

Target: windows-compact-ui-plan.md, original lines 1-33. Source reference: [Mac-port transfer guide](https://github.com/Kiragroh/QuickLook-DicomRT/blob/ed55d65f85e623d531367a7704d44cf7c0668b42/docs/windows-design-improvements.md).

Two independent, fresh-conversation reviewers completed read-only reviews of the full plan. Both read their EGM persona, assigned standard and all supplied source context. No private data was supplied. Conversation isolation does not establish exclusion of host instructions. This was a bounded design/readiness review, not a claim of full EGM loop convergence. Implementation was authorized by the user.

| ID | Reviewer | Severity | Finding | Disposition |
|---|---|---|---|---|
| design-1-1 | design_critic | Major | Alternatives to global compact styles and horizontal overflow are missing | PROPOSE FIX applied: alternatives and drawbacks documented |
| design-1-2 | design_critic | Major | DPI tests omit whole-viewer sidebar width constraints | PROPOSE FIX applied: whole viewer at 1024/1600 DIP and 100/150/200% scale; existing 923-DIP two-panel limit documented; no new responsive-layout claim |
| readiness-1-1 | implementation_readiness | Major | UI labels could leak into generated sum/export identity; sums lack ordinal policy | PROPOSE FIX applied: original identity helper retained, separate display helper and explicit generated-sum label; regression assertions |
| readiness-1-2 | implementation_readiness | Major | Build phases and file/test boundary unspecified | PROPOSE FIX applied: exact source/test/release paths and commands documented |
| readiness-1-3 | implementation_readiness | Major | Jaw/layer clipping and closed-field verification underspecified | PROPOSE FIX applied: final tip pass, LayerIndex, actual jaw bounds, crossed/equal intervals closed; compact miniature unchanged |
| readiness-1-4 | implementation_readiness | Nit | Weak cache lifetime and concurrent access untested | PROPOSE FIX applied: lazy entries, weak-lifetime and parallel-access checks; timing remains informational |

The design reviewer read Theme.cs, Theme.xaml, ViewerNavigation.cs, ViewerControl.cs, MlcPlaybackControl.cs, ViewerLoading.cs and the transfer guide. The readiness reviewer read MlcPlaybackControl.cs, MlcAnatomy.cs, MlcProjectionCache.cs, BeamFieldOverlay.cs, ViewerLoading.cs, ViewerWorkspace.cs, ViewerRt.cs, Rt/PlanData.cs, Interaction.Tests/Program.cs and the guide. Their full reports were returned before synthesis.

## Final bounded source review and follow-up

A third fresh-context implementation-readiness reviewer read the full updated plan, report and 14 supplied source/test files. The review completed without builds or runtime claims. Findings were resolved as follows:

- Closed picker tooltip could remain on an older selection: bind it to the current selected item's hover property; regression changes dose selection and checks the new maximum.
- File boundary omitted dispatcher correction: all paths now repository-relative with Create/Modify labels, including the startup scenario and regression.
- Factory-only compact ComboBox style missed standalone 3D: implicit ComboBox style now supplies 28-DIP minimum and 12-DIP text.
- Dose tooltip metadata absent: a shared detail formatter supplies original label, summation type, maximum and units.
- Concurrent test only installed entries: both lazy properties are now read during parallel cold access.

Native inspection before the user stopped computer use verified the gear opening and successful synthetic-case loading after the dispatcher correction. Further native click checks were not completed. Automated WPF render/layout checks cover both sidebars at 1024/1600 DIP and 96/144/192 DPI; these are not claims of native mouse/keyboard verification at each Windows scaling setting.

The 2026-11-15 DVH additions have targeted tests for 3D/DVH reparenting and cache identity, no DVH work on slice redraw, fixed Gy ticks, prescription association/unit/summation exclusions, parsing multiple TARGET prescriptions, and curve hit testing with an Rx-expanded axis. Offscreen screenshots remain local test artifacts. No full EGM-loop convergence or macOS runtime validation is claimed.

Final automated result: 867 WPF interaction checks passed with no build warnings. The owning viewer cancels DVH work on workspace exit/disposal; reparenting does not cancel the new calculation through a deferred Unloaded event.
