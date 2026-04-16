using System.Globalization;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Readers.Live;

internal sealed partial class DataverseWebApiLiveReader
{
    private async Task ReadCodeExtensibilityFamiliesAsync(
        SolutionComponentScope scope,
        IReadOnlySet<ComponentFamily> requestedFamilies,
        ICollection<FamilyArtifact> artifacts,
        ICollection<CompilerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var assemblyFullNamesById = new Dictionary<Guid, string>();
        if (ShouldReadAny(requestedFamilies, ComponentFamily.PluginAssembly, ComponentFamily.PluginType)
            && scope.PluginAssemblyIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"pluginassemblies?$select=pluginassemblyid,name,path,isolationmode,sourcetype,introducedversion,version,culture,publickeytoken&$filter={BuildGuidFilter("pluginassemblyid", scope.PluginAssemblyIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<System.Text.Json.Nodes.JsonObject>())
                {
                    var assemblyId = GetGuid(row, "pluginassemblyid");
                    var fullName = BuildPluginAssemblyFullName(row);
                    if (assemblyId.HasValue && !string.IsNullOrWhiteSpace(fullName))
                    {
                        assemblyFullNamesById[assemblyId.Value] = fullName!;
                    }

                    if (requestedFamilies.Contains(ComponentFamily.PluginAssembly))
                    {
                        var artifact = CreatePluginAssemblyArtifact(row, fullName);
                        if (artifact is not null)
                        {
                            artifacts.Add(artifact);
                        }
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.PluginAssembly, "pluginassemblies", exception));
            }
        }

        var pluginTypeNamesById = new Dictionary<Guid, string>();
        if (ShouldReadAny(requestedFamilies, ComponentFamily.PluginType, ComponentFamily.PluginStep, ComponentFamily.PluginStepImage)
            && scope.PluginTypeIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"plugintypes?$select=plugintypeid,typename,name,friendlyname,workflowactivitygroupname,description,assemblyname,_pluginassemblyid_value&$filter={BuildGuidFilter("plugintypeid", scope.PluginTypeIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<System.Text.Json.Nodes.JsonObject>())
                {
                    var pluginTypeId = GetGuid(row, "plugintypeid");
                    var typeName = GetString(row, "typename", "name");
                    if (pluginTypeId.HasValue && !string.IsNullOrWhiteSpace(typeName))
                    {
                        pluginTypeNamesById[pluginTypeId.Value] = typeName!;
                    }

                    if (requestedFamilies.Contains(ComponentFamily.PluginType))
                    {
                        var artifact = CreatePluginTypeArtifact(row, ResolveAssemblyFullName(row, assemblyFullNamesById));
                        if (artifact is not null)
                        {
                            artifacts.Add(artifact);
                        }
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.PluginType, "plugintypes", exception));
            }
        }

        var pluginStepsById = new Dictionary<Guid, PluginStepContext>();
        if (ShouldReadAny(requestedFamilies, ComponentFamily.PluginStep, ComponentFamily.PluginStepImage)
            && scope.PluginStepIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"sdkmessageprocessingsteps?$select=sdkmessageprocessingstepid,name,description,stage,mode,rank,supporteddeployment,filteringattributes,_eventhandler_value,_sdkmessageid_value,_sdkmessagefilterid_value&$filter={BuildGuidFilter("sdkmessageprocessingstepid", scope.PluginStepIds)}",
                    cancellationToken).ConfigureAwait(false);

                var stepRows = rows.OfType<System.Text.Json.Nodes.JsonObject>().ToArray();
                var messageNamesById = await ResolveSdkMessageNamesAsync(
                    stepRows.Select(row => GetGuid(row, "_sdkmessageid_value")).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray(),
                    cancellationToken).ConfigureAwait(false);
                var primaryEntitiesByFilterId = await ResolveSdkMessageFilterEntitiesAsync(
                    stepRows.Select(row => GetGuid(row, "_sdkmessagefilterid_value")).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray(),
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in stepRows)
                {
                    var context = CreatePluginStepContext(row, pluginTypeNamesById, messageNamesById, primaryEntitiesByFilterId);
                    if (context is null)
                    {
                        continue;
                    }

                    pluginStepsById[context.Id] = context;
                    if (requestedFamilies.Contains(ComponentFamily.PluginStep))
                    {
                        artifacts.Add(CreatePluginStepArtifact(context));
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.PluginStep, "sdkmessageprocessingsteps", exception));
            }
        }

        if (requestedFamilies.Contains(ComponentFamily.PluginStepImage) && scope.PluginStepImageIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"sdkmessageprocessingstepimages?$select=sdkmessageprocessingstepimageid,name,description,entityalias,imagetype,messagepropertyname,attributes,_sdkmessageprocessingstepid_value&$filter={BuildGuidFilter("sdkmessageprocessingstepimageid", scope.PluginStepImageIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<System.Text.Json.Nodes.JsonObject>())
                {
                    var artifact = CreatePluginStepImageArtifact(row, pluginStepsById);
                    if (artifact is not null)
                    {
                        artifacts.Add(artifact);
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.PluginStepImage, "sdkmessageprocessingstepimages", exception));
            }
        }

        if (requestedFamilies.Contains(ComponentFamily.ServiceEndpoint) && scope.ServiceEndpointIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"serviceendpoints?$select=serviceendpointid,logical_name,name,description,contract,connectionmode,authtype,namespaceaddress,path,url,messageformat,messagecharset,introducedversion,iscustomizable&$filter={BuildGuidFilter("serviceendpointid", scope.ServiceEndpointIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<System.Text.Json.Nodes.JsonObject>())
                {
                    var artifact = CreateServiceEndpointArtifact(row);
                    if (artifact is not null)
                    {
                        artifacts.Add(artifact);
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.ServiceEndpoint, "serviceendpoints", exception));
            }
        }

        if (requestedFamilies.Contains(ComponentFamily.Connector) && scope.ConnectorIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"connectors?$select=connectorid,logical_name,name,displayname,description,connectorinternalid,connectortype,capabilities,introducedversion,iscustomizable&$filter={BuildGuidFilter("connectorid", scope.ConnectorIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<System.Text.Json.Nodes.JsonObject>())
                {
                    var artifact = CreateConnectorArtifact(row);
                    if (artifact is not null)
                    {
                        artifacts.Add(artifact);
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.Connector, "connectors", exception));
            }
        }
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveSdkMessageNamesAsync(
        IReadOnlyCollection<Guid> sdkMessageIds,
        CancellationToken cancellationToken)
    {
        if (sdkMessageIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var rows = await GetCollectionAsync(
            $"sdkmessages?$select=sdkmessageid,name&$filter={BuildGuidFilter("sdkmessageid", sdkMessageIds)}",
            cancellationToken).ConfigureAwait(false);

        return rows
            .OfType<System.Text.Json.Nodes.JsonObject>()
            .Where(row => GetGuid(row, "sdkmessageid").HasValue && !string.IsNullOrWhiteSpace(GetString(row, "name")))
            .ToDictionary(row => GetGuid(row, "sdkmessageid")!.Value, row => GetString(row, "name")!, EqualityComparer<Guid>.Default);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveSdkMessageFilterEntitiesAsync(
        IReadOnlyCollection<Guid> sdkMessageFilterIds,
        CancellationToken cancellationToken)
    {
        if (sdkMessageFilterIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var rows = await GetCollectionAsync(
            $"sdkmessagefilters?$select=sdkmessagefilterid,primaryobjecttypecode&$filter={BuildGuidFilter("sdkmessagefilterid", sdkMessageFilterIds)}",
            cancellationToken).ConfigureAwait(false);

        return rows
            .OfType<System.Text.Json.Nodes.JsonObject>()
            .Where(row => GetGuid(row, "sdkmessagefilterid").HasValue && !string.IsNullOrWhiteSpace(GetString(row, "primaryobjecttypecode")))
            .ToDictionary(
                row => GetGuid(row, "sdkmessagefilterid")!.Value,
                row => NormalizeLogicalName(GetString(row, "primaryobjecttypecode")) ?? string.Empty,
                EqualityComparer<Guid>.Default);
    }

    private static FamilyArtifact? CreatePluginAssemblyArtifact(System.Text.Json.Nodes.JsonObject row, string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return null;
        }

        var summaryJson = SerializeJson(new
        {
            fullName,
            fileName = Path.GetFileName(GetString(row, "path")),
            isolationMode = GetString(row, "isolationmode"),
            sourceType = GetString(row, "sourcetype"),
            introducedVersion = GetString(row, "introducedversion")
        });

        return new FamilyArtifact(
            ComponentFamily.PluginAssembly,
            fullName,
            GetString(row, "name") ?? fullName,
            $"pluginassemblies/{fullName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.AssemblyFullName, fullName),
                (ArtifactPropertyKeys.AssemblyFileName, Path.GetFileName(GetString(row, "path"))),
                (ArtifactPropertyKeys.IsolationMode, GetString(row, "isolationmode")),
                (ArtifactPropertyKeys.SourceType, GetString(row, "sourcetype")),
                (ArtifactPropertyKeys.IntroducedVersion, GetString(row, "introducedversion")),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FamilyArtifact? CreatePluginTypeArtifact(System.Text.Json.Nodes.JsonObject row, string? assemblyFullName)
    {
        var typeName = GetString(row, "typename", "name");
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        var assemblyQualifiedName = string.IsNullOrWhiteSpace(assemblyFullName)
            ? null
            : $"{typeName}, {assemblyFullName}";
        var summaryJson = SerializeJson(new
        {
            typeName,
            assemblyFullName,
            assemblyQualifiedName
        });

        return new FamilyArtifact(
            ComponentFamily.PluginType,
            typeName,
            typeName,
            $"plugintypes/{typeName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.AssemblyFullName, assemblyFullName),
                (ArtifactPropertyKeys.AssemblyQualifiedName, assemblyQualifiedName),
                (ArtifactPropertyKeys.PluginTypeKind, string.IsNullOrWhiteSpace(GetString(row, "workflowactivitygroupname")) ? "plugin" : "customWorkflowActivity"),
                (ArtifactPropertyKeys.FriendlyName, GetString(row, "friendlyname")),
                (ArtifactPropertyKeys.WorkflowActivityGroupName, GetString(row, "workflowactivitygroupname")),
                (ArtifactPropertyKeys.Description, GetString(row, "description")),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static PluginStepContext? CreatePluginStepContext(
        System.Text.Json.Nodes.JsonObject row,
        IReadOnlyDictionary<Guid, string> pluginTypeNamesById,
        IReadOnlyDictionary<Guid, string> messageNamesById,
        IReadOnlyDictionary<Guid, string> primaryEntitiesByFilterId)
    {
        var stepId = GetGuid(row, "sdkmessageprocessingstepid");
        var stepName = GetString(row, "name");
        if (!stepId.HasValue || string.IsNullOrWhiteSpace(stepName))
        {
            return null;
        }

        var handlerPluginTypeName = string.Empty;
        var handlerId = GetGuid(row, "_eventhandler_value");
        if (handlerId.HasValue && pluginTypeNamesById.TryGetValue(handlerId.Value, out var mappedTypeName))
        {
            handlerPluginTypeName = mappedTypeName;
        }

        var messageName = string.Empty;
        var messageId = GetGuid(row, "_sdkmessageid_value");
        if (messageId.HasValue && messageNamesById.TryGetValue(messageId.Value, out var mappedMessageName))
        {
            messageName = mappedMessageName;
        }

        var primaryEntity = string.Empty;
        var filterId = GetGuid(row, "_sdkmessagefilterid_value");
        if (filterId.HasValue && primaryEntitiesByFilterId.TryGetValue(filterId.Value, out var mappedPrimaryEntity))
        {
            primaryEntity = mappedPrimaryEntity;
        }

        var stage = GetString(row, "stage");
        var mode = GetString(row, "mode");
        var logicalName = BuildPluginStepLogicalName(handlerPluginTypeName, messageName, primaryEntity, stage, mode, stepName);

        return new PluginStepContext(
            stepId.Value,
            logicalName,
            stepName,
            GetString(row, "description"),
            stage,
            mode,
            GetString(row, "rank"),
            GetString(row, "supporteddeployment"),
            messageName,
            primaryEntity,
            handlerPluginTypeName,
            NormalizeAttributeList(GetString(row, "filteringattributes")));
    }

    private static FamilyArtifact CreatePluginStepArtifact(PluginStepContext context)
    {
        var summaryJson = SerializeJson(new
        {
            context.DisplayName,
            context.Stage,
            context.Mode,
            context.Rank,
            context.SupportedDeployment,
            context.MessageName,
            context.PrimaryEntity,
            context.HandlerPluginTypeName,
            context.FilteringAttributes
        });

        return new FamilyArtifact(
            ComponentFamily.PluginStep,
            context.LogicalName,
            context.DisplayName,
            $"sdkmessageprocessingsteps/{context.LogicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.Description, context.Description),
                (ArtifactPropertyKeys.Stage, context.Stage),
                (ArtifactPropertyKeys.Mode, context.Mode),
                (ArtifactPropertyKeys.Rank, context.Rank),
                (ArtifactPropertyKeys.SupportedDeployment, context.SupportedDeployment),
                (ArtifactPropertyKeys.MessageName, context.MessageName),
                (ArtifactPropertyKeys.PrimaryEntity, context.PrimaryEntity),
                (ArtifactPropertyKeys.HandlerPluginTypeName, context.HandlerPluginTypeName),
                (ArtifactPropertyKeys.FilteringAttributes, context.FilteringAttributes),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FamilyArtifact? CreatePluginStepImageArtifact(
        System.Text.Json.Nodes.JsonObject row,
        IReadOnlyDictionary<Guid, PluginStepContext> pluginStepsById)
    {
        var imageName = GetString(row, "name");
        if (string.IsNullOrWhiteSpace(imageName))
        {
            return null;
        }

        var parentStepLogicalName = string.Empty;
        var parentStepId = GetGuid(row, "_sdkmessageprocessingstepid_value");
        if (parentStepId.HasValue && pluginStepsById.TryGetValue(parentStepId.Value, out var mappedStep))
        {
            parentStepLogicalName = mappedStep.LogicalName;
        }

        var entityAlias = NormalizeLogicalName(GetString(row, "entityalias"));
        var imageType = GetString(row, "imagetype");
        var selectedAttributes = NormalizeAttributeList(GetString(row, "attributes"));
        var logicalName = BuildPluginStepImageLogicalName(parentStepLogicalName, imageName, entityAlias, imageType);
        var summaryJson = SerializeJson(new
        {
            imageName,
            parentStepLogicalName,
            entityAlias,
            imageType,
            messagePropertyName = GetString(row, "messagepropertyname"),
            selectedAttributes
        });

        return new FamilyArtifact(
            ComponentFamily.PluginStepImage,
            logicalName,
            imageName,
            $"sdkmessageprocessingstepimages/{logicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.Description, GetString(row, "description")),
                (ArtifactPropertyKeys.ParentPluginStepLogicalName, parentStepLogicalName),
                (ArtifactPropertyKeys.EntityAlias, entityAlias),
                (ArtifactPropertyKeys.ImageType, imageType),
                (ArtifactPropertyKeys.MessagePropertyName, GetString(row, "messagepropertyname")),
                (ArtifactPropertyKeys.SelectedAttributes, selectedAttributes),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FamilyArtifact? CreateServiceEndpointArtifact(System.Text.Json.Nodes.JsonObject row)
    {
        var name = GetString(row, "name");
        var logicalName = NormalizeLogicalName(GetString(row, "logical_name", "logicalname") ?? name);
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        var isCustomizable = StringValue(GetProperty(row, "iscustomizable"));
        var messageCharset = NormalizeServiceEndpointMessageCharset(GetString(row, "messagecharset"));
        var summaryJson = SerializeJson(new
        {
            name,
            contract = GetString(row, "contract"),
            connectionMode = GetString(row, "connectionmode"),
            authType = GetString(row, "authtype"),
            namespaceAddress = GetString(row, "namespaceaddress"),
            endpointPath = GetString(row, "path"),
            url = GetString(row, "url"),
            messageFormat = GetString(row, "messageformat"),
            messageCharset,
            introducedVersion = GetString(row, "introducedversion"),
            isCustomizable = string.IsNullOrWhiteSpace(isCustomizable) ? null : NormalizeBoolean(isCustomizable)
        });

        return new FamilyArtifact(
            ComponentFamily.ServiceEndpoint,
            logicalName,
            name ?? logicalName,
            $"serviceendpoints/{logicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.Name, name),
                (ArtifactPropertyKeys.Description, GetString(row, "description")),
                (ArtifactPropertyKeys.Contract, GetString(row, "contract")),
                (ArtifactPropertyKeys.ConnectionMode, GetString(row, "connectionmode")),
                (ArtifactPropertyKeys.AuthType, GetString(row, "authtype")),
                (ArtifactPropertyKeys.NamespaceAddress, GetString(row, "namespaceaddress")),
                (ArtifactPropertyKeys.EndpointPath, GetString(row, "path")),
                (ArtifactPropertyKeys.Url, GetString(row, "url")),
                (ArtifactPropertyKeys.MessageFormat, GetString(row, "messageformat")),
                (ArtifactPropertyKeys.MessageCharset, messageCharset),
                (ArtifactPropertyKeys.IntroducedVersion, GetString(row, "introducedversion")),
                (ArtifactPropertyKeys.IsCustomizable, string.IsNullOrWhiteSpace(isCustomizable) ? null : NormalizeBoolean(isCustomizable)),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FamilyArtifact? CreateConnectorArtifact(System.Text.Json.Nodes.JsonObject row)
    {
        var name = GetString(row, "name");
        var connectorInternalId = NormalizeLogicalName(GetString(row, "connectorinternalid"));
        var logicalName = NormalizeLogicalName(GetString(row, "logical_name", "logicalname") ?? connectorInternalId ?? name);
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        var displayName = GetString(row, "displayname") ?? name ?? logicalName;
        var capabilitiesJson = NormalizeConnectorCapabilitiesJson(GetProperty(row, "capabilities"));
        var isCustomizable = StringValue(GetProperty(row, "iscustomizable"));
        var summaryJson = SerializeJson(new
        {
            name,
            displayName,
            description = GetString(row, "description"),
            connectorInternalId,
            connectorType = GetString(row, "connectortype"),
            capabilities = string.IsNullOrWhiteSpace(capabilitiesJson) ? null : System.Text.Json.Nodes.JsonNode.Parse(capabilitiesJson),
            introducedVersion = GetString(row, "introducedversion"),
            isCustomizable = string.IsNullOrWhiteSpace(isCustomizable) ? null : NormalizeBoolean(isCustomizable)
        });

        return new FamilyArtifact(
            ComponentFamily.Connector,
            logicalName,
            displayName,
            $"connectors/{logicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.Name, name),
                (ArtifactPropertyKeys.Description, GetString(row, "description")),
                (ArtifactPropertyKeys.ConnectorInternalId, connectorInternalId),
                (ArtifactPropertyKeys.ConnectorType, GetString(row, "connectortype")),
                (ArtifactPropertyKeys.CapabilitiesJson, capabilitiesJson),
                (ArtifactPropertyKeys.IntroducedVersion, GetString(row, "introducedversion")),
                (ArtifactPropertyKeys.IsCustomizable, string.IsNullOrWhiteSpace(isCustomizable) ? null : NormalizeBoolean(isCustomizable)),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static string? ResolveAssemblyFullName(
        System.Text.Json.Nodes.JsonObject row,
        IReadOnlyDictionary<Guid, string> assemblyFullNamesById)
    {
        var assemblyFullName = GetString(row, "assemblyname");
        if (!string.IsNullOrWhiteSpace(assemblyFullName))
        {
            return assemblyFullName;
        }

        var pluginAssemblyId = GetGuid(row, "_pluginassemblyid_value");
        return pluginAssemblyId.HasValue && assemblyFullNamesById.TryGetValue(pluginAssemblyId.Value, out var mappedFullName)
            ? mappedFullName
            : null;
    }

    private static string? BuildPluginAssemblyFullName(System.Text.Json.Nodes.JsonObject row)
    {
        var explicitFullName = GetString(row, "full_name", "assemblyname");
        if (!string.IsNullOrWhiteSpace(explicitFullName))
        {
            return explicitFullName;
        }

        var name = GetString(row, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var version = GetString(row, "version") ?? "0.0.0.0";
        var culture = GetString(row, "culture") ?? "neutral";
        var publicKeyToken = GetString(row, "publickeytoken") ?? "null";
        return $"{name}, Version={version}, Culture={culture}, PublicKeyToken={publicKeyToken}";
    }

    private static string BuildPluginStepLogicalName(
        string? handlerPluginTypeName,
        string? messageName,
        string? primaryEntity,
        string? stage,
        string? mode,
        string? stepName) =>
        string.Join("|",
            new[]
            {
                handlerPluginTypeName?.Trim() ?? "handler",
                messageName?.Trim() ?? "message",
                primaryEntity?.Trim() ?? "*",
                stage?.Trim() ?? "stage",
                mode?.Trim() ?? "mode",
                stepName?.Trim() ?? "step"
            });

    private static string BuildPluginStepImageLogicalName(
        string? parentStepLogicalName,
        string? imageName,
        string? entityAlias,
        string? imageType) =>
        string.Join("|",
            new[]
            {
                parentStepLogicalName?.Trim() ?? "step",
                imageName?.Trim() ?? "image",
                entityAlias?.Trim() ?? "alias",
                imageType?.Trim() ?? "type"
            });

    private static string? NormalizeAttributeList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var values = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeLogicalName)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values.Length == 0 ? null : string.Join(",", values);
    }

    private static string? NormalizeConnectorCapabilitiesJson(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is System.Text.Json.Nodes.JsonArray array)
        {
            return SerializeJson(array
                .Select(StringValue)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => NormalizeLogicalName(value)!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray());
        }

        var raw = StringValue(node);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var normalizedJson = NormalizeJson(raw);
        if (!string.IsNullOrWhiteSpace(normalizedJson)
            && System.Text.Json.Nodes.JsonNode.Parse(normalizedJson) is System.Text.Json.Nodes.JsonArray parsedArray)
        {
            return SerializeJson(parsedArray
                .Select(StringValue)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => NormalizeLogicalName(value)!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray());
        }

        return SerializeJson(raw
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeLogicalName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    private static string? NormalizeServiceEndpointMessageCharset(string? value)
    {
        var normalized = NormalizeLogicalName(value);
        return normalized switch
        {
            null => null,
            "0" or "default" => "0",
            "1" or "utf8" or "65001" => "65001",
            "2" or "windows1252" or "1252" => "1252",
            _ => normalized
        };
    }

    private sealed record PluginStepContext(
        Guid Id,
        string LogicalName,
        string DisplayName,
        string? Description,
        string? Stage,
        string? Mode,
        string? Rank,
        string? SupportedDeployment,
        string? MessageName,
        string? PrimaryEntity,
        string? HandlerPluginTypeName,
        string? FilteringAttributes);
}
