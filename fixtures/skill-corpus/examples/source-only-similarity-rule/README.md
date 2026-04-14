# Source-Only Similarity Rule

This compact example is a source-only parser fixture for the `SimilarityRule` family.

Use it when:
- you need a concrete XML shape for similarity-rule source analysis
- you want to exercise the inventory and normalization scripts without claiming live round-trip parity
- the environment does not expose normal Web API create or list operations for `similarityrule`

What it includes:
- one compact unpacked solution shell under `unpacked/`
- one `SimilarityRule` definition in `Other/Customizations.xml`
- generated `inventory.md`
- generated `normalized/`

Important limits:
- this is not a live neutral Dev seed
- it is intentionally labeled source-only because the current Dataverse surface in this environment does not support a clean unattended similarity-rule round-trip
- use it to study parser shape and normalization, not as proof of live readback symmetry
