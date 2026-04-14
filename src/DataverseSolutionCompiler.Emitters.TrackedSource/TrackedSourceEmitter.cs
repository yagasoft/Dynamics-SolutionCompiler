using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Emission;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Emitters.TrackedSource;

public sealed class TrackedSourceEmitter : ISolutionEmitter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public EmittedArtifacts Emit(CanonicalSolution model, EmitRequest request)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(request);

        var trackedSourceRoot = GetContainedPath(request.OutputRoot, "tracked-source");
        if (Directory.Exists(trackedSourceRoot))
        {
            Directory.Delete(trackedSourceRoot, recursive: true);
        }

        Directory.CreateDirectory(trackedSourceRoot);

        var emittedFiles = new List<EmittedArtifact>();

        WriteSolutionManifest(model, trackedSourceRoot, emittedFiles);
        WriteEntityFiles(model, trackedSourceRoot, emittedFiles);
        WriteGlobalOptionSets(model, trackedSourceRoot, emittedFiles);
        WriteVisualizationFiles(model, trackedSourceRoot, emittedFiles);
        WriteAppShellFiles(model, trackedSourceRoot, emittedFiles);
        WriteWebResources(model, trackedSourceRoot, emittedFiles);
        WriteEnvironmentVariables(model, trackedSourceRoot, emittedFiles);
        WriteImportMaps(model, trackedSourceRoot, emittedFiles);
        WriteIntegrationEndpointFamilies(model, trackedSourceRoot, emittedFiles);
        WritePluginRegistrationFamilies(model, trackedSourceRoot, emittedFiles);
        WriteProcessPolicyFamilies(model, trackedSourceRoot, emittedFiles);
        WriteSecurityFamilies(model, trackedSourceRoot, emittedFiles);
        WriteAiFamilies(model, trackedSourceRoot, emittedFiles);
        WriteEntityAnalyticsConfigurations(model, trackedSourceRoot, emittedFiles);
        WriteCanvasApps(model, trackedSourceRoot, emittedFiles);

        var inventory = emittedFiles.Select(file => file.RelativePath)
            .Append("tracked-source/manifest.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        WriteJson(
            trackedSourceRoot,
            "manifest.json",
            new
            {
                solution = new
                {
                    model.Identity.UniqueName,
                    model.Identity.DisplayName,
                    model.Identity.Version,
                    layeringIntent = model.Identity.LayeringIntent.ToString()
                },
                publisher = new
                {
                    model.Publisher.UniqueName,
                    model.Publisher.Prefix,
                    model.Publisher.CustomizationPrefix,
                    model.Publisher.DisplayName
                },
                files = inventory
            },
            emittedFiles,
            "Root manifest for tracked Dataverse compiler output.");

        return new EmittedArtifacts(
            true,
            request.OutputRoot,
            emittedFiles
                .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                .ToArray(),
            [
                new CompilerDiagnostic(
                    "tracked-source-emitter-materialized",
                    DiagnosticSeverity.Info,
                    "Tracked source emitter wrote a deterministic tracked-source tree for the strongest proven Dataverse families.",
                    trackedSourceRoot)
            ]);
    }

    private static void WriteSolutionManifest(CanonicalSolution model, string trackedSourceRoot, List<EmittedArtifact> emittedFiles) =>
        WriteJson(
            trackedSourceRoot,
            "solution/manifest.json",
            new
            {
                model.Identity.UniqueName,
                model.Identity.DisplayName,
                model.Identity.Version,
                layeringIntent = model.Identity.LayeringIntent.ToString(),
                publisher = new
                {
                    model.Publisher.UniqueName,
                    model.Publisher.Prefix,
                    model.Publisher.CustomizationPrefix,
                    model.Publisher.DisplayName
                }
            },
            emittedFiles,
            "Solution identity and publisher manifest.");

    private static void WriteEntityFiles(CanonicalSolution model, string trackedSourceRoot, List<EmittedArtifact> emittedFiles)
    {
        var localOptionSets = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.OptionSet && !GetBoolProperty(artifact, ArtifactPropertyKeys.IsGlobal))
            .ToDictionary(artifact => artifact.LogicalName, artifact => artifact, StringComparer.OrdinalIgnoreCase);

        foreach (var table in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.Table).OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var entityLogicalName = GetProperty(table, ArtifactPropertyKeys.EntityLogicalName) ?? table.LogicalName;
            var entityDirectory = $"entities/{SafeSegment(entityLogicalName)}";

            WriteJson(
                trackedSourceRoot,
                $"{entityDirectory}/entity.json",
                new
                {
                    logicalName = entityLogicalName,
                    table.DisplayName,
                    schemaName = GetProperty(table, ArtifactPropertyKeys.SchemaName),
                    description = GetProperty(table, ArtifactPropertyKeys.Description),
                    entitySetName = GetProperty(table, ArtifactPropertyKeys.EntitySetName),
                    ownershipTypeMask = GetProperty(table, ArtifactPropertyKeys.OwnershipTypeMask),
                    primaryIdAttribute = GetProperty(table, ArtifactPropertyKeys.PrimaryIdAttribute),
                    primaryNameAttribute = GetProperty(table, ArtifactPropertyKeys.PrimaryNameAttribute),
                    isCustomizable = GetBoolProperty(table, ArtifactPropertyKeys.IsCustomizable),
                    shellOnly = GetBoolProperty(table, ArtifactPropertyKeys.ShellOnly)
                },
                emittedFiles,
                $"Tracked table manifest for {entityLogicalName}.");

            var attributes = model.Artifacts
                .Where(artifact => artifact.Family == ComponentFamily.Column
                    && string.Equals(GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName), entityLogicalName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
                .Select(artifact =>
                {
                    localOptionSets.TryGetValue(artifact.LogicalName, out var optionSet);
                    return new
                    {
                        logicalName = artifact.LogicalName.Split('|').Last(),
                        artifact.DisplayName,
                        schemaName = GetProperty(artifact, ArtifactPropertyKeys.SchemaName),
                        description = GetProperty(artifact, ArtifactPropertyKeys.Description),
                        attributeType = GetProperty(artifact, ArtifactPropertyKeys.AttributeType),
                        isSecured = GetBoolProperty(artifact, ArtifactPropertyKeys.IsSecured),
                        isCustomField = GetBoolProperty(artifact, ArtifactPropertyKeys.IsCustomField),
                        isCustomizable = GetBoolProperty(artifact, ArtifactPropertyKeys.IsCustomizable),
                        isPrimaryKey = GetBoolProperty(artifact, ArtifactPropertyKeys.IsPrimaryKey),
                        isPrimaryName = GetBoolProperty(artifact, ArtifactPropertyKeys.IsPrimaryName),
                        isLogical = GetBoolProperty(artifact, ArtifactPropertyKeys.IsLogical),
                        optionSetName = GetProperty(artifact, ArtifactPropertyKeys.OptionSetName),
                        optionSetType = GetProperty(artifact, ArtifactPropertyKeys.OptionSetType),
                        isGlobal = GetBoolProperty(artifact, ArtifactPropertyKeys.IsGlobal),
                        optionSet = optionSet is null ? null : new
                        {
                            optionSet.DisplayName,
                            optionSetType = GetProperty(optionSet, ArtifactPropertyKeys.OptionSetType),
                            optionCount = GetIntProperty(optionSet, ArtifactPropertyKeys.OptionCount),
                            options = ParseJsonNode(GetProperty(optionSet, ArtifactPropertyKeys.OptionsJson)),
                            comparisonSignature = GetProperty(optionSet, ArtifactPropertyKeys.ComparisonSignature)
                        }
                    };
                })
                .ToArray();

            WriteJson(
                trackedSourceRoot,
                $"{entityDirectory}/attributes.json",
                attributes,
                emittedFiles,
                $"Tracked attribute inventory for {entityLogicalName}.");

            var relationships = model.Artifacts
                .Where(artifact => artifact.Family == ComponentFamily.Relationship
                    && string.Equals(GetProperty(artifact, ArtifactPropertyKeys.OwningEntityLogicalName), entityLogicalName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
                .Select(artifact => new
                {
                    artifact.LogicalName,
                    relationshipType = GetProperty(artifact, ArtifactPropertyKeys.RelationshipType),
                    referencedEntity = GetProperty(artifact, ArtifactPropertyKeys.ReferencedEntity),
                    referencingEntity = GetProperty(artifact, ArtifactPropertyKeys.ReferencingEntity),
                    referencingAttribute = GetProperty(artifact, ArtifactPropertyKeys.ReferencingAttribute),
                    description = GetProperty(artifact, ArtifactPropertyKeys.Description)
                })
                .ToArray();

            WriteJson(
                trackedSourceRoot,
                $"{entityDirectory}/relationships.json",
                relationships,
                emittedFiles,
                $"Tracked relationship inventory for {entityLogicalName}.");

            var keys = model.Artifacts
                .Where(artifact => artifact.Family == ComponentFamily.Key
                    && string.Equals(GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName), entityLogicalName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
                .Select(artifact => new
                {
                    logicalName = artifact.LogicalName.Split('|').Last(),
                    artifact.DisplayName,
                    schemaName = GetProperty(artifact, ArtifactPropertyKeys.SchemaName),
                    description = GetProperty(artifact, ArtifactPropertyKeys.Description),
                    keyAttributes = ParseJsonNode(GetProperty(artifact, ArtifactPropertyKeys.KeyAttributesJson)),
                    indexStatus = GetProperty(artifact, ArtifactPropertyKeys.IndexStatus)
                })
                .ToArray();
            if (keys.Length > 0)
            {
                WriteJson(
                    trackedSourceRoot,
                    $"{entityDirectory}/keys.json",
                    keys,
                    emittedFiles,
                    $"Tracked alternate-key inventory for {entityLogicalName}.");
            }

            var imageConfigurations = model.Artifacts
                .Where(artifact => artifact.Family == ComponentFamily.ImageConfiguration
                    && string.Equals(GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName), entityLogicalName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
                .Select(artifact => new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    scope = GetProperty(artifact, ArtifactPropertyKeys.ImageConfigurationScope),
                    primaryImageAttribute = GetProperty(artifact, ArtifactPropertyKeys.PrimaryImageAttribute),
                    imageAttributeLogicalName = GetProperty(artifact, ArtifactPropertyKeys.ImageAttributeLogicalName),
                    canStoreFullImage = GetBoolProperty(artifact, ArtifactPropertyKeys.CanStoreFullImage),
                    isPrimaryImage = GetBoolProperty(artifact, ArtifactPropertyKeys.IsPrimaryImage)
                })
                .ToArray();
            if (imageConfigurations.Length > 0)
            {
                WriteJson(
                    trackedSourceRoot,
                    $"{entityDirectory}/image-configurations.json",
                    imageConfigurations,
                    emittedFiles,
                    $"Tracked image-configuration inventory for {entityLogicalName}.");
            }

            foreach (var form in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.Form
                && string.Equals(GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName), entityLogicalName, StringComparison.OrdinalIgnoreCase)))
            {
                var slug = Slugify(form.DisplayName ?? form.LogicalName);
                WriteJson(
                    trackedSourceRoot,
                    $"{entityDirectory}/forms/{slug}.json",
                    BuildSummaryArtifactJson(form),
                    emittedFiles,
                    $"Tracked form summary for {form.DisplayName ?? form.LogicalName}.");
            }

            foreach (var view in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.View
                && string.Equals(GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName), entityLogicalName, StringComparison.OrdinalIgnoreCase)))
            {
                var slug = Slugify(view.DisplayName ?? view.LogicalName);
                WriteJson(
                    trackedSourceRoot,
                    $"{entityDirectory}/views/{slug}.json",
                    BuildSummaryArtifactJson(view),
                    emittedFiles,
                    $"Tracked view summary for {view.DisplayName ?? view.LogicalName}.");
            }
        }
    }

    private static void WriteGlobalOptionSets(CanonicalSolution model, string trackedSourceRoot, List<EmittedArtifact> emittedFiles)
    {
        foreach (var optionSet in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.OptionSet && GetBoolProperty(artifact, ArtifactPropertyKeys.IsGlobal)))
        {
            WriteJson(
                trackedSourceRoot,
                $"global-option-sets/{SafeSegment(optionSet.LogicalName)}.json",
                new
                {
                    optionSet.LogicalName,
                    optionSet.DisplayName,
                    optionSetType = GetProperty(optionSet, ArtifactPropertyKeys.OptionSetType),
                    isGlobal = true,
                    optionCount = GetIntProperty(optionSet, ArtifactPropertyKeys.OptionCount),
                    options = ParseJsonNode(GetProperty(optionSet, ArtifactPropertyKeys.OptionsJson)),
                    description = GetProperty(optionSet, ArtifactPropertyKeys.Description),
                    comparisonSignature = GetProperty(optionSet, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked global option set for {optionSet.LogicalName}.");
        }
    }

    private static void WriteVisualizationFiles(CanonicalSolution model, string trackedSourceRoot, List<EmittedArtifact> emittedFiles)
    {
        foreach (var visualization in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.Visualization))
        {
            var entity = GetProperty(visualization, ArtifactPropertyKeys.TargetEntity) ?? "visualization";
            var slug = Slugify(visualization.DisplayName ?? visualization.LogicalName);
            WriteJson(
                trackedSourceRoot,
                $"saved-query-visualizations/{SafeSegment(entity)}-{slug}.json",
                BuildSummaryArtifactJson(visualization),
                emittedFiles,
                $"Tracked chart summary for {visualization.DisplayName ?? visualization.LogicalName}.");
        }
    }

    private static void WriteAppShellFiles(CanonicalSolution model, string trackedSourceRoot, List<EmittedArtifact> emittedFiles)
    {
        foreach (var artifact in model.Artifacts.Where(artifact =>
            artifact.Family is ComponentFamily.AppModule or ComponentFamily.AppSetting or ComponentFamily.SiteMap))
        {
            var relativePath = artifact.Family switch
            {
                ComponentFamily.AppModule => $"app-modules/{SafeSegment(artifact.LogicalName)}.json",
                ComponentFamily.AppSetting => $"app-settings/{SafeSegment(GetProperty(artifact, ArtifactPropertyKeys.ParentAppModuleUniqueName) ?? "app")}--{SafeSegment(GetProperty(artifact, ArtifactPropertyKeys.SettingDefinitionUniqueName) ?? artifact.LogicalName)}.json",
                ComponentFamily.SiteMap => $"site-maps/{SafeSegment(artifact.LogicalName)}.json",
                _ => throw new InvalidOperationException("Unexpected app-shell artifact family.")
            };

            WriteJson(
                trackedSourceRoot,
                relativePath,
                BuildSummaryArtifactJson(artifact),
                emittedFiles,
                $"Tracked {artifact.Family} materialization for {artifact.LogicalName}.");
        }
    }

    private static void WriteWebResources(CanonicalSolution model, string trackedSourceRoot, List<EmittedArtifact> emittedFiles)
    {
        foreach (var webResource in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.WebResource))
        {
            var slug = Slugify(webResource.LogicalName);
            var extension = Path.GetExtension(GetProperty(webResource, ArtifactPropertyKeys.AssetSourcePath) ?? webResource.LogicalName);
            WriteJson(
                trackedSourceRoot,
                $"web-resources/{slug}.json",
                BuildSummaryArtifactJson(webResource),
                emittedFiles,
                $"Tracked web resource metadata for {webResource.LogicalName}.");

            var sourcePath = ResolveArtifactSourcePath(webResource, ArtifactPropertyKeys.AssetSourcePath);
            if (!string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath))
            {
                CopyBinary(
                    trackedSourceRoot,
                    sourcePath,
                    $"web-resources/{slug}{extension}",
                    emittedFiles,
                    $"Tracked web resource payload for {webResource.LogicalName}.");
            }
        }
    }

    private static void WriteEnvironmentVariables(CanonicalSolution model, string trackedSourceRoot, List<EmittedArtifact> emittedFiles)
    {
        foreach (var artifact in model.Artifacts.Where(artifact =>
            artifact.Family is ComponentFamily.EnvironmentVariableDefinition or ComponentFamily.EnvironmentVariableValue))
        {
            var suffix = artifact.Family == ComponentFamily.EnvironmentVariableDefinition ? "definition" : "value";
            WriteJson(
                trackedSourceRoot,
                $"environment-variables/{SafeSegment(artifact.LogicalName)}.{suffix}.json",
                BuildSummaryArtifactJson(artifact),
                emittedFiles,
                $"Tracked {artifact.Family} materialization for {artifact.LogicalName}.");
        }
    }

    private static void WriteImportMaps(CanonicalSolution model, string trackedSourceRoot, List<EmittedArtifact> emittedFiles)
    {
        foreach (var importMap in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.ImportMap))
        {
            WriteJson(
                trackedSourceRoot,
                $"import-maps/{SafeSegment(importMap.LogicalName)}.json",
                new
                {
                    importMap.LogicalName,
                    importMap.DisplayName,
                    importSource = GetProperty(importMap, ArtifactPropertyKeys.ImportSource),
                    sourceFormat = GetProperty(importMap, ArtifactPropertyKeys.SourceFormat),
                    targetEntity = GetProperty(importMap, ArtifactPropertyKeys.ImportTargetEntity),
                    fieldDelimiter = GetProperty(importMap, ArtifactPropertyKeys.FieldDelimiter),
                    description = GetProperty(importMap, ArtifactPropertyKeys.Description),
                    mappingCount = GetIntProperty(importMap, ArtifactPropertyKeys.MappingCount),
                    comparisonSignature = GetProperty(importMap, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked import-map summary for {importMap.LogicalName}.");
        }

        foreach (var mapping in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.DataSourceMapping))
        {
            WriteJson(
                trackedSourceRoot,
                $"data-source-mappings/{SafeSegment(mapping.LogicalName)}.json",
                BuildSummaryArtifactJson(mapping),
                emittedFiles,
                $"Tracked data-source mapping summary for {mapping.LogicalName}.");
        }
    }

    private static void WriteIntegrationEndpointFamilies(CanonicalSolution model, string trackedSourceRoot, List<EmittedArtifact> emittedFiles)
    {
        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.ServiceEndpoint))
        {
            WriteJson(
                trackedSourceRoot,
                $"service-endpoints/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    name = GetProperty(artifact, ArtifactPropertyKeys.Name),
                    description = GetProperty(artifact, ArtifactPropertyKeys.Description),
                    contract = GetProperty(artifact, ArtifactPropertyKeys.Contract),
                    connectionMode = GetProperty(artifact, ArtifactPropertyKeys.ConnectionMode),
                    authType = GetProperty(artifact, ArtifactPropertyKeys.AuthType),
                    namespaceAddress = GetProperty(artifact, ArtifactPropertyKeys.NamespaceAddress),
                    endpointPath = GetProperty(artifact, ArtifactPropertyKeys.EndpointPath),
                    url = GetProperty(artifact, ArtifactPropertyKeys.Url),
                    messageFormat = GetProperty(artifact, ArtifactPropertyKeys.MessageFormat),
                    messageCharset = GetProperty(artifact, ArtifactPropertyKeys.MessageCharset),
                    introducedVersion = GetProperty(artifact, ArtifactPropertyKeys.IntroducedVersion),
                    isCustomizable = GetBoolProperty(artifact, ArtifactPropertyKeys.IsCustomizable),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked service endpoint for {artifact.LogicalName}.");
        }

        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.Connector))
        {
            WriteJson(
                trackedSourceRoot,
                $"connectors/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    name = GetProperty(artifact, ArtifactPropertyKeys.Name),
                    description = GetProperty(artifact, ArtifactPropertyKeys.Description),
                    connectorInternalId = GetProperty(artifact, ArtifactPropertyKeys.ConnectorInternalId),
                    connectorType = GetProperty(artifact, ArtifactPropertyKeys.ConnectorType),
                    capabilities = ParseJsonNode(GetProperty(artifact, ArtifactPropertyKeys.CapabilitiesJson)),
                    introducedVersion = GetProperty(artifact, ArtifactPropertyKeys.IntroducedVersion),
                    isCustomizable = GetBoolProperty(artifact, ArtifactPropertyKeys.IsCustomizable),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked connector for {artifact.LogicalName}.");
        }
    }

    private static void WritePluginRegistrationFamilies(CanonicalSolution model, string trackedSourceRoot, List<EmittedArtifact> emittedFiles)
    {
        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.PluginAssembly))
        {
            WriteJson(
                trackedSourceRoot,
                $"plugin-assemblies/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    assemblyFileName = GetProperty(artifact, ArtifactPropertyKeys.AssemblyFileName),
                    isolationMode = GetProperty(artifact, ArtifactPropertyKeys.IsolationMode),
                    sourceType = GetProperty(artifact, ArtifactPropertyKeys.SourceType),
                    introducedVersion = GetProperty(artifact, ArtifactPropertyKeys.IntroducedVersion),
                    byteLength = GetIntProperty(artifact, ArtifactPropertyKeys.ByteLength),
                    contentHash = GetProperty(artifact, ArtifactPropertyKeys.ContentHash),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked plugin assembly for {artifact.LogicalName}.");
        }

        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.PluginType))
        {
            WriteJson(
                trackedSourceRoot,
                $"plugin-types/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    assemblyFullName = GetProperty(artifact, ArtifactPropertyKeys.AssemblyFullName),
                    assemblyQualifiedName = GetProperty(artifact, ArtifactPropertyKeys.AssemblyQualifiedName),
                    friendlyName = GetProperty(artifact, ArtifactPropertyKeys.FriendlyName),
                    workflowActivityGroupName = GetProperty(artifact, ArtifactPropertyKeys.WorkflowActivityGroupName),
                    description = GetProperty(artifact, ArtifactPropertyKeys.Description),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked plugin type for {artifact.LogicalName}.");
        }

        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.PluginStep))
        {
            WriteJson(
                trackedSourceRoot,
                $"plugin-steps/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    stage = GetProperty(artifact, ArtifactPropertyKeys.Stage),
                    mode = GetProperty(artifact, ArtifactPropertyKeys.Mode),
                    rank = GetProperty(artifact, ArtifactPropertyKeys.Rank),
                    supportedDeployment = GetProperty(artifact, ArtifactPropertyKeys.SupportedDeployment),
                    messageName = GetProperty(artifact, ArtifactPropertyKeys.MessageName),
                    primaryEntity = GetProperty(artifact, ArtifactPropertyKeys.PrimaryEntity),
                    handlerPluginTypeName = GetProperty(artifact, ArtifactPropertyKeys.HandlerPluginTypeName),
                    filteringAttributes = GetProperty(artifact, ArtifactPropertyKeys.FilteringAttributes),
                    description = GetProperty(artifact, ArtifactPropertyKeys.Description),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked plugin step for {artifact.LogicalName}.");
        }

        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.PluginStepImage))
        {
            WriteJson(
                trackedSourceRoot,
                $"plugin-step-images/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    parentPluginStepLogicalName = GetProperty(artifact, ArtifactPropertyKeys.ParentPluginStepLogicalName),
                    entityAlias = GetProperty(artifact, ArtifactPropertyKeys.EntityAlias),
                    imageType = GetProperty(artifact, ArtifactPropertyKeys.ImageType),
                    messagePropertyName = GetProperty(artifact, ArtifactPropertyKeys.MessagePropertyName),
                    selectedAttributes = GetProperty(artifact, ArtifactPropertyKeys.SelectedAttributes),
                    description = GetProperty(artifact, ArtifactPropertyKeys.Description),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked plugin step image for {artifact.LogicalName}.");
        }
    }

    private static void WriteProcessPolicyFamilies(CanonicalSolution model, string trackedSourceRoot, List<EmittedArtifact> emittedFiles)
    {
        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.DuplicateRule))
        {
            WriteJson(
                trackedSourceRoot,
                $"duplicate-rules/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    description = GetProperty(artifact, ArtifactPropertyKeys.Description),
                    baseEntityName = GetProperty(artifact, ArtifactPropertyKeys.BaseEntityName),
                    matchingEntityName = GetProperty(artifact, ArtifactPropertyKeys.MatchingEntityName),
                    isCaseSensitive = GetBoolProperty(artifact, ArtifactPropertyKeys.IsCaseSensitive),
                    excludeInactiveRecords = GetBoolProperty(artifact, ArtifactPropertyKeys.ExcludeInactiveRecords),
                    conditionCount = GetIntProperty(artifact, ArtifactPropertyKeys.ItemCount),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked duplicate rule for {artifact.LogicalName}.");
        }

        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.DuplicateRuleCondition))
        {
            WriteJson(
                trackedSourceRoot,
                $"duplicate-rule-conditions/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    parentDuplicateRuleLogicalName = GetProperty(artifact, ArtifactPropertyKeys.ParentDuplicateRuleLogicalName),
                    baseAttributeName = GetProperty(artifact, ArtifactPropertyKeys.BaseAttributeName),
                    matchingAttributeName = GetProperty(artifact, ArtifactPropertyKeys.MatchingAttributeName),
                    operatorCode = GetProperty(artifact, ArtifactPropertyKeys.OperatorCode),
                    ignoreBlankValues = GetBoolProperty(artifact, ArtifactPropertyKeys.IgnoreBlankValues),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked duplicate-rule condition for {artifact.LogicalName}.");
        }

        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.RoutingRule))
        {
            WriteJson(
                trackedSourceRoot,
                $"routing-rules/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    description = GetProperty(artifact, ArtifactPropertyKeys.Description),
                    workflowId = GetProperty(artifact, ArtifactPropertyKeys.WorkflowId),
                    itemCount = GetIntProperty(artifact, ArtifactPropertyKeys.ItemCount),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked routing rule for {artifact.LogicalName}.");
        }

        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.RoutingRuleItem))
        {
            WriteJson(
                trackedSourceRoot,
                $"routing-rule-items/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    parentRoutingRuleLogicalName = GetProperty(artifact, ArtifactPropertyKeys.ParentRoutingRuleLogicalName),
                    description = GetProperty(artifact, ArtifactPropertyKeys.Description),
                    conditionXml = GetProperty(artifact, ArtifactPropertyKeys.ConditionXml),
                    workflowId = GetProperty(artifact, ArtifactPropertyKeys.WorkflowId),
                    routedQueueId = GetProperty(artifact, ArtifactPropertyKeys.RoutedQueueId),
                    assignObjectId = GetProperty(artifact, ArtifactPropertyKeys.AssignObjectId),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked routing-rule item for {artifact.LogicalName}.");
        }

        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.MobileOfflineProfile))
        {
            WriteJson(
                trackedSourceRoot,
                $"mobile-offline-profiles/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    description = GetProperty(artifact, ArtifactPropertyKeys.Description),
                    isValidated = GetBoolProperty(artifact, ArtifactPropertyKeys.IsValidated),
                    itemCount = GetIntProperty(artifact, ArtifactPropertyKeys.ItemCount),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked mobile offline profile for {artifact.LogicalName}.");
        }

        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.MobileOfflineProfileItem))
        {
            WriteJson(
                trackedSourceRoot,
                $"mobile-offline-profile-items/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    parentMobileOfflineProfileLogicalName = GetProperty(artifact, ArtifactPropertyKeys.ParentMobileOfflineProfileLogicalName),
                    entityLogicalName = GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName),
                    recordDistributionCriteria = GetProperty(artifact, ArtifactPropertyKeys.RecordDistributionCriteria),
                    recordsOwnedByMe = GetBoolProperty(artifact, ArtifactPropertyKeys.RecordsOwnedByMe),
                    recordsOwnedByMyTeam = GetBoolProperty(artifact, ArtifactPropertyKeys.RecordsOwnedByMyTeam),
                    recordsOwnedByMyBusinessUnit = GetBoolProperty(artifact, ArtifactPropertyKeys.RecordsOwnedByMyBusinessUnit),
                    profileItemEntityFilter = GetProperty(artifact, ArtifactPropertyKeys.ProfileItemEntityFilter),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked mobile offline profile item for {artifact.LogicalName}.");
        }

        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.SimilarityRule))
        {
            WriteJson(
                trackedSourceRoot,
                $"similarity-rules/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    description = GetProperty(artifact, ArtifactPropertyKeys.Description),
                    baseEntityName = GetProperty(artifact, ArtifactPropertyKeys.BaseEntityName),
                    matchingEntityName = GetProperty(artifact, ArtifactPropertyKeys.MatchingEntityName),
                    excludeInactiveRecords = GetBoolProperty(artifact, ArtifactPropertyKeys.ExcludeInactiveRecords),
                    maxKeywords = GetProperty(artifact, ArtifactPropertyKeys.MaxKeywords),
                    ngramSize = GetProperty(artifact, ArtifactPropertyKeys.NgramSize),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature),
                    evidence = artifact.Evidence.ToString()
                },
                emittedFiles,
                $"Tracked similarity rule for {artifact.LogicalName}.");
        }

        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.Sla))
        {
            WriteJson(
                trackedSourceRoot,
                $"slas/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    applicableFrom = GetProperty(artifact, ArtifactPropertyKeys.ApplicableFrom),
                    allowPauseResume = GetBoolProperty(artifact, ArtifactPropertyKeys.AllowPauseResume),
                    isDefault = GetBoolProperty(artifact, ArtifactPropertyKeys.IsDefault),
                    workflowId = GetProperty(artifact, ArtifactPropertyKeys.WorkflowId),
                    itemCount = GetIntProperty(artifact, ArtifactPropertyKeys.ItemCount),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature),
                    evidence = artifact.Evidence.ToString()
                },
                emittedFiles,
                $"Tracked SLA for {artifact.LogicalName}.");
        }

        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.SlaItem))
        {
            WriteJson(
                trackedSourceRoot,
                $"sla-items/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    parentSlaLogicalName = GetProperty(artifact, ArtifactPropertyKeys.ParentSlaLogicalName),
                    applicableEntity = GetProperty(artifact, ArtifactPropertyKeys.ApplicableEntity),
                    allowPauseResume = GetBoolProperty(artifact, ArtifactPropertyKeys.AllowPauseResume),
                    applicableWhenXml = GetProperty(artifact, ArtifactPropertyKeys.ApplicableWhenXml),
                    actionUrl = GetProperty(artifact, ArtifactPropertyKeys.ActionUrl),
                    actionFlowUniqueName = GetProperty(artifact, ArtifactPropertyKeys.ActionFlowUniqueName),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature),
                    evidence = artifact.Evidence.ToString()
                },
                emittedFiles,
                $"Tracked SLA item for {artifact.LogicalName}.");
        }
    }

    private static void WriteSecurityFamilies(CanonicalSolution model, string trackedSourceRoot, List<EmittedArtifact> emittedFiles)
    {
        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.Role))
        {
            WriteJson(
                trackedSourceRoot,
                $"roles/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    isCustomizable = GetBoolProperty(artifact, ArtifactPropertyKeys.IsCustomizable),
                    privilegeCount = GetIntProperty(artifact, ArtifactPropertyKeys.PrivilegeCount),
                    privilegeSummary = ParseJsonNode(GetProperty(artifact, ArtifactPropertyKeys.CapabilitiesJson)),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked role definition for {artifact.LogicalName}.");
        }

        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.RolePrivilege))
        {
            WriteJson(
                trackedSourceRoot,
                $"role-privileges/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    parentRoleLogicalName = GetProperty(artifact, ArtifactPropertyKeys.ParentRoleLogicalName),
                    privilegeName = GetProperty(artifact, ArtifactPropertyKeys.PrivilegeName),
                    accessLevel = GetProperty(artifact, ArtifactPropertyKeys.AccessLevel),
                    objectTypeCode = GetProperty(artifact, ArtifactPropertyKeys.ObjectTypeCode),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature),
                    evidence = artifact.Evidence.ToString()
                },
                emittedFiles,
                $"Tracked role privilege for {artifact.LogicalName}.");
        }

        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.FieldSecurityProfile))
        {
            WriteJson(
                trackedSourceRoot,
                $"field-security-profiles/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    description = GetProperty(artifact, ArtifactPropertyKeys.Description),
                    permissionCount = GetIntProperty(artifact, ArtifactPropertyKeys.ItemCount),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked field security profile for {artifact.LogicalName}.");
        }

        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.FieldPermission))
        {
            WriteJson(
                trackedSourceRoot,
                $"field-permissions/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    parentFieldSecurityProfileLogicalName = GetProperty(artifact, ArtifactPropertyKeys.ParentFieldSecurityProfileLogicalName),
                    entityLogicalName = GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName),
                    attributeLogicalName = GetProperty(artifact, ArtifactPropertyKeys.AttributeLogicalName),
                    canRead = GetProperty(artifact, ArtifactPropertyKeys.CanRead),
                    canCreate = GetProperty(artifact, ArtifactPropertyKeys.CanCreate),
                    canUpdate = GetProperty(artifact, ArtifactPropertyKeys.CanUpdate),
                    canReadUnmasked = GetProperty(artifact, ArtifactPropertyKeys.CanReadUnmasked),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked field permission for {artifact.LogicalName}.");
        }

        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.ConnectionRole))
        {
            WriteJson(
                trackedSourceRoot,
                $"connection-roles/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    category = GetProperty(artifact, ArtifactPropertyKeys.Category),
                    description = GetProperty(artifact, ArtifactPropertyKeys.Description),
                    isCustomizable = GetBoolProperty(artifact, ArtifactPropertyKeys.IsCustomizable),
                    introducedVersion = GetProperty(artifact, ArtifactPropertyKeys.IntroducedVersion),
                    objectTypeMappings = ParseJsonNode(GetProperty(artifact, ArtifactPropertyKeys.ObjectTypeMappingsJson)),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked connection role for {artifact.LogicalName}.");
        }
    }

    private static void WriteAiFamilies(CanonicalSolution model, string trackedSourceRoot, List<EmittedArtifact> emittedFiles)
    {
        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.AiProjectType))
        {
            WriteJson(
                trackedSourceRoot,
                $"ai-project-types/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    description = GetProperty(artifact, ArtifactPropertyKeys.Description),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked AI project type for {artifact.LogicalName}.");
        }

        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.AiProject))
        {
            WriteJson(
                trackedSourceRoot,
                $"ai-projects/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    description = GetProperty(artifact, ArtifactPropertyKeys.Description),
                    parentAiProjectTypeLogicalName = GetProperty(artifact, ArtifactPropertyKeys.ParentAiProjectTypeLogicalName),
                    targetEntity = GetProperty(artifact, ArtifactPropertyKeys.TargetEntity),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked AI project for {artifact.LogicalName}.");
        }

        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.AiConfiguration))
        {
            WriteJson(
                trackedSourceRoot,
                $"ai-configurations/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    parentAiProjectLogicalName = GetProperty(artifact, ArtifactPropertyKeys.ParentAiProjectLogicalName),
                    configurationKind = GetProperty(artifact, ArtifactPropertyKeys.ConfigurationKind),
                    value = GetProperty(artifact, ArtifactPropertyKeys.Value),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked AI configuration for {artifact.LogicalName}.");
        }
    }

    private static void WriteEntityAnalyticsConfigurations(CanonicalSolution model, string trackedSourceRoot, List<EmittedArtifact> emittedFiles)
    {
        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.EntityAnalyticsConfiguration))
        {
            WriteJson(
                trackedSourceRoot,
                $"entity-analytics-configurations/{SafeSegment(artifact.LogicalName)}.json",
                new
                {
                    artifact.LogicalName,
                    artifact.DisplayName,
                    parentEntityLogicalName = GetProperty(artifact, ArtifactPropertyKeys.ParentEntityLogicalName),
                    entityDataSource = GetProperty(artifact, ArtifactPropertyKeys.EntityDataSource),
                    isEnabledForAdls = GetProperty(artifact, ArtifactPropertyKeys.IsEnabledForAdls),
                    isEnabledForTimeSeries = GetProperty(artifact, ArtifactPropertyKeys.IsEnabledForTimeSeries),
                    comparisonSignature = GetProperty(artifact, ArtifactPropertyKeys.ComparisonSignature)
                },
                emittedFiles,
                $"Tracked entity analytics configuration for {artifact.LogicalName}.");
        }
    }

    private static void WriteCanvasApps(CanonicalSolution model, string trackedSourceRoot, List<EmittedArtifact> emittedFiles)
    {
        foreach (var canvasApp in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.CanvasApp))
        {
            var slug = SafeSegment(canvasApp.LogicalName);
            WriteJson(
                trackedSourceRoot,
                $"canvas-apps/{slug}.json",
                BuildSummaryArtifactJson(canvasApp),
                emittedFiles,
                $"Tracked canvas app metadata for {canvasApp.LogicalName}.");

            CopyOptionalArtifactAsset(canvasApp, ArtifactPropertyKeys.DocumentSourcePath, $"canvas-apps/{Path.GetFileName(GetProperty(canvasApp, ArtifactPropertyKeys.DocumentSourcePath) ?? string.Empty)}", trackedSourceRoot, emittedFiles);
            CopyOptionalArtifactAsset(canvasApp, ArtifactPropertyKeys.BackgroundSourcePath, $"canvas-apps/{Path.GetFileName(GetProperty(canvasApp, ArtifactPropertyKeys.BackgroundSourcePath) ?? string.Empty)}", trackedSourceRoot, emittedFiles);
        }
    }

    private static void CopyOptionalArtifactAsset(FamilyArtifact artifact, string sourcePropertyKey, string relativePath, string trackedSourceRoot, List<EmittedArtifact> emittedFiles)
    {
        var sourcePath = ResolveArtifactSourcePath(artifact, sourcePropertyKey);
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath) || string.IsNullOrWhiteSpace(relativePath) || relativePath.EndsWith("/", StringComparison.Ordinal))
        {
            return;
        }

        CopyBinary(trackedSourceRoot, sourcePath, relativePath, emittedFiles, $"Tracked package evidence payload for {artifact.LogicalName}.");
    }

    private static object BuildSummaryArtifactJson(FamilyArtifact artifact) => new
    {
        artifact.Family,
        artifact.LogicalName,
        artifact.DisplayName,
        metadataSourcePath = GetProperty(artifact, ArtifactPropertyKeys.MetadataSourcePath),
        properties = artifact.Properties?
            .Where(pair => !pair.Key.EndsWith("SourcePath", StringComparison.Ordinal))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => ConvertPropertyValue(pair.Key, pair.Value), StringComparer.Ordinal)
    };

    private static object? ConvertPropertyValue(string key, string value) =>
        key.EndsWith("Json", StringComparison.Ordinal)
            ? ParseJsonNode(value)
            : value;

    private static JsonNode? ParseJsonNode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : JsonNode.Parse(value);

    private static string? GetProperty(FamilyArtifact artifact, string key) =>
        artifact.Properties is not null && artifact.Properties.TryGetValue(key, out var value) ? value : null;

    private static bool GetBoolProperty(FamilyArtifact artifact, string key) =>
        string.Equals(GetProperty(artifact, key), "true", StringComparison.OrdinalIgnoreCase);

    private static int? GetIntProperty(FamilyArtifact artifact, string key) =>
        int.TryParse(GetProperty(artifact, key), out var value) ? value : null;

    private static string ResolveArtifactSourcePath(FamilyArtifact artifact, string propertyKey)
    {
        var relativePath = GetProperty(artifact, propertyKey);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(relativePath))
        {
            return Path.GetFullPath(relativePath);
        }

        var metadataRelativePath = GetProperty(artifact, ArtifactPropertyKeys.MetadataSourcePath);
        if (string.IsNullOrWhiteSpace(metadataRelativePath) || string.IsNullOrWhiteSpace(artifact.SourcePath))
        {
            return Path.GetFullPath(relativePath);
        }

        var sourceRoot = artifact.SourcePath!;
        foreach (var _ in metadataRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            sourceRoot = Path.GetDirectoryName(sourceRoot)!;
        }

        return Path.GetFullPath(Path.Combine(sourceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string SafeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Tracked-source path segment cannot be empty.");
        }

        if (value.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Refusing to materialize a tracked-source path from traversal-like input: {value}");
        }

        var normalized = value.Trim().Replace('\\', '-').Replace('/', '-');
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            normalized = normalized.Replace(invalid, '-');
        }
        return normalized;
    }

    private static string Slugify(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '-');
        }

        var slug = builder.ToString();
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }

    private static void WriteJson(string trackedSourceRoot, string relativePath, object document, List<EmittedArtifact> emittedFiles, string description)
    {
        var fullPath = GetContainedPath(trackedSourceRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var json = JsonSerializer.Serialize(document, JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal);
        File.WriteAllText(fullPath, json + "\n", Utf8NoBom);
        emittedFiles.Add(new EmittedArtifact($"tracked-source/{relativePath.Replace('\\', '/')}", EmittedArtifactRole.TrackedSource, description));
    }

    private static void CopyBinary(string trackedSourceRoot, string sourcePath, string relativePath, List<EmittedArtifact> emittedFiles, string description)
    {
        var fullPath = GetContainedPath(trackedSourceRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.Copy(sourcePath, fullPath, overwrite: true);
        emittedFiles.Add(new EmittedArtifact($"tracked-source/{relativePath.Replace('\\', '/')}", EmittedArtifactRole.TrackedSource, description));
    }

    private static string GetContainedPath(string root, string relativePath)
    {
        var rootFullPath = Path.GetFullPath(root);
        var candidatePath = Path.GetFullPath(Path.Combine(rootFullPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = rootFullPath.EndsWith(Path.DirectorySeparatorChar) ? rootFullPath : rootFullPath + Path.DirectorySeparatorChar;
        if (!candidatePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !candidatePath.Equals(rootFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Refusing to write outside the tracked-source root: {relativePath}");
        }

        return candidatePath;
    }
}
