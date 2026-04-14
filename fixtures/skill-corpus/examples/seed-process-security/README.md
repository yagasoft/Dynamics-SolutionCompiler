# Seed Process Security

This neutral unmanaged seed is the current reusable foothold for the `Security and Access` family.

It intentionally stays compact:
- one security role shell
- one secured attribute shell on `cdxmeta_workitem.cdxmeta_details`
- one field security profile
- one real field permission tied to that secured attribute
- one connection role

Use it for:
- source inventory of unpacked security artifacts
- normalization and drift checks for stored definitions
- understanding how secured attributes and field permissions round-trip once the attribute is actually secured
- understanding where readback is honest versus best-effort

Important limits:
- role privileges are source-first and should not be confused with effective runtime access
- effective access still needs direct `Dev` proof with users, teams, or business units
- entity-level drift in this seed is intentionally broader than the single secured-field change because readback expands the surrounding table family
- process-policy artifacts such as duplicate rules, routing rules, and SLAs are not represented in this seed yet
