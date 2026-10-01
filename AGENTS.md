# Maintainer rules

- After testing a new LINAC model, update `docs/linac-compatibility.md` in the same task. Keep the public list short: model, manufacturer and a brief test status. Keep the README link current.
- Record supporting evidence separately in `docs/linac-test-evidence.md`: viewer version, test date, source type (original, reconstructed or synthetic), test scope and material limitations. Name recognition and local machine aliases alone are not proof of export compatibility.
- Publish no patient identifiers, source filenames, DICOM UIDs or internal share paths. Unknown machine identity stays unknown. Keep local alias rules and detailed implementation notes out of the short compatibility list.
