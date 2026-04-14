# Source-Only SLA

This compact example is a source-only parser fixture for the `SLA` family.

Use it when:
- you need a concrete XML shape for `SLA` and `SLAItem` source analysis
- you want to exercise the inventory and normalization scripts without claiming live SLA provisioning or readback parity
- the current environment does not provide a clean neutral `IsSLAEnabled` table for unattended SLA seeding

What it includes:
- one compact unpacked solution shell under `unpacked/`
- one `SLA` definition with one nested `SLAItem` in `Other/Customizations.xml`
- generated `inventory.md`
- generated `normalized/`

Important limits:
- this is not a live neutral Dev seed
- it is intentionally labeled source-only because the current environment still needs an SLA-enabled neutral table before honest live seeding can happen
- use it to study parser shape and normalization, not as proof of runtime SLA behavior
