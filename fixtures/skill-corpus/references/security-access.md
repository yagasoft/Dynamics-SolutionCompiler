# Security and Access

Use this reference for Dataverse security definitions and for deciding how much of a security change can be proven directly in `Dev` versus compared from source or treated as best-effort readback.

The main rule is simple: compare the definition you own, not the effective access you merely observe. Effective access is shaped by users, teams, business units, hierarchy, ownership, and field security at runtime.

## Roles

Security roles are the definition surface for access grants.

- Source: track the role definition, the privilege set, and any role-component linkage in the unpacked solution or source tree.
- Direct `Dev` proof: assign the role to a test user or team and validate actual allowed actions in the app.
- Readback: best-effort for definition parity; effective permissions are environment-specific and should not be treated as a pure source export.

## Role Privileges

Role privileges are the core of access reasoning because they define the operation, the target scope, and the access depth.

- Source: compare by privilege name, object type, and depth rather than by display wording alone.
- Direct `Dev` proof: use a real secured operation and confirm the role can or cannot execute it.
- Readback: compare the role definition and privilege list, but do not assume the readback reflects all runtime inheritance or indirect access.

## Privilege Object Type Codes

Privilege object type codes tie privileges to the protected object.

- Source: treat the numeric code as a mapping key that should be resolved to the underlying table or component where possible.
- Direct `Dev` proof: validate against the concrete table or secured object the user actually touches.
- Readback: best-effort only; the code list can be incomplete or require lookup context, so it should not be treated as a standalone truth source.

## Field Security Profiles And Permissions

Field security profiles define field-level access, and field permissions define read, create, and update behavior.

- Source: track the secured attribute shell, the profile definition, and the per-field permissions separately.
- Direct `Dev` proof: first ensure the target attribute is actually `IsSecured`, then impersonate a user with the profile and verify the field is hidden, read-only, or editable as expected.
- Readback: useful for definition drift once both the secured attribute and the profile are in scope, but the real behavior still depends on form design and any business rules that touch the field.
- Neutral example: `references/examples/seed-process-security/` now includes a real secured memo attribute plus a matching field permission, so the skill can distinguish definition drift from runtime access questions.

## Connection Roles

Connection roles define which relationship patterns are allowed for connections.

- Source: keep the role definition and any related object-type mapping under source control.
- Direct `Dev` proof: create or edit a connection and confirm the allowed relationship behaves as expected.
- Readback: best-effort because existing records, security, and org configuration can affect what is visible.

## Practical Guidance

- Use tracked source for durable release artifacts.
- Use direct `Dev` proof when the question is "can a user actually do this?"
- Use best-effort readback when the question is "what definition did Dataverse store?"
- Do not infer effective access from role XML alone.
- Do not treat role-privilege readback gaps as proof that the source definition is wrong; compare the owned definition and then use live proof for effective access.
- Keep security guidance project-agnostic; only add project-specific examples in a separate reference if the task explicitly requires them.
