# Goldfish review report

Target: [macOS parity plan](macos-parity-plan.md), original lines 1–43.

Both reviewers completed a fresh-context, read-only review. This was one review round; it is not a claim of the full EGM loop converging. The user explicitly authorized implementation and delivery. The design workflow ends after applying these findings; implementation follows the revised plan.

| ID | Reviewer | Severity | Finding | Resolution |
| --- | --- | --- | --- | --- |
| codebase-1-1 | codebase_details | Blocker | Serialization was wrongly said to hold session lock | PROPOSE FIX applied: distinguish render/Base64, serialization and native queue |
| readiness-1-1 | implementation_readiness | Blocker | Multi-dose selection and normalization contract undefined | PROPOSE FIX applied: separate legacy source selection, active overlays, DVH choice and per-dose references |
| codebase-1-2 | codebase_details | Major | RT-only reformat navigation missing | PROPOSE FIX applied: canReformat from RT bounds, synthetic coverage |
| codebase-1-3 | codebase_details | Major | RT tag sources not selectable | PROPOSE FIX applied: explicit validated source picker |
| codebase-1-4 | codebase_details | Major | MLC context lacks axial field arrangement | PROPOSE FIX applied: cached slice plus movable LINAC card |
| codebase-1-5 | codebase_details | Major | Image field playback missing and must not rerasterize anatomy | PROPOSE FIX applied: fields-only protocol and shared controls |
| codebase-1-6 | codebase_details | Major | DVH PNG is whole viewer, missing original report semantics | PROPOSE FIX applied: dedicated independent chart composition |
| codebase-1-7 | codebase_details | Major | First usable content waits for full scan | PROPOSE FIX applied: staged single-file preview with final context merge |
| codebase-1-8 | codebase_details | Major | Standalone RT associations incomplete | PROPOSE FIX applied: deterministic selection table and exact-dose preservation |
| readiness-1-2 | implementation_readiness | Major | Cancellation/snapshot contract undefined | PROPOSE FIX applied: document/context/group revisions and tokens |
| readiness-1-3 | implementation_readiness | Major | Cache bounds undefined | PROPOSE FIX applied: explicit byte/entry/scene caps and release policy |
| readiness-1-4 | implementation_readiness | Major | No performance pass/fail gates | PROPOSE FIX applied: baseline method and numeric worker/native gates |
| readiness-1-5 | implementation_readiness | Major | Test cases and fixture limitations vague | PROPOSE FIX applied: numeric multi-dose, negative associations and separate skipped states |
| readiness-1-6 | implementation_readiness | Major | File boundary open-ended | PROPOSE FIX applied: exact modified/created/generated paths |
| readiness-1-7 | implementation_readiness | Major | No buildable phase checkpoints | PROPOSE FIX applied: worker, UI/native and packaging gates |

No findings contested or escalated. Reviewers read the persona and readiness standard. The codebase reviewer read all required worker/native/web and Windows comparison files; the readiness reviewer read all explicitly supplied Mac files and did not claim Windows verification. No private datasets were supplied to either reviewer.

## Final source verification

A further fresh-context codebase reviewer read both final transfer/feature documents and the worker/native/web/graphics sources plus parity tests. It found two Major defects: dose-surface cache identity omitted the resolved Gy threshold, and unit switching rebuilt presets instead of preserving custom levels/colors. Both were fixed. The cache now uses source dose plus physical threshold; unit switching converts existing levels, hidden choices and colors. Native regressions verify changed surface vertices after a manual reference edit and round-trip preservation of custom levels/colors.

Final evidence: 130 worker integration checks; 123 additional selection/export contracts; 36 original plus 30 additional native UI checks for each original case (three conditional arrangement checks skipped per one-plan source); 40 generated native multi-plan/multi-dose checks, zero skipped. The source-review findings are resolved by code and these regressions. This remains an independent verification pass, not a claim of EGM loop convergence.
