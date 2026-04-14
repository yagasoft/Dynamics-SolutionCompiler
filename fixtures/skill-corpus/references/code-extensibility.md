# Code Extensibility

Use this reference for Dataverse extensibility families where the durable truth spans plugin registration metadata, SDK message families, and source code.

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

Treat `serviceendpoint` and `connector` as adjacent extensibility or integration families, but do not inflate them into proof points without a real artifact.

- if they only appear in schema lists or type catalogs, mark them catalog-known
- if they appear in solution scope or readback, inventory them as their own artifacts
- if they are referenced only indirectly by code or config, keep them source-first
- in the current skill corpus, their proof is still thinner than steps and images, so do not over-apply sdk-message registration guidance to them

## Direct Dev Versus Durable Release

- use direct `Dev` mutation and readback when validating that a step, image, or binding resolves correctly
- use tracked source and PAC packaging for durable assemblies and solution-managed registration artifacts
- when step registration lives in source code, keep that source-first layer explicit instead of forcing it into raw solution XML reasoning

## Current Proof Points

- `references/examples/dbm-baseline/` proves a real plugin assembly in tracked solution source
- `references/examples/dbm-sdk-registration/` proves a DBM-specific source-first SDK-message registration pattern from code
- neutral seeds still focus more strongly on schema and UI; code-extensibility remains the family with the heaviest honest use of best-effort or source-first handling
