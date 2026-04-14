# App Shell And Extensibility

This reference covers the Dataverse component families that sit beside schema and forms: app modules, site maps, web resources, environment variables, and plugin assemblies or steps.

## App Module And Site Map

Treat the app module as the shell that binds navigation, role access, and subcomponents.

What to look for:
- `AppModule.xml` for the app shell identity and component list
- `AppModuleSiteMap.xml` or app-aware sitemap XML for navigation structure
- `AppModuleComponents` for owned subcomponents such as site maps
- `AppModuleRoleMaps` for role access
- `appsettings` for app-level configuration

How to reason about it:
- the app module is not the same thing as the site map
- the site map is the navigation contract that the app consumes
- app-aware sitemap XML can point at entities, dashboards, web resources, and custom pages
- if the app references a web resource or other custom artifact, the app shell and the asset should be analyzed together

## Web Resources

Treat web resources as packaged assets with metadata, not as standalone source files.

What to look for:
- the web resource file itself
- the companion `.data.xml` metadata file when unpacked
- solution root components that register the asset
- app module or sitemap references that consume the asset

How to reason about it:
- the asset payload and registration are separate concerns
- HTML, SVG, JavaScript, CSS, and image assets all follow the same general packaging pattern
- when comparing source to readback, distinguish the file content from the web resource record metadata

## Environment Variables

Treat environment variable definitions and values as a pair with different responsibilities.

What to look for:
- definition metadata for the reusable contract
- value records for environment-specific overrides
- source-controlled defaults versus environment-specific deployment values

How to reason about it:
- the definition is the portable schema-like artifact
- the value is the override bound to a specific environment
- drift in a value does not automatically mean the definition is wrong
- compare definitions and values separately so the analysis stays precise

## Plugin Assemblies And Steps

Use plugin artifacts for inventory and drift analysis, but keep code-bound expectations realistic.

Use [code-extensibility.md](code-extensibility.md) when the task depends on sdk-message families, source-first step registration, or service-endpoint and connector boundaries.

What to look for:
- plugin assemblies
- plugin types
- sdk message processing steps
- step images

How to reason about it:
- plugin assemblies and steps are part of the extensibility surface, but the runtime behavior lives in the code
- solution XML may show registration, but it cannot explain code logic on its own
- step images and filtering attributes matter for runtime behavior and should be inventoried
- if the readback path cannot materialize a family cleanly, mark it as `source-only` or `best-effort` instead of pretending it is fully normalized

## Authoring And Release Guidance

- Use direct Dataverse mutation in `Dev` when you need fast proof, readback, or synthesis validation.
- Use tracked solution source plus PAC packaging when the asset must survive release governance.
- Use both when the canonical model drives a shell or extensibility change and the durable artifact still needs to be emitted.

## Boundaries

- Do not treat raw `solution.xml` or `customizations.xml` as the primary authoring surface.
- Do not infer plugin code behavior from solution metadata alone.
- Do not treat environment variable values as the same artifact family as environment variable definitions.
- Keep DBM-specific examples out of global guidance unless the task is explicitly DBM-specific.
