# Code Extensibility

Use this reference for Dataverse extensibility families where the durable truth spans plugin registration metadata, SDK message families, integration endpoints, and source code.

## What Belongs In This Family

- plugin assemblies
- plugin types
- sdk message processing steps
- sdk message processing step images
- sdk message and sdk message filter context
- service endpoints and connectors when present

## How To Reason About It

- treat the plugin assembly as the packaged registration shell
- treat plugin types as the bind point between code and Dataverse registration
- treat steps as runtime registration contracts that depend on message, filter, handler, stage, mode, rank, and filtering attributes
- treat step images as runtime data-shape helpers, not as independent business logic
- treat sdk messages and filters as lookup context for a step, not as the step itself

## Source-First Reality

This family often splits across two planes:

- solution source may carry the assembly and sometimes steps or images
- source code may still be the clearest place where step creation, deletion, message lookup, and image registration logic live

When the solution export only proves the assembly shell, do not pretend that the assembly alone explains runtime behavior. Inventory the registration pattern from code as a separate source-first artifact.

Helper:
- `python scripts/summarize_sdk_registration_source.py <source-file-or-folder>`

Use it when:
- a project creates or updates `sdkmessageprocessingstep` rows from code
- the solution export lacks step rows
- you need a compact source-first view of message, filter, handler, and image registration patterns

## SDK Message Families

For each step, keep these concerns distinct:

- `sdkmessage`: the message such as `Create`, `Update`, or custom pipeline targets
- `sdkmessagefilter`: the entity or primary-object scoping for that message
- `sdkmessageprocessingstep`: the registered execution contract
- `sdkmessageprocessingstepimage`: the registered pre/post image contract

Default rule:
- treat `sdkmessage` and `sdkmessagefilter` as lookup context for a step unless the task truly requires their own metadata surface
- do not present them as first-class authoring artifacts just because source code resolves them explicitly during registration

When readback is available, compare:
- step name
- stage
- mode
- rank
- supported deployment
- filtering attributes
- sdk message name
- primary entity type code
- handler or plugin type name

When readback is not available, keep the family source-first and say so explicitly.

## Service Endpoints And Connectors

Treat `serviceendpoint` and `connector` as adjacent extensibility or integration families with their own durable overlap.

- compare service endpoints on stable transport and registration shell fields such as name, contract, connection mode, auth type, namespace/path/url, message format, message charset, introduced version, and stable `isCustomizable`
- compare connectors on stable identity and descriptive overlap such as internal id, display/name, connector type, normalized capabilities, introduced version, and stable `isCustomizable`
- keep secrets, auth values, tokens, large payload blobs such as `openApiDefinition`, and tenant-local operational state out of stable drift unless a smaller shared overlap is proven
- do not rejoin service endpoints to plugin steps or broaden into connection references unless a later neutral seed proves that contract honestly

## Direct Dev Versus Durable Release

- use direct `Dev` mutation and readback when validating that a step, image, or binding resolves correctly
- use tracked source and PAC packaging for durable assemblies and solution-managed registration artifacts
- when step registration lives in source code, keep that source-first layer explicit instead of forcing it into raw solution XML reasoning

## Current Proof Points

- `references/examples/seed-plugin-registration/` now proves a compact neutral plugin-registration slice across source, tracked-source, package-inputs, live readback, and stable-overlap drift for:
  - `pluginassembly`
  - `plugintype`
  - `sdkmessageprocessingstep`
  - `sdkmessageprocessingstepimage`
- `references/examples/seed-service-endpoint-connector/` now proves a compact neutral integration-endpoint slice across source, tracked-source, package-inputs, live readback, and stable-overlap drift for:
  - `serviceendpoint`
  - `connector`
- `references/examples/dbm-baseline/` still provides a real project-specific plugin assembly reference
- `references/examples/dbm-sdk-registration/` proves a DBM-specific source-first SDK-message registration pattern from code
- code-level SDK registration remains a source-first adjunct rather than a runtime compiler input
