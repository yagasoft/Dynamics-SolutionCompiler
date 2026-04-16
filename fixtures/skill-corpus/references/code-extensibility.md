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

When the solution export only proves the assembly shell, do not pretend that the assembly alone explains runtime behavior. For the supported DBM-style registration shapes the repo seeds prove today, the compiler can now read code directly into the existing plug-in families through a bounded common-idiom static-analysis lane. Outside that bounded lane, inventory the registration pattern from code as a separate source-first artifact.

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
- when the supported code-first registration pattern lives in source code, stage the classic `.dll` or plug-in package `.nupkg` outside the source tree and deploy it through `apply-dev` or `publish`
- regular plug-in packages stay explicit live finalize apply; the repo does not yet claim that `.nupkg` payloads are rebuilt into `solution.zip`
- custom workflow activities stay inside the same plug-in families as a `PluginType` subtype; they do not reopen the owner-level `Workflow` family
- custom workflow activities are classic-assembly only in the current proof surface; plug-in package deployment for them is an explicit permanent boundary aligned with current Microsoft guidance for workflow extensions
- when step registration lives in unsupported code shapes, keep that source-first layer explicit instead of forcing it into raw solution XML reasoning

## Current Proof Points

- `references/examples/seed-plugin-registration/` now proves a compact neutral plugin-registration slice across source, tracked-source, package-inputs, live readback, and stable-overlap drift for:
  - `pluginassembly`
  - `plugintype`
  - `sdkmessageprocessingstep`
  - `sdkmessageprocessingstepimage`
- `references/examples/seed-code-plugin-classic/` now proves the supported narrow code-first read/build/apply path for the same four plug-in families through a staged signed classic assembly
- `references/examples/seed-code-plugin-imperative/` now proves the supported imperative DBM helper registration shape for the same four plug-in families through the same read/build/apply/readback/drift path
- `references/examples/seed-code-plugin-helper/` now proves zero-argument helper-returned `Types`, `Steps`, and `Images` registration collections, including a mixed normal plug-in plus `customWorkflowActivity` type catalog in one classic assembly
- `references/examples/seed-code-plugin-imperative-service/` now proves the more realistic DBM `GetMessage(service, entity, message, handler)` imperative lookup shape for the same four plug-in families
- `references/examples/seed-code-plugin-package/` now proves the supported narrow code-first read/build/publish path for the same four plug-in families through a staged plug-in package with a dependent assembly
- `references/examples/seed-code-workflow-activity-classic/` now proves custom workflow activity registration, classic assembly staging, live readback, reverse generation, and stable-overlap under the existing `PluginAssembly` and `PluginType` families
- `references/examples/seed-code-workflow-activity-package/` now proves the explicit package boundary: custom workflow activities stop with a clear diagnostic instead of being silently downgraded or falsely treated as package-supported
- `references/examples/seed-service-endpoint-connector/` now proves a compact neutral integration-endpoint slice across source, tracked-source, package-inputs, live readback, and stable-overlap drift for:
  - `serviceendpoint`
  - `connector`
- `references/examples/dbm-baseline/` still provides a real project-specific plugin assembly reference
- `references/examples/dbm-sdk-registration/` proves a DBM-specific source-first SDK-message registration pattern from code
- broader arbitrary code-level SDK registration still remains outside the supported runtime parser scope and should stay explicit as unsupported shapes or separate boundaries; the supported parser now covers a bounded common-idiom lane with reducible members, locals, constants, `nameof`, simple interpolation, switch or ternary reductions, reducible helpers, direct collection builders, and simple `yield return` aggregators
