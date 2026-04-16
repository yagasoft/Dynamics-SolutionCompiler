using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using System.Diagnostics;
using Azure.Core;
using Azure.Identity;
using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Apply;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Apply;

public sealed class WebApiApplyExecutor : IApplyExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };
    private static readonly ComponentFamily[] DevApplySupportedFamilies =
    [
        ComponentFamily.ImageConfiguration,
        ComponentFamily.EntityAnalyticsConfiguration,
        ComponentFamily.PluginAssembly,
        ComponentFamily.PluginType,
        ComponentFamily.PluginStep,
        ComponentFamily.PluginStepImage,
        ComponentFamily.ServiceEndpoint,
        ComponentFamily.Connector,
        ComponentFamily.MobileOfflineProfile,
        ComponentFamily.MobileOfflineProfileItem,
        ComponentFamily.ConnectionRole
    ];

    private readonly HttpClient? _httpClientOverride;
    private readonly TokenCredential? _credentialOverride;
    private string? _accessToken;
    private Uri? _serviceRoot;

    public WebApiApplyExecutor()
    {
    }

    internal WebApiApplyExecutor(HttpClient httpClient, TokenCredential credential)
    {
        _httpClientOverride = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _credentialOverride = credential ?? throw new ArgumentNullException(nameof(credential));
    }

    public static IReadOnlyList<ComponentFamily> SupportedDevApplyFamilies => DevApplySupportedFamilies;

    public ApplyResult Apply(CanonicalSolution model, ApplyRequest request)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(request);

        var diagnostics = new List<CompilerDiagnostic>();
        var appliedFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (request.Environment.DataverseUrl is null)
        {
            return new ApplyResult(
                Success: false,
                request.Mode,
                [],
                [
                    new CompilerDiagnostic(
                        "apply-missing-url",
                        DiagnosticSeverity.Error,
                        "Live Dataverse apply requires Environment.DataverseUrl.")
                ]);
        }

        var imageConfigurations = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.ImageConfiguration)
            .ToArray();
        var entityAnalyticsConfigurations = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.EntityAnalyticsConfiguration)
            .ToArray();
        var aiProjectTypes = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.AiProjectType)
            .ToArray();
        var aiProjects = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.AiProject)
            .ToArray();
        var aiConfigurations = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.AiConfiguration)
            .ToArray();
        var pluginAssemblies = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.PluginAssembly)
            .ToArray();
        var pluginTypes = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.PluginType)
            .ToArray();
        var pluginSteps = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.PluginStep)
            .ToArray();
        var pluginStepImages = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.PluginStepImage)
            .ToArray();
        var serviceEndpoints = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.ServiceEndpoint)
            .ToArray();
        var connectors = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.Connector)
            .ToArray();
        var mobileOfflineProfiles = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.MobileOfflineProfile)
            .ToArray();
        var mobileOfflineProfileItems = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.MobileOfflineProfileItem)
            .ToArray();
        var connectionRoles = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.ConnectionRole)
            .ToArray();
        if (aiProjectTypes.Length > 0 || aiProjects.Length > 0 || aiConfigurations.Length > 0)
        {
            diagnostics.Add(new CompilerDiagnostic(
                "apply-ai-families-unsupported-live",
                DiagnosticSeverity.Error,
                "Compact AI families remain an explicit non-live-rebuildable boundary in the current environment. Dataverse rejects AITemplate creation with OperationNotSupported, so publish cannot finalize AI project types, AI projects, or AI configurations from intent.",
                request.Environment.DataverseUrl.ToString()));

            return new ApplyResult(false, request.Mode, [], diagnostics);
        }

        if (imageConfigurations.Length == 0
            && entityAnalyticsConfigurations.Length == 0
            && pluginAssemblies.Length == 0
            && pluginTypes.Length == 0
            && pluginSteps.Length == 0
            && pluginStepImages.Length == 0
            && serviceEndpoints.Length == 0
            && connectors.Length == 0
            && mobileOfflineProfiles.Length == 0
            && mobileOfflineProfileItems.Length == 0
            && connectionRoles.Length == 0)
        {
            diagnostics.Add(new CompilerDiagnostic(
                "apply-noop",
                DiagnosticSeverity.Info,
                "No live metadata apply steps were required for this model.",
                request.Environment.DataverseUrl.ToString()));

            return new ApplyResult(true, request.Mode, [], diagnostics);
        }

        try
        {
            _serviceRoot = BuildServiceRoot(request.Environment.DataverseUrl);
            using var ownedHttpClient = _httpClientOverride is null ? new HttpClient() : null;
            var httpClient = _httpClientOverride ?? ownedHttpClient!;
            var credential = _credentialOverride ?? CreateCredential(request);
            var solutionId = EnsureSolutionShellAsync(httpClient, credential, model, diagnostics, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            var applyCount = ApplyImageConfigurationsAsync(httpClient, credential, imageConfigurations, diagnostics, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            if (applyCount > 0)
            {
                appliedFamilies.Add(ComponentFamily.ImageConfiguration.ToString());
                diagnostics.Add(new CompilerDiagnostic(
                    "apply-image-config-applied",
                    DiagnosticSeverity.Info,
                    $"Applied {applyCount} live image-configuration metadata change(s) after import.",
                    request.Environment.DataverseUrl.ToString()));
            }
            else
            {
                diagnostics.Add(new CompilerDiagnostic(
                    "apply-image-config-noop",
                    DiagnosticSeverity.Info,
                    "Image-configuration metadata already matched the requested live state; no post-import metadata updates were required.",
                    request.Environment.DataverseUrl.ToString()));
            }

            var entityAnalyticsApplyCount = ApplyEntityAnalyticsConfigurationsAsync(
                    httpClient,
                    credential,
                    solutionId,
                    model.Identity.UniqueName,
                    entityAnalyticsConfigurations,
                    diagnostics,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            if (entityAnalyticsApplyCount > 0)
            {
                appliedFamilies.Add(ComponentFamily.EntityAnalyticsConfiguration.ToString());
                diagnostics.Add(new CompilerDiagnostic(
                    "apply-entity-analytics-applied",
                    DiagnosticSeverity.Info,
                    $"Applied {entityAnalyticsApplyCount} live entity-analytics metadata change(s) after import.",
                    request.Environment.DataverseUrl.ToString()));
            }
            else if (entityAnalyticsConfigurations.Length > 0)
            {
                diagnostics.Add(new CompilerDiagnostic(
                    "apply-entity-analytics-noop",
                    DiagnosticSeverity.Info,
                    "Entity-analytics metadata already matched the requested live state; no post-import metadata updates were required.",
                    request.Environment.DataverseUrl.ToString()));
            }

            var aiApplyCount = ApplyAiFamiliesAsync(
                    httpClient,
                    credential,
                    solutionId,
                    model.Identity.UniqueName,
                    aiProjectTypes,
                    aiProjects,
                    aiConfigurations,
                    diagnostics,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            if (aiApplyCount > 0)
            {
                if (aiProjectTypes.Length > 0)
                {
                    appliedFamilies.Add(ComponentFamily.AiProjectType.ToString());
                }

                if (aiProjects.Length > 0)
                {
                    appliedFamilies.Add(ComponentFamily.AiProject.ToString());
                }

                if (aiConfigurations.Length > 0)
                {
                    appliedFamilies.Add(ComponentFamily.AiConfiguration.ToString());
                }

                diagnostics.Add(new CompilerDiagnostic(
                    "apply-ai-families-applied",
                    DiagnosticSeverity.Info,
                    $"Applied {aiApplyCount} live AI metadata change(s) after import.",
                    request.Environment.DataverseUrl.ToString()));
            }
            else if (aiProjectTypes.Length > 0 || aiProjects.Length > 0 || aiConfigurations.Length > 0)
            {
                diagnostics.Add(new CompilerDiagnostic(
                    "apply-ai-families-noop",
                    DiagnosticSeverity.Info,
                    "AI family metadata already matched the requested live state; no post-import metadata updates were required.",
                    request.Environment.DataverseUrl.ToString()));
            }

            var codeExtensibilityApplyCount = ApplyCodeExtensibilityFamiliesAsync(
                    httpClient,
                    credential,
                    solutionId,
                    model.Identity.UniqueName,
                    pluginAssemblies,
                    pluginTypes,
                    pluginSteps,
                    pluginStepImages,
                    serviceEndpoints,
                    connectors,
                    diagnostics,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            if (codeExtensibilityApplyCount > 0)
            {
                if (pluginAssemblies.Length > 0)
                {
                    appliedFamilies.Add(ComponentFamily.PluginAssembly.ToString());
                }

                if (pluginTypes.Length > 0)
                {
                    appliedFamilies.Add(ComponentFamily.PluginType.ToString());
                }

                if (pluginSteps.Length > 0)
                {
                    appliedFamilies.Add(ComponentFamily.PluginStep.ToString());
                }

                if (pluginStepImages.Length > 0)
                {
                    appliedFamilies.Add(ComponentFamily.PluginStepImage.ToString());
                }

                if (serviceEndpoints.Length > 0)
                {
                    appliedFamilies.Add(ComponentFamily.ServiceEndpoint.ToString());
                }

                if (connectors.Length > 0)
                {
                    appliedFamilies.Add(ComponentFamily.Connector.ToString());
                }

                diagnostics.Add(new CompilerDiagnostic(
                    "apply-code-extensibility-families-applied",
                    DiagnosticSeverity.Info,
                    $"Applied {codeExtensibilityApplyCount} live code/extensibility metadata change(s) after import.",
                    request.Environment.DataverseUrl.ToString()));
            }
            else if (pluginAssemblies.Length > 0
                || pluginTypes.Length > 0
                || pluginSteps.Length > 0
                || pluginStepImages.Length > 0
                || serviceEndpoints.Length > 0
                || connectors.Length > 0)
            {
                diagnostics.Add(new CompilerDiagnostic(
                    "apply-code-extensibility-families-noop",
                    DiagnosticSeverity.Info,
                    "Code/extensibility metadata already matched the requested live state; no post-import metadata updates were required.",
                    request.Environment.DataverseUrl.ToString()));
            }

            var processSecurityApplyCount = ApplyHybridProcessSecurityFamiliesAsync(
                    httpClient,
                    credential,
                    solutionId,
                    model.Identity.UniqueName,
                    mobileOfflineProfiles,
                    mobileOfflineProfileItems,
                    connectionRoles,
                    diagnostics,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            if (processSecurityApplyCount > 0)
            {
                if (mobileOfflineProfiles.Length > 0)
                {
                    appliedFamilies.Add(ComponentFamily.MobileOfflineProfile.ToString());
                }

                if (mobileOfflineProfileItems.Length > 0)
                {
                    appliedFamilies.Add(ComponentFamily.MobileOfflineProfileItem.ToString());
                }

                if (connectionRoles.Length > 0)
                {
                    appliedFamilies.Add(ComponentFamily.ConnectionRole.ToString());
                }

                diagnostics.Add(new CompilerDiagnostic(
                    "apply-process-security-families-applied",
                    DiagnosticSeverity.Info,
                    $"Applied {processSecurityApplyCount} live process/security metadata change(s) after import.",
                    request.Environment.DataverseUrl.ToString()));
            }
            else if (mobileOfflineProfiles.Length > 0
                || mobileOfflineProfileItems.Length > 0
                || connectionRoles.Length > 0)
            {
                diagnostics.Add(new CompilerDiagnostic(
                    "apply-process-security-families-noop",
                    DiagnosticSeverity.Info,
                    "Process/security metadata already matched the requested live state; no post-import metadata updates were required.",
                    request.Environment.DataverseUrl.ToString()));
            }

            return new ApplyResult(true, request.Mode, appliedFamilies.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(), diagnostics);
        }
        catch (Exception exception) when (exception is AuthenticationFailedException or HttpRequestException or InvalidOperationException)
        {
            diagnostics.Add(new CompilerDiagnostic(
                "apply-live-failure",
                DiagnosticSeverity.Error,
                $"Live Dataverse apply failed: {exception.Message}",
                request.Environment.DataverseUrl.ToString()));
            return new ApplyResult(false, request.Mode, appliedFamilies.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(), diagnostics);
        }
    }

    private async Task<int> ApplyImageConfigurationsAsync(
        HttpClient httpClient,
        TokenCredential credential,
        IReadOnlyList<FamilyArtifact> imageConfigurations,
        ICollection<CompilerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var changeCount = 0;

        var entityConfigurations = imageConfigurations
            .Where(artifact => string.Equals(GetArtifactProperty(artifact, ArtifactPropertyKeys.ImageConfigurationScope), "entity", StringComparison.OrdinalIgnoreCase))
            .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var entityConfiguration in entityConfigurations)
        {
            var entityLogicalName = GetArtifactProperty(entityConfiguration, ArtifactPropertyKeys.EntityLogicalName);
            var primaryImageAttribute = GetArtifactProperty(entityConfiguration, ArtifactPropertyKeys.PrimaryImageAttribute);
            if (string.IsNullOrWhiteSpace(entityLogicalName) || string.IsNullOrWhiteSpace(primaryImageAttribute))
            {
                continue;
            }

            var current = await FindSingleAsync(
                httpClient,
                credential,
                $"entityimageconfigs?$select=entityimageconfigid,parententitylogicalname,primaryimageattribute&$filter=parententitylogicalname eq '{EscapeODataLiteral(entityLogicalName)}'",
                cancellationToken).ConfigureAwait(false);
            var currentPrimaryImageAttribute = NormalizeLogicalName(GetString(current, "primaryimageattribute"));
            if (string.Equals(currentPrimaryImageAttribute, NormalizeLogicalName(primaryImageAttribute), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var payload = new JsonObject
            {
                ["parententitylogicalname"] = entityLogicalName,
                ["primaryimageattribute"] = primaryImageAttribute
            };

            if (GetGuid(current, "entityimageconfigid") is Guid entityImageConfigId)
            {
                await SendJsonAsync(httpClient, credential, HttpMethod.Patch, $"entityimageconfigs({entityImageConfigId:D})", payload, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await SendJsonAsync(httpClient, credential, HttpMethod.Post, "entityimageconfigs", payload, cancellationToken).ConfigureAwait(false);
            }

            changeCount++;
        }

        var attributeConfigurations = imageConfigurations
            .Where(artifact => string.Equals(GetArtifactProperty(artifact, ArtifactPropertyKeys.ImageConfigurationScope), "attribute", StringComparison.OrdinalIgnoreCase))
            .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var attributeConfiguration in attributeConfigurations)
        {
            var entityLogicalName = GetArtifactProperty(attributeConfiguration, ArtifactPropertyKeys.EntityLogicalName);
            var attributeLogicalName = GetArtifactProperty(attributeConfiguration, ArtifactPropertyKeys.ImageAttributeLogicalName);
            var desiredCanStoreFullImage = string.Equals(GetArtifactProperty(attributeConfiguration, ArtifactPropertyKeys.CanStoreFullImage), "true", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(entityLogicalName) || string.IsNullOrWhiteSpace(attributeLogicalName))
            {
                continue;
            }

            var current = await FindSingleAsync(
                httpClient,
                credential,
                $"attributeimageconfigs?$select=attributeimageconfigid,parententitylogicalname,attributelogicalname,canstorefullimage&$filter=parententitylogicalname eq '{EscapeODataLiteral(entityLogicalName)}' and attributelogicalname eq '{EscapeODataLiteral(attributeLogicalName)}'",
                cancellationToken).ConfigureAwait(false);
            var currentCanStoreFullImage = string.Equals(NormalizeBoolean(GetString(current, "canstorefullimage")), "true", StringComparison.OrdinalIgnoreCase);
            if (current is not null && currentCanStoreFullImage == desiredCanStoreFullImage)
            {
                continue;
            }

            var payload = new JsonObject
            {
                ["parententitylogicalname"] = entityLogicalName,
                ["attributelogicalname"] = attributeLogicalName,
                ["canstorefullimage"] = desiredCanStoreFullImage
            };

            if (GetGuid(current, "attributeimageconfigid") is Guid attributeImageConfigId)
            {
                await SendJsonAsync(httpClient, credential, HttpMethod.Patch, $"attributeimageconfigs({attributeImageConfigId:D})", payload, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await SendJsonAsync(httpClient, credential, HttpMethod.Post, "attributeimageconfigs", payload, cancellationToken).ConfigureAwait(false);
            }

            changeCount++;
        }

        if (changeCount > 0)
        {
            await SendJsonAsync(httpClient, credential, HttpMethod.Post, "PublishAllXml", new JsonObject(), cancellationToken).ConfigureAwait(false);
        }

        diagnostics.Add(new CompilerDiagnostic(
            "apply-image-config-targets",
            DiagnosticSeverity.Info,
            $"Resolved {entityConfigurations.Length} entity image configuration target(s) and {attributeConfigurations.Length} attribute image configuration target(s).",
            _serviceRoot?.ToString()));

        return changeCount;
    }

    private async Task<int> ApplyEntityAnalyticsConfigurationsAsync(
        HttpClient httpClient,
        TokenCredential credential,
        Guid? solutionId,
        string solutionUniqueName,
        IReadOnlyList<FamilyArtifact> entityAnalyticsConfigurations,
        ICollection<CompilerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (entityAnalyticsConfigurations.Count == 0)
        {
            return 0;
        }

        var changeCount = 0;

        foreach (var configuration in entityAnalyticsConfigurations.OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var parentEntityLogicalName = GetArtifactProperty(configuration, ArtifactPropertyKeys.ParentEntityLogicalName)
                ?? NormalizeLogicalName(configuration.LogicalName);
            if (string.IsNullOrWhiteSpace(parentEntityLogicalName))
            {
                continue;
            }

            var desiredEntityDataSource = NormalizeEntityAnalyticsDataSource(GetArtifactProperty(configuration, ArtifactPropertyKeys.EntityDataSource));
            var desiredIsEnabledForAdls = string.Equals(GetArtifactProperty(configuration, ArtifactPropertyKeys.IsEnabledForAdls), "true", StringComparison.OrdinalIgnoreCase);
            var desiredIsEnabledForTimeSeries = string.Equals(GetArtifactProperty(configuration, ArtifactPropertyKeys.IsEnabledForTimeSeries), "true", StringComparison.OrdinalIgnoreCase);

            var current = await FindSingleAsync(
                httpClient,
                credential,
                $"entityanalyticsconfigs?$select=entityanalyticsconfigid,parententitylogicalname,entitydatasource,isenabledforadls,isenabledfortimeseries&$filter=parententitylogicalname eq '{EscapeODataLiteral(parentEntityLogicalName)}'",
                cancellationToken).ConfigureAwait(false);

            var currentEntityDataSource = NormalizeEntityAnalyticsDataSource(GetString(current, "entitydatasource"));
            var currentIsEnabledForAdls = string.Equals(NormalizeBoolean(GetString(current, "isenabledforadls")), "true", StringComparison.OrdinalIgnoreCase);
            var currentIsEnabledForTimeSeries = string.Equals(NormalizeBoolean(GetString(current, "isenabledfortimeseries")), "true", StringComparison.OrdinalIgnoreCase);
            var needsUpdate = current is null
                || !string.Equals(currentEntityDataSource ?? string.Empty, desiredEntityDataSource ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || currentIsEnabledForAdls != desiredIsEnabledForAdls
                || currentIsEnabledForTimeSeries != desiredIsEnabledForTimeSeries;

            if (needsUpdate)
            {
                var entityDataSourceOptionValue = ResolveEntityAnalyticsDataSourceOptionValue(desiredEntityDataSource);
                var payload = new JsonObject
                {
                    ["parententitylogicalname"] = parentEntityLogicalName,
                    ["entitydatasource"] = entityDataSourceOptionValue,
                    ["isenabledforadls"] = desiredIsEnabledForAdls,
                    ["isenabledfortimeseries"] = desiredIsEnabledForTimeSeries
                };

                if (GetGuid(current, "entityanalyticsconfigid") is Guid configurationId)
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Patch, $"entityanalyticsconfigs({configurationId:D})", payload, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Post, "entityanalyticsconfigs", payload, cancellationToken).ConfigureAwait(false);
                }

                changeCount++;
                current = await FindSingleAsync(
                    httpClient,
                    credential,
                    $"entityanalyticsconfigs?$select=entityanalyticsconfigid,parententitylogicalname&$filter=parententitylogicalname eq '{EscapeODataLiteral(parentEntityLogicalName)}'",
                    cancellationToken).ConfigureAwait(false);
            }

            if (solutionId.HasValue && GetGuid(current, "entityanalyticsconfigid") is Guid componentId)
            {
                var addedToSolution = await EnsureSolutionComponentAsync(
                    httpClient,
                    credential,
                    solutionId.Value,
                    solutionUniqueName,
                    componentId,
                    430,
                    cancellationToken).ConfigureAwait(false);
                if (addedToSolution)
                {
                    changeCount++;
                }
            }
        }

        if (changeCount > 0)
        {
            await SendJsonAsync(httpClient, credential, HttpMethod.Post, "PublishAllXml", new JsonObject(), cancellationToken).ConfigureAwait(false);
        }

        diagnostics.Add(new CompilerDiagnostic(
            "apply-entity-analytics-targets",
            DiagnosticSeverity.Info,
            $"Resolved {entityAnalyticsConfigurations.Count} entity analytics configuration target(s).",
            _serviceRoot?.ToString()));

        return changeCount;
    }

    private async Task<int> ApplyAiFamiliesAsync(
        HttpClient httpClient,
        TokenCredential credential,
        Guid? solutionId,
        string solutionUniqueName,
        IReadOnlyList<FamilyArtifact> aiProjectTypes,
        IReadOnlyList<FamilyArtifact> aiProjects,
        IReadOnlyList<FamilyArtifact> aiConfigurations,
        ICollection<CompilerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (aiProjectTypes.Count == 0 && aiProjects.Count == 0 && aiConfigurations.Count == 0)
        {
            return 0;
        }

        var changeCount = 0;
        var projectTypeIdsByLogicalName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var projectIdsByLogicalName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var projectType in aiProjectTypes.OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var logicalName = NormalizeLogicalName(projectType.LogicalName);
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            var desiredName = projectType.DisplayName ?? logicalName;
            var desiredDescription = GetArtifactProperty(projectType, ArtifactPropertyKeys.Description);
            var desiredResourceInfo = BuildAiProjectTypeResourceInfo(desiredName, desiredDescription);

            var current = await FindSingleAsync(
                httpClient,
                credential,
                $"msdyn_aitemplates?$select=msdyn_aitemplateid,msdyn_uniquename,msdyn_resourceinfo,msdyn_templateversion,msdyn_istrainable&$filter=msdyn_uniquename eq '{EscapeODataLiteral(logicalName)}'",
                cancellationToken).ConfigureAwait(false);

            var currentName = GetAiProjectTypeResourceInfoValue(current, "displayName");
            var currentDescription = GetAiProjectTypeResourceInfoValue(current, "description");
            var needsUpdate = current is null
                || !string.Equals(currentName ?? string.Empty, desiredName, StringComparison.Ordinal)
                || !string.Equals(currentDescription ?? string.Empty, desiredDescription ?? string.Empty, StringComparison.Ordinal);

            if (needsUpdate)
            {
                var payload = new JsonObject
                {
                    ["msdyn_uniquename"] = logicalName,
                    ["msdyn_istrainable"] = false,
                    ["msdyn_resourceinfo"] = desiredResourceInfo,
                    ["msdyn_templateversion"] = 1
                };

                if (GetGuid(current, "msdyn_aitemplateid") is Guid projectTypeId)
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Patch, $"msdyn_aitemplates({projectTypeId:D})", payload, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Post, "msdyn_aitemplates", payload, cancellationToken).ConfigureAwait(false);
                }

                changeCount++;
                current = await FindSingleAsync(
                    httpClient,
                    credential,
                    $"msdyn_aitemplates?$select=msdyn_aitemplateid,msdyn_uniquename,msdyn_resourceinfo&$filter=msdyn_uniquename eq '{EscapeODataLiteral(logicalName)}'",
                    cancellationToken).ConfigureAwait(false);
            }

            if (GetGuid(current, "msdyn_aitemplateid") is Guid currentProjectTypeId)
            {
                projectTypeIdsByLogicalName[logicalName] = currentProjectTypeId;

                if (solutionId.HasValue)
                {
                    var added = await EnsureSolutionComponentAsync(
                        httpClient,
                        credential,
                        solutionId.Value,
                        solutionUniqueName,
                        currentProjectTypeId,
                        400,
                        cancellationToken).ConfigureAwait(false);
                    if (added)
                    {
                        changeCount++;
                    }
                }
            }
        }

        foreach (var project in aiProjects.OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var logicalName = NormalizeLogicalName(project.LogicalName);
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            var parentProjectTypeLogicalName = NormalizeLogicalName(GetArtifactProperty(project, ArtifactPropertyKeys.ParentAiProjectTypeLogicalName));
            var desiredName = project.DisplayName ?? logicalName;
            var desiredDescription = GetArtifactProperty(project, ArtifactPropertyKeys.Description);
            var desiredTargetEntity = NormalizeLogicalName(GetArtifactProperty(project, ArtifactPropertyKeys.TargetEntity));
            var desiredCreationContext = BuildAiProjectCreationContext(logicalName, desiredDescription, desiredTargetEntity);

            Guid? resolvedParentProjectTypeId = null;
            if (!string.IsNullOrWhiteSpace(parentProjectTypeLogicalName))
            {
                if (!projectTypeIdsByLogicalName.TryGetValue(parentProjectTypeLogicalName, out var parentProjectTypeId))
                {
                    var parentProjectType = await FindSingleAsync(
                        httpClient,
                        credential,
                        $"msdyn_aitemplates?$select=msdyn_aitemplateid,msdyn_uniquename&$filter=msdyn_uniquename eq '{EscapeODataLiteral(parentProjectTypeLogicalName)}'",
                        cancellationToken).ConfigureAwait(false);
                    if (GetGuid(parentProjectType, "msdyn_aitemplateid") is Guid resolvedProjectTypeId)
                    {
                        parentProjectTypeId = resolvedProjectTypeId;
                        projectTypeIdsByLogicalName[parentProjectTypeLogicalName] = resolvedProjectTypeId;
                    }
                }

                if (projectTypeIdsByLogicalName.TryGetValue(parentProjectTypeLogicalName, out var boundProjectTypeId))
                {
                    resolvedParentProjectTypeId = boundProjectTypeId;
                }
            }

            JsonObject? current = null;
            if (resolvedParentProjectTypeId.HasValue)
            {
                current = (await GetRowsAsync(
                    httpClient,
                    credential,
                    $"msdyn_aimodels?$select=msdyn_aimodelid,msdyn_name,msdyn_modelcreationcontext,_msdyn_templateid_value&$filter=_msdyn_templateid_value eq {FormatGuid(resolvedParentProjectTypeId.Value)}",
                    cancellationToken).ConfigureAwait(false))
                    .FirstOrDefault(row =>
                    {
                        var currentLogicalName = NormalizeLogicalName(GetAiProjectContextValue(row, "logicalName"));
                        return string.Equals(currentLogicalName, logicalName, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(GetString(row, "msdyn_name"), desiredName, StringComparison.Ordinal);
                    });
            }

            current ??= await FindSingleAsync(
                httpClient,
                credential,
                $"msdyn_aimodels?$select=msdyn_aimodelid,msdyn_name,msdyn_modelcreationcontext,_msdyn_templateid_value&$filter=msdyn_name eq '{EscapeODataLiteral(desiredName)}'",
                cancellationToken).ConfigureAwait(false);

            string? currentProjectTypeLogicalName = null;
            if (GetGuid(current, "_msdyn_templateid_value") is Guid currentProjectTypeId
                && projectTypeIdsByLogicalName.FirstOrDefault(pair => pair.Value == currentProjectTypeId) is var mappedProjectType
                && !string.IsNullOrWhiteSpace(mappedProjectType.Key))
            {
                currentProjectTypeLogicalName = mappedProjectType.Key;
            }

            var needsUpdate = current is null
                || !string.Equals(NormalizeLogicalName(GetAiProjectContextValue(current, "logicalName")) ?? string.Empty, logicalName, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(GetString(current, "msdyn_name", "name") ?? string.Empty, desiredName, StringComparison.Ordinal)
                || !string.Equals(GetAiProjectContextValue(current, "description") ?? string.Empty, desiredDescription ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(NormalizeLogicalName(GetAiProjectContextValue(current, "targetEntity")) ?? string.Empty, desiredTargetEntity ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(currentProjectTypeLogicalName ?? string.Empty, parentProjectTypeLogicalName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(NormalizeJson(GetString(current, "msdyn_modelcreationcontext")) ?? string.Empty, desiredCreationContext, StringComparison.Ordinal);

            if (needsUpdate)
            {
                var payload = new JsonObject
                {
                    ["msdyn_name"] = desiredName,
                    ["msdyn_modelcreationcontext"] = desiredCreationContext
                };

                if (resolvedParentProjectTypeId.HasValue)
                {
                    payload["msdyn_templateid@odata.bind"] = $"/msdyn_aitemplates({resolvedParentProjectTypeId.Value:D})";
                }

                if (GetGuid(current, "msdyn_aimodelid") is Guid projectId)
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Patch, $"msdyn_aimodels({projectId:D})", payload, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Post, "msdyn_aimodels", payload, cancellationToken).ConfigureAwait(false);
                }

                changeCount++;
                current = resolvedParentProjectTypeId.HasValue
                    ? (await GetRowsAsync(
                        httpClient,
                        credential,
                        $"msdyn_aimodels?$select=msdyn_aimodelid,msdyn_name,msdyn_modelcreationcontext,_msdyn_templateid_value&$filter=_msdyn_templateid_value eq {FormatGuid(resolvedParentProjectTypeId.Value)}",
                        cancellationToken).ConfigureAwait(false))
                        .FirstOrDefault(row => string.Equals(NormalizeLogicalName(GetAiProjectContextValue(row, "logicalName")), logicalName, StringComparison.OrdinalIgnoreCase))
                    : await FindSingleAsync(
                        httpClient,
                        credential,
                        $"msdyn_aimodels?$select=msdyn_aimodelid,msdyn_name,msdyn_modelcreationcontext&$filter=msdyn_name eq '{EscapeODataLiteral(desiredName)}'",
                        cancellationToken).ConfigureAwait(false);
            }

            if (GetGuid(current, "msdyn_aimodelid") is Guid currentProjectId)
            {
                projectIdsByLogicalName[logicalName] = currentProjectId;

                if (solutionId.HasValue)
                {
                    var added = await EnsureSolutionComponentAsync(
                        httpClient,
                        credential,
                        solutionId.Value,
                        solutionUniqueName,
                        currentProjectId,
                        401,
                        cancellationToken).ConfigureAwait(false);
                    if (added)
                    {
                        changeCount++;
                    }
                }
            }
        }

        foreach (var configuration in aiConfigurations.OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var logicalName = NormalizeLogicalName(configuration.LogicalName);
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            var parentProjectLogicalName = NormalizeLogicalName(GetArtifactProperty(configuration, ArtifactPropertyKeys.ParentAiProjectLogicalName));
            var desiredName = configuration.DisplayName ?? logicalName;
            var desiredConfigurationKind = NormalizeAiConfigurationKind(GetArtifactProperty(configuration, ArtifactPropertyKeys.ConfigurationKind));
            var desiredValue = GetArtifactProperty(configuration, ArtifactPropertyKeys.Value);
            var desiredResourceInfo = BuildAiConfigurationResourceInfo(logicalName, parentProjectLogicalName);

            projectIdsByLogicalName.TryGetValue(parentProjectLogicalName ?? string.Empty, out var boundProjectId);

            JsonObject? current = null;
            if (boundProjectId != Guid.Empty)
            {
                current = (await GetRowsAsync(
                    httpClient,
                    credential,
                    $"msdyn_aiconfigurations?$select=msdyn_aiconfigurationid,msdyn_name,msdyn_type,msdyn_runconfiguration,msdyn_customconfiguration,msdyn_resourceinfo,_msdyn_aimodelid_value&$filter=_msdyn_aimodelid_value eq {FormatGuid(boundProjectId)}",
                    cancellationToken).ConfigureAwait(false))
                    .FirstOrDefault(row =>
                    {
                        var currentLogicalName = NormalizeLogicalName(GetAiConfigurationResourceInfoValue(row, "logicalName"));
                        return string.Equals(currentLogicalName, logicalName, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(GetString(row, "msdyn_name"), desiredName, StringComparison.Ordinal);
                    });
            }

            current ??= await FindSingleAsync(
                httpClient,
                credential,
                $"msdyn_aiconfigurations?$select=msdyn_aiconfigurationid,msdyn_name,msdyn_type,msdyn_runconfiguration,msdyn_customconfiguration,msdyn_resourceinfo,_msdyn_aimodelid_value&$filter=msdyn_name eq '{EscapeODataLiteral(desiredName)}'",
                cancellationToken).ConfigureAwait(false);

            var currentProjectLogicalName = NormalizeLogicalName(GetAiConfigurationResourceInfoValue(current, "parentProjectLogicalName"));
            if (string.IsNullOrWhiteSpace(currentProjectLogicalName)
                && GetGuid(current, "_msdyn_aimodelid_value") is Guid currentProjectId
                && projectIdsByLogicalName.FirstOrDefault(pair => pair.Value == currentProjectId) is var mappedProject
                && !string.IsNullOrWhiteSpace(mappedProject.Key))
            {
                currentProjectLogicalName = mappedProject.Key;
            }

            var currentConfigurationKind = NormalizeAiConfigurationKind(GetString(current, "msdyn_type"));
            var currentValue = GetString(current, "msdyn_runconfiguration") ?? GetString(current, "msdyn_customconfiguration");
            var needsUpdate = current is null
                || !string.Equals(NormalizeLogicalName(GetAiConfigurationResourceInfoValue(current, "logicalName")) ?? string.Empty, logicalName, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(GetString(current, "msdyn_name", "name") ?? string.Empty, desiredName, StringComparison.Ordinal)
                || !string.Equals(currentProjectLogicalName ?? string.Empty, parentProjectLogicalName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(currentConfigurationKind ?? string.Empty, desiredConfigurationKind ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(currentValue ?? string.Empty, desiredValue ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(NormalizeJson(GetString(current, "msdyn_resourceinfo")) ?? string.Empty, desiredResourceInfo, StringComparison.Ordinal);

            if (needsUpdate)
            {
                var payload = new JsonObject
                {
                    ["msdyn_name"] = desiredName,
                    ["msdyn_type"] = ResolveAiConfigurationTypeOptionValue(desiredConfigurationKind),
                    ["msdyn_resourceinfo"] = desiredResourceInfo,
                    ["msdyn_templateversion"] = 1,
                    ["msdyn_majoriterationnumber"] = 1,
                    ["msdyn_minoriterationnumber"] = 0
                };

                if (string.Equals(desiredConfigurationKind, "run", StringComparison.OrdinalIgnoreCase))
                {
                    payload["msdyn_runconfiguration"] = desiredValue;
                }
                else
                {
                    payload["msdyn_customconfiguration"] = desiredValue;
                }

                if (boundProjectId != Guid.Empty)
                {
                    payload["msdyn_aimodelid@odata.bind"] = $"/msdyn_aimodels({boundProjectId:D})";
                }

                if (GetGuid(current, "msdyn_aiconfigurationid") is Guid configurationId)
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Patch, $"msdyn_aiconfigurations({configurationId:D})", payload, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Post, "msdyn_aiconfigurations", payload, cancellationToken).ConfigureAwait(false);
                }

                changeCount++;
                current = boundProjectId != Guid.Empty
                    ? (await GetRowsAsync(
                        httpClient,
                        credential,
                        $"msdyn_aiconfigurations?$select=msdyn_aiconfigurationid,msdyn_name,msdyn_resourceinfo,_msdyn_aimodelid_value&$filter=_msdyn_aimodelid_value eq {FormatGuid(boundProjectId)}",
                        cancellationToken).ConfigureAwait(false))
                        .FirstOrDefault(row => string.Equals(NormalizeLogicalName(GetAiConfigurationResourceInfoValue(row, "logicalName")), logicalName, StringComparison.OrdinalIgnoreCase))
                    : await FindSingleAsync(
                        httpClient,
                        credential,
                        $"msdyn_aiconfigurations?$select=msdyn_aiconfigurationid,msdyn_name,msdyn_resourceinfo&$filter=msdyn_name eq '{EscapeODataLiteral(desiredName)}'",
                        cancellationToken).ConfigureAwait(false);
            }

            if (solutionId.HasValue && GetGuid(current, "msdyn_aiconfigurationid") is Guid currentConfigurationId)
            {
                var added = await EnsureSolutionComponentAsync(
                    httpClient,
                    credential,
                    solutionId.Value,
                    solutionUniqueName,
                    currentConfigurationId,
                    402,
                    cancellationToken).ConfigureAwait(false);
                if (added)
                {
                    changeCount++;
                }
            }
        }

        if (changeCount > 0)
        {
            await SendJsonAsync(httpClient, credential, HttpMethod.Post, "PublishAllXml", new JsonObject(), cancellationToken).ConfigureAwait(false);
        }

        diagnostics.Add(new CompilerDiagnostic(
            "apply-ai-families-targets",
            DiagnosticSeverity.Info,
            $"Resolved {aiProjectTypes.Count} AI project type target(s), {aiProjects.Count} AI project target(s), and {aiConfigurations.Count} AI configuration target(s).",
            _serviceRoot?.ToString()));

        return changeCount;
    }

    private async Task<int> ApplyCodeExtensibilityFamiliesAsync(
        HttpClient httpClient,
        TokenCredential credential,
        Guid? solutionId,
        string solutionUniqueName,
        IReadOnlyList<FamilyArtifact> pluginAssemblies,
        IReadOnlyList<FamilyArtifact> pluginTypes,
        IReadOnlyList<FamilyArtifact> pluginSteps,
        IReadOnlyList<FamilyArtifact> pluginStepImages,
        IReadOnlyList<FamilyArtifact> serviceEndpoints,
        IReadOnlyList<FamilyArtifact> connectors,
        ICollection<CompilerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (pluginAssemblies.Count == 0
            && pluginTypes.Count == 0
            && pluginSteps.Count == 0
            && pluginStepImages.Count == 0
            && serviceEndpoints.Count == 0
            && connectors.Count == 0)
        {
            return 0;
        }

        var changeCount = 0;
        changeCount += await ApplyServiceEndpointsAsync(
            httpClient,
            credential,
            solutionId,
            solutionUniqueName,
            serviceEndpoints,
            cancellationToken).ConfigureAwait(false);
        changeCount += await ApplyConnectorsAsync(
            httpClient,
            credential,
            solutionId,
            solutionUniqueName,
            connectors,
            cancellationToken).ConfigureAwait(false);
        changeCount += await ApplyPluginRegistrationFamiliesAsync(
            httpClient,
            credential,
            solutionId,
            solutionUniqueName,
            pluginAssemblies,
            pluginTypes,
            pluginSteps,
            pluginStepImages,
            diagnostics,
            cancellationToken).ConfigureAwait(false);

        if (changeCount > 0)
        {
            await SendJsonAsync(httpClient, credential, HttpMethod.Post, "PublishAllXml", new JsonObject(), cancellationToken).ConfigureAwait(false);
        }

        diagnostics.Add(new CompilerDiagnostic(
            "apply-code-extensibility-targets",
            DiagnosticSeverity.Info,
            $"Resolved {pluginAssemblies.Count} plug-in assembly target(s), {pluginTypes.Count} plug-in type target(s), {pluginSteps.Count} plug-in step target(s), {pluginStepImages.Count} plug-in step image target(s), {serviceEndpoints.Count} service endpoint target(s), and {connectors.Count} connector target(s).",
            _serviceRoot?.ToString()));

        return changeCount;
    }

    private async Task<int> ApplyHybridProcessSecurityFamiliesAsync(
        HttpClient httpClient,
        TokenCredential credential,
        Guid? solutionId,
        string solutionUniqueName,
        IReadOnlyList<FamilyArtifact> mobileOfflineProfiles,
        IReadOnlyList<FamilyArtifact> mobileOfflineProfileItems,
        IReadOnlyList<FamilyArtifact> connectionRoles,
        ICollection<CompilerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (mobileOfflineProfiles.Count == 0
            && mobileOfflineProfileItems.Count == 0
            && connectionRoles.Count == 0)
        {
            return 0;
        }

        var changeCount = 0;
        changeCount += await ApplyMobileOfflineProfilesAsync(
            httpClient,
            credential,
            solutionId,
            solutionUniqueName,
            mobileOfflineProfiles,
            mobileOfflineProfileItems,
            cancellationToken).ConfigureAwait(false);
        changeCount += await ApplyConnectionRolesAsync(
            httpClient,
            credential,
            solutionId,
            solutionUniqueName,
            connectionRoles,
            cancellationToken).ConfigureAwait(false);

        if (changeCount > 0)
        {
            await SendJsonAsync(httpClient, credential, HttpMethod.Post, "PublishAllXml", new JsonObject(), cancellationToken).ConfigureAwait(false);
        }

        diagnostics.Add(new CompilerDiagnostic(
            "apply-process-security-targets",
            DiagnosticSeverity.Info,
            $"Resolved {mobileOfflineProfiles.Count} mobile offline profile target(s), {mobileOfflineProfileItems.Count} mobile offline profile item target(s), and {connectionRoles.Count} connection role target(s).",
            _serviceRoot?.ToString()));

        return changeCount;
    }

    private async Task<int> ApplyMobileOfflineProfilesAsync(
        HttpClient httpClient,
        TokenCredential credential,
        Guid? solutionId,
        string solutionUniqueName,
        IReadOnlyList<FamilyArtifact> mobileOfflineProfiles,
        IReadOnlyList<FamilyArtifact> mobileOfflineProfileItems,
        CancellationToken cancellationToken)
    {
        var changeCount = 0;
        var profileIdsByLogicalName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in mobileOfflineProfiles.OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var logicalName = NormalizeLogicalName(profile.LogicalName);
            var name = profile.DisplayName ?? logicalName;
            if (string.IsNullOrWhiteSpace(logicalName) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var current = await FindSingleAsync(
                httpClient,
                credential,
                $"mobileofflineprofiles?$select=mobileofflineprofileid,name,description,isvalidated&$filter=name eq '{EscapeODataLiteral(name)}'",
                cancellationToken).ConfigureAwait(false);

            var payload = new JsonObject
            {
                ["name"] = name
            };
            AddStringProperty(payload, "description", GetArtifactProperty(profile, ArtifactPropertyKeys.Description));

            var needsUpdate = current is null
                || !string.Equals(GetString(current, "name") ?? string.Empty, name, StringComparison.Ordinal)
                || !string.Equals(GetString(current, "description") ?? string.Empty, GetArtifactProperty(profile, ArtifactPropertyKeys.Description) ?? string.Empty, StringComparison.Ordinal);

            if (needsUpdate)
            {
                if (GetGuid(current, "mobileofflineprofileid") is Guid profileId)
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Patch, $"mobileofflineprofiles({profileId:D})", payload, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Post, "mobileofflineprofiles", payload, cancellationToken).ConfigureAwait(false);
                }

                changeCount++;
                current = await FindSingleAsync(
                    httpClient,
                    credential,
                    $"mobileofflineprofiles?$select=mobileofflineprofileid,name,description,isvalidated&$filter=name eq '{EscapeODataLiteral(name)}'",
                    cancellationToken).ConfigureAwait(false);
            }

            if (GetGuid(current, "mobileofflineprofileid") is not Guid currentProfileId)
            {
                continue;
            }

            profileIdsByLogicalName[logicalName] = currentProfileId;

            if (solutionId.HasValue)
            {
                var added = await EnsureSolutionComponentAsync(
                    httpClient,
                    credential,
                    solutionId.Value,
                    solutionUniqueName,
                    currentProfileId,
                    161,
                    cancellationToken).ConfigureAwait(false);
                if (added)
                {
                    changeCount++;
                }
            }
        }

        foreach (var item in mobileOfflineProfileItems.OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var parentLogicalName = NormalizeLogicalName(GetArtifactProperty(item, ArtifactPropertyKeys.ParentMobileOfflineProfileLogicalName));
            if (string.IsNullOrWhiteSpace(parentLogicalName)
                || !profileIdsByLogicalName.TryGetValue(parentLogicalName, out var parentProfileId))
            {
                continue;
            }

            var entityLogicalName = NormalizeLogicalName(GetArtifactProperty(item, ArtifactPropertyKeys.EntityLogicalName));
            if (string.IsNullOrWhiteSpace(entityLogicalName))
            {
                continue;
            }

            var itemName = item.DisplayName ?? entityLogicalName;

            var current = await FindSingleAsync(
                httpClient,
                credential,
                $"mobileofflineprofileitems?$select=mobileofflineprofileitemid,name,selectedentitytypecode,recorddistributioncriteria,recordsownedbyme,recordsownedbymyteam,recordsownedbymybusinessunit,profileitementityfilter&$filter=name eq '{EscapeODataLiteral(itemName)}' and selectedentitytypecode eq '{EscapeODataLiteral(entityLogicalName)}'",
                cancellationToken).ConfigureAwait(false);

            var payload = new JsonObject
            {
                ["name"] = itemName,
                ["selectedentitytypecode"] = entityLogicalName,
                ["regardingobjectid@odata.bind"] = $"/mobileofflineprofiles({parentProfileId:D})"
            };
            AddStringProperty(payload, "recorddistributioncriteria", GetArtifactProperty(item, ArtifactPropertyKeys.RecordDistributionCriteria));
            AddBooleanProperty(payload, "recordsownedbyme", GetArtifactProperty(item, ArtifactPropertyKeys.RecordsOwnedByMe));
            AddBooleanProperty(payload, "recordsownedbymyteam", GetArtifactProperty(item, ArtifactPropertyKeys.RecordsOwnedByMyTeam));
            AddBooleanProperty(payload, "recordsownedbymybusinessunit", GetArtifactProperty(item, ArtifactPropertyKeys.RecordsOwnedByMyBusinessUnit));
            AddStringProperty(payload, "profileitementityfilter", GetArtifactProperty(item, ArtifactPropertyKeys.ProfileItemEntityFilter));

            var needsUpdate = current is null
                || !string.Equals(NormalizeLogicalName(GetString(current, "selectedentitytypecode")) ?? string.Empty, entityLogicalName, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(GetString(current, "recorddistributioncriteria") ?? string.Empty, GetArtifactProperty(item, ArtifactPropertyKeys.RecordDistributionCriteria) ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(NormalizeBoolean(GetString(current, "recordsownedbyme")) ?? string.Empty, NormalizeBoolean(GetArtifactProperty(item, ArtifactPropertyKeys.RecordsOwnedByMe)) ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(NormalizeBoolean(GetString(current, "recordsownedbymyteam")) ?? string.Empty, NormalizeBoolean(GetArtifactProperty(item, ArtifactPropertyKeys.RecordsOwnedByMyTeam)) ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(NormalizeBoolean(GetString(current, "recordsownedbymybusinessunit")) ?? string.Empty, NormalizeBoolean(GetArtifactProperty(item, ArtifactPropertyKeys.RecordsOwnedByMyBusinessUnit)) ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(GetString(current, "profileitementityfilter") ?? string.Empty, GetArtifactProperty(item, ArtifactPropertyKeys.ProfileItemEntityFilter) ?? string.Empty, StringComparison.Ordinal);

            if (!needsUpdate)
            {
                continue;
            }

            if (GetGuid(current, "mobileofflineprofileitemid") is Guid currentItemId)
            {
                await SendJsonAsync(httpClient, credential, HttpMethod.Patch, $"mobileofflineprofileitems({currentItemId:D})", payload, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await SendJsonAsync(httpClient, credential, HttpMethod.Post, "mobileofflineprofileitems", payload, cancellationToken).ConfigureAwait(false);
            }

            changeCount++;
        }

        return changeCount;
    }

    private async Task<int> ApplyConnectionRolesAsync(
        HttpClient httpClient,
        TokenCredential credential,
        Guid? solutionId,
        string solutionUniqueName,
        IReadOnlyList<FamilyArtifact> connectionRoles,
        CancellationToken cancellationToken)
    {
        var changeCount = 0;

        foreach (var connectionRole in connectionRoles.OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var logicalName = NormalizeLogicalName(connectionRole.LogicalName);
            var name = connectionRole.DisplayName ?? logicalName;
            if (string.IsNullOrWhiteSpace(logicalName) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var current = await FindSingleAsync(
                httpClient,
                credential,
                $"connectionroles?$select=connectionroleid,name,description,category&$filter=name eq '{EscapeODataLiteral(name)}'",
                cancellationToken).ConfigureAwait(false);

            var payload = new JsonObject
            {
                ["name"] = name
            };
            AddStringProperty(payload, "description", GetArtifactProperty(connectionRole, ArtifactPropertyKeys.Description));
            AddIntegerProperty(payload, "category", GetArtifactProperty(connectionRole, ArtifactPropertyKeys.Category));

            var needsUpdate = current is null
                || !string.Equals(GetString(current, "name") ?? string.Empty, name, StringComparison.Ordinal)
                || !string.Equals(GetString(current, "description") ?? string.Empty, GetArtifactProperty(connectionRole, ArtifactPropertyKeys.Description) ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(GetString(current, "category") ?? string.Empty, GetArtifactProperty(connectionRole, ArtifactPropertyKeys.Category) ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            if (needsUpdate)
            {
                if (GetGuid(current, "connectionroleid") is Guid currentConnectionRoleId)
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Patch, $"connectionroles({currentConnectionRoleId:D})", payload, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Post, "connectionroles", payload, cancellationToken).ConfigureAwait(false);
                }

                changeCount++;
                current = await FindSingleAsync(
                    httpClient,
                    credential,
                    $"connectionroles?$select=connectionroleid,name,description,category&$filter=name eq '{EscapeODataLiteral(name)}'",
                    cancellationToken).ConfigureAwait(false);
            }

            if (solutionId.HasValue && GetGuid(current, "connectionroleid") is Guid currentConnectionRoleIdForSolution)
            {
                var added = await EnsureSolutionComponentAsync(
                    httpClient,
                    credential,
                    solutionId.Value,
                    solutionUniqueName,
                    currentConnectionRoleIdForSolution,
                    63,
                    cancellationToken).ConfigureAwait(false);
                if (added)
                {
                    changeCount++;
                }
            }
        }

        return changeCount;
    }

    private async Task<int> ApplyServiceEndpointsAsync(
        HttpClient httpClient,
        TokenCredential credential,
        Guid? solutionId,
        string solutionUniqueName,
        IReadOnlyList<FamilyArtifact> serviceEndpoints,
        CancellationToken cancellationToken)
    {
        var changeCount = 0;

        foreach (var serviceEndpoint in serviceEndpoints.OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var logicalName = NormalizeLogicalName(serviceEndpoint.LogicalName);
            var name = GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.Name)
                ?? serviceEndpoint.DisplayName
                ?? logicalName;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var current = await FindSingleAsync(
                httpClient,
                credential,
                BuildServiceEndpointLookupQuery(logicalName, name),
                cancellationToken).ConfigureAwait(false);

            var payload = new JsonObject
            {
                ["name"] = name
            };
            AddStringProperty(payload, "description", GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.Description));
            AddIntegerProperty(payload, "contract", GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.Contract));
            AddIntegerProperty(payload, "connectionmode", GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.ConnectionMode));
            AddIntegerProperty(payload, "authtype", GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.AuthType));
            AddStringProperty(payload, "namespaceaddress", GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.NamespaceAddress));
            AddStringProperty(payload, "path", GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.EndpointPath));
            AddStringProperty(payload, "url", GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.Url));
            AddIntegerProperty(payload, "messageformat", GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.MessageFormat));
            AddIntegerProperty(payload, "messagecharset", ResolveServiceEndpointMessageCharsetOptionValue(GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.MessageCharset)));
            AddStringProperty(payload, "introducedversion", GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.IntroducedVersion));

            var needsUpdate = current is null
                || !string.Equals(GetString(current, "name") ?? string.Empty, name, StringComparison.Ordinal)
                || !string.Equals(GetString(current, "description") ?? string.Empty, GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.Description) ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(GetString(current, "contract") ?? string.Empty, GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.Contract) ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(GetString(current, "connectionmode") ?? string.Empty, GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.ConnectionMode) ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(GetString(current, "authtype") ?? string.Empty, GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.AuthType) ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(GetString(current, "namespaceaddress") ?? string.Empty, GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.NamespaceAddress) ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(GetString(current, "path") ?? string.Empty, GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.EndpointPath) ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(GetString(current, "url") ?? string.Empty, GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.Url) ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(GetString(current, "messageformat") ?? string.Empty, GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.MessageFormat) ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(NormalizeServiceEndpointMessageCharset(GetString(current, "messagecharset")) ?? string.Empty, NormalizeServiceEndpointMessageCharset(GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.MessageCharset)) ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(GetString(current, "introducedversion") ?? string.Empty, GetArtifactProperty(serviceEndpoint, ArtifactPropertyKeys.IntroducedVersion) ?? string.Empty, StringComparison.Ordinal);

            if (needsUpdate)
            {
                if (GetGuid(current, "serviceendpointid") is Guid serviceEndpointId)
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Patch, $"serviceendpoints({serviceEndpointId:D})", payload, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Post, "serviceendpoints", payload, cancellationToken).ConfigureAwait(false);
                }

                changeCount++;
                current = await FindSingleAsync(
                    httpClient,
                    credential,
                    BuildServiceEndpointLookupQuery(logicalName, name),
                    cancellationToken).ConfigureAwait(false);
            }

            if (solutionId.HasValue && GetGuid(current, "serviceendpointid") is Guid currentServiceEndpointId)
            {
                var added = await EnsureSolutionComponentAsync(
                    httpClient,
                    credential,
                    solutionId.Value,
                    solutionUniqueName,
                    currentServiceEndpointId,
                    95,
                    cancellationToken).ConfigureAwait(false);
                if (added)
                {
                    changeCount++;
                }
            }
        }

        return changeCount;
    }

    private async Task<int> ApplyConnectorsAsync(
        HttpClient httpClient,
        TokenCredential credential,
        Guid? solutionId,
        string solutionUniqueName,
        IReadOnlyList<FamilyArtifact> connectors,
        CancellationToken cancellationToken)
    {
        var changeCount = 0;

        foreach (var connector in connectors.OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var logicalName = NormalizeLogicalName(connector.LogicalName);
            var connectorInternalId = NormalizeLogicalName(GetArtifactProperty(connector, ArtifactPropertyKeys.ConnectorInternalId));
            var name = GetArtifactProperty(connector, ArtifactPropertyKeys.Name)
                ?? connector.DisplayName
                ?? logicalName;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var current = await FindSingleAsync(
                httpClient,
                credential,
                BuildConnectorLookupQuery(logicalName, connectorInternalId, name),
                cancellationToken).ConfigureAwait(false);

            var payload = new JsonObject
            {
                ["name"] = name
            };
            AddStringProperty(payload, "displayname", connector.DisplayName);
            AddStringProperty(payload, "description", GetArtifactProperty(connector, ArtifactPropertyKeys.Description));
            AddStringProperty(payload, "connectorinternalid", connectorInternalId);
            AddIntegerProperty(payload, "connectortype", GetArtifactProperty(connector, ArtifactPropertyKeys.ConnectorType));
            AddStringProperty(payload, "capabilities", ResolveConnectorCapabilitiesPayload(GetArtifactProperty(connector, ArtifactPropertyKeys.CapabilitiesJson)));
            AddStringProperty(payload, "introducedversion", GetArtifactProperty(connector, ArtifactPropertyKeys.IntroducedVersion));

            var desiredCapabilitiesJson = NormalizeCapabilitiesComparisonJson(GetArtifactProperty(connector, ArtifactPropertyKeys.CapabilitiesJson));
            var currentCapabilitiesJson = NormalizeCapabilitiesComparisonJson(GetProperty(current, "capabilities"));
            var needsUpdate = current is null
                || !string.Equals(GetString(current, "name") ?? string.Empty, name, StringComparison.Ordinal)
                || !string.Equals(GetString(current, "displayname") ?? string.Empty, connector.DisplayName ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(GetString(current, "description") ?? string.Empty, GetArtifactProperty(connector, ArtifactPropertyKeys.Description) ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(NormalizeLogicalName(GetString(current, "connectorinternalid")) ?? string.Empty, connectorInternalId ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(GetString(current, "connectortype") ?? string.Empty, GetArtifactProperty(connector, ArtifactPropertyKeys.ConnectorType) ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(currentCapabilitiesJson ?? string.Empty, desiredCapabilitiesJson ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(GetString(current, "introducedversion") ?? string.Empty, GetArtifactProperty(connector, ArtifactPropertyKeys.IntroducedVersion) ?? string.Empty, StringComparison.Ordinal);

            if (needsUpdate)
            {
                if (GetGuid(current, "connectorid") is Guid connectorId)
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Patch, $"connectors({connectorId:D})", payload, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Post, "connectors", payload, cancellationToken).ConfigureAwait(false);
                }

                changeCount++;
                current = await FindSingleAsync(
                    httpClient,
                    credential,
                    BuildConnectorLookupQuery(logicalName, connectorInternalId, name),
                    cancellationToken).ConfigureAwait(false);
            }

            if (solutionId.HasValue && GetGuid(current, "connectorid") is Guid currentConnectorId)
            {
                var added = await EnsureSolutionComponentAsync(
                    httpClient,
                    credential,
                    solutionId.Value,
                    solutionUniqueName,
                    currentConnectorId,
                    372,
                    cancellationToken).ConfigureAwait(false);
                if (added)
                {
                    changeCount++;
                }
            }
        }

        return changeCount;
    }

    private async Task<int> ApplyPluginRegistrationFamiliesAsync(
        HttpClient httpClient,
        TokenCredential credential,
        Guid? solutionId,
        string solutionUniqueName,
        IReadOnlyList<FamilyArtifact> pluginAssemblies,
        IReadOnlyList<FamilyArtifact> pluginTypes,
        IReadOnlyList<FamilyArtifact> pluginSteps,
        IReadOnlyList<FamilyArtifact> pluginStepImages,
        ICollection<CompilerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var changeCount = 0;
        var assemblyIdsByFullName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var pluginTypeIdsByLogicalName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var pluginStepIdsByLogicalName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var sdkMessageIdsByName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var sdkMessageFilterIdsByMessageAndEntity = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in pluginAssemblies.OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var fullName = GetArtifactProperty(assembly, ArtifactPropertyKeys.AssemblyFullName)
                ?? assembly.LogicalName;
            if (string.IsNullOrWhiteSpace(fullName))
            {
                continue;
            }

            var identity = ParseAssemblyIdentity(fullName);
            var assemblyName = identity.Name ?? assembly.DisplayName ?? fullName;
            var assemblyBinaryPath = ResolvePluginAssemblyBinaryPath(assembly);
            if (string.IsNullOrWhiteSpace(assemblyBinaryPath) || !File.Exists(assemblyBinaryPath))
            {
                diagnostics.Add(new CompilerDiagnostic(
                    "apply-plugin-assembly-missing-binary",
                    DiagnosticSeverity.Warning,
                    $"Skipped plug-in assembly '{assemblyName}' because no assembly binary could be materialized for live apply.",
                    assembly.SourcePath));
                continue;
            }

            var current = await FindSingleAsync(
                httpClient,
                credential,
                BuildPluginAssemblyLookupQuery(identity, assemblyName),
                cancellationToken).ConfigureAwait(false);

            var payload = new JsonObject
            {
                ["name"] = assemblyName,
                ["content"] = Convert.ToBase64String(File.ReadAllBytes(assemblyBinaryPath)),
                ["path"] = Path.GetFileName(assemblyBinaryPath),
                ["culture"] = identity.Culture ?? "neutral",
                ["publickeytoken"] = identity.PublicKeyToken ?? "null",
                ["version"] = identity.Version ?? "1.0.0.0"
            };
            AddIntegerProperty(payload, "isolationmode", GetArtifactProperty(assembly, ArtifactPropertyKeys.IsolationMode), 2);
            AddIntegerProperty(payload, "sourcetype", GetArtifactProperty(assembly, ArtifactPropertyKeys.SourceType), 0);
            AddStringProperty(payload, "introducedversion", GetArtifactProperty(assembly, ArtifactPropertyKeys.IntroducedVersion));

            var needsUpdate = current is null
                || !string.Equals(GetString(current, "name") ?? string.Empty, assemblyName, StringComparison.Ordinal)
                || !string.Equals(GetString(current, "path") ?? string.Empty, Path.GetFileName(assemblyBinaryPath), StringComparison.Ordinal)
                || !string.Equals(GetString(current, "isolationmode") ?? string.Empty, GetArtifactProperty(assembly, ArtifactPropertyKeys.IsolationMode) ?? "2", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(GetString(current, "sourcetype") ?? string.Empty, GetArtifactProperty(assembly, ArtifactPropertyKeys.SourceType) ?? "0", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(GetString(current, "introducedversion") ?? string.Empty, GetArtifactProperty(assembly, ArtifactPropertyKeys.IntroducedVersion) ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(GetString(current, "version") ?? string.Empty, identity.Version ?? "1.0.0.0", StringComparison.Ordinal)
                || !string.Equals(GetString(current, "culture") ?? string.Empty, identity.Culture ?? "neutral", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(GetString(current, "publickeytoken") ?? string.Empty, identity.PublicKeyToken ?? "null", StringComparison.OrdinalIgnoreCase);

            var assemblyCreated = false;
            Guid? currentAssemblyId = GetGuid(current, "pluginassemblyid");
            if (needsUpdate)
            {
                if (currentAssemblyId is Guid pluginAssemblyId)
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Patch, $"pluginassemblies({pluginAssemblyId:D})", payload, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    currentAssemblyId = await SendCreateJsonAsync(httpClient, credential, "pluginassemblies", payload, cancellationToken).ConfigureAwait(false);
                    assemblyCreated = true;
                }

                current = currentAssemblyId.HasValue
                    ? await FindSingleAsync(
                        httpClient,
                        credential,
                        $"pluginassemblies({currentAssemblyId.Value:D})?$select=pluginassemblyid,name,path,isolationmode,sourcetype,introducedversion,version,culture,publickeytoken",
                        cancellationToken).ConfigureAwait(false)
                    : await FindSingleAsync(
                        httpClient,
                        credential,
                        BuildPluginAssemblyLookupQuery(identity, assemblyName),
                        cancellationToken).ConfigureAwait(false);
            }

            currentAssemblyId ??= GetGuid(current, "pluginassemblyid");
            if (currentAssemblyId is not Guid resolvedAssemblyId)
            {
                diagnostics.Add(new CompilerDiagnostic(
                    "apply-plugin-assembly-missing-id",
                    DiagnosticSeverity.Warning,
                    $"Skipped plug-in assembly '{assemblyName}' because Dataverse did not return a pluginassemblyid after metadata creation/update.",
                    assembly.SourcePath));
                continue;
            }

            var expectedTypeNames = pluginTypes
                .Where(artifact => string.Equals(GetArtifactProperty(artifact, ArtifactPropertyKeys.AssemblyFullName), fullName, StringComparison.OrdinalIgnoreCase))
                .Select(artifact => NormalizeLogicalName(artifact.LogicalName))
                .Where(logicalName => !string.IsNullOrWhiteSpace(logicalName))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var existingTypeNames = await GetPluginTypeNamesAsync(httpClient, credential, resolvedAssemblyId, cancellationToken).ConfigureAwait(false);
            var missingExpectedTypes = expectedTypeNames
                .Where(typeName => !existingTypeNames.Contains(typeName, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            var needsBinaryPush = assemblyCreated || missingExpectedTypes.Length > 0;

            if (needsBinaryPush)
            {
                var environmentUrl = BuildEnvironmentUrl();
                if (!TryRunPacPluginPush(resolvedAssemblyId, assemblyBinaryPath, environmentUrl, diagnostics))
                {
                    diagnostics.Add(new CompilerDiagnostic(
                        "apply-plugin-assembly-push-failed",
                        DiagnosticSeverity.Error,
                        $"Skipped plug-in assembly '{assemblyName}' because PAC plug-in push failed.",
                        assemblyBinaryPath));
                    continue;
                }

                changeCount++;

                if (expectedTypeNames.Length > 0)
                {
                    await WaitForPluginTypesAsync(
                        httpClient,
                        credential,
                        resolvedAssemblyId,
                        expectedTypeNames,
                        diagnostics,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            else if (needsUpdate)
            {
                changeCount++;
            }

            assemblyIdsByFullName[fullName] = resolvedAssemblyId;
            if (solutionId.HasValue)
            {
                var added = await EnsureSolutionComponentAsync(
                    httpClient,
                    credential,
                    solutionId.Value,
                    solutionUniqueName,
                    resolvedAssemblyId,
                    91,
                    cancellationToken).ConfigureAwait(false);
                if (added)
                {
                    changeCount++;
                }
            }
        }

        foreach (var pluginType in pluginTypes.OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var logicalName = pluginType.LogicalName;
            var assemblyFullName = GetArtifactProperty(pluginType, ArtifactPropertyKeys.AssemblyFullName);
            if (string.IsNullOrWhiteSpace(logicalName) || string.IsNullOrWhiteSpace(assemblyFullName))
            {
                continue;
            }

            if (!assemblyIdsByFullName.TryGetValue(assemblyFullName, out var pluginAssemblyId))
            {
                var assemblyIdentity = ParseAssemblyIdentity(assemblyFullName);
                var currentAssembly = await FindSingleAsync(
                    httpClient,
                    credential,
                    BuildPluginAssemblyLookupQuery(assemblyIdentity, assemblyIdentity.Name ?? assemblyFullName),
                    cancellationToken).ConfigureAwait(false);
                if (GetGuid(currentAssembly, "pluginassemblyid") is Guid resolvedAssemblyId)
                {
                    pluginAssemblyId = resolvedAssemblyId;
                    assemblyIdsByFullName[assemblyFullName] = resolvedAssemblyId;
                }
                else
                {
                    continue;
                }
            }

            var current = await FindPluginTypeAsync(httpClient, credential, logicalName, pluginAssemblyId, cancellationToken).ConfigureAwait(false);
            var friendlyName = GetArtifactProperty(pluginType, ArtifactPropertyKeys.FriendlyName) ?? pluginType.DisplayName ?? logicalName;
            var description = GetArtifactProperty(pluginType, ArtifactPropertyKeys.Description);
            var workflowActivityGroupName = GetArtifactProperty(pluginType, ArtifactPropertyKeys.WorkflowActivityGroupName);
            var payload = new JsonObject
            {
                ["typename"] = logicalName,
                ["name"] = logicalName,
                ["friendlyname"] = friendlyName,
                ["pluginassemblyid@odata.bind"] = $"/pluginassemblies({pluginAssemblyId:D})"
            };
            AddStringProperty(payload, "description", description);
            AddStringProperty(payload, "workflowactivitygroupname", workflowActivityGroupName);

            var needsUpdate = current is null
                || !string.Equals(GetString(current, "friendlyname") ?? string.Empty, friendlyName, StringComparison.Ordinal)
                || !string.Equals(GetString(current, "description") ?? string.Empty, description ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(GetString(current, "workflowactivitygroupname") ?? string.Empty, workflowActivityGroupName ?? string.Empty, StringComparison.Ordinal);

            if (needsUpdate)
            {
                if (GetGuid(current, "plugintypeid") is Guid pluginTypeId)
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Patch, $"plugintypes({pluginTypeId:D})", payload, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Post, "plugintypes", payload, cancellationToken).ConfigureAwait(false);
                }

                changeCount++;
                current = await FindPluginTypeAsync(httpClient, credential, logicalName, pluginAssemblyId, cancellationToken).ConfigureAwait(false);
            }

            if (GetGuid(current, "plugintypeid") is not Guid currentPluginTypeId)
            {
                continue;
            }

            pluginTypeIdsByLogicalName[NormalizeLogicalName(logicalName)!] = currentPluginTypeId;
        }

        foreach (var pluginStep in pluginSteps.OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var handlerPluginTypeName = NormalizeLogicalName(GetArtifactProperty(pluginStep, ArtifactPropertyKeys.HandlerPluginTypeName));
            if (string.IsNullOrWhiteSpace(handlerPluginTypeName))
            {
                var inferredHandlerTypeName = pluginStep.LogicalName.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(inferredHandlerTypeName)
                    && !string.Equals(inferredHandlerTypeName, "handler", StringComparison.OrdinalIgnoreCase))
                {
                    handlerPluginTypeName = NormalizeLogicalName(inferredHandlerTypeName);
                }
            }

            var stepName = pluginStep.DisplayName;
            var messageName = GetArtifactProperty(pluginStep, ArtifactPropertyKeys.MessageName);
            var sdkMessageIdValue = GetArtifactProperty(pluginStep, ArtifactPropertyKeys.SdkMessageId);
            var primaryEntity = NormalizeLogicalName(GetArtifactProperty(pluginStep, ArtifactPropertyKeys.PrimaryEntity));
            if (string.IsNullOrWhiteSpace(handlerPluginTypeName)
                || string.IsNullOrWhiteSpace(stepName)
                || (string.IsNullOrWhiteSpace(messageName) && !Guid.TryParse(sdkMessageIdValue, out _)))
            {
                continue;
            }

            if (!pluginTypeIdsByLogicalName.TryGetValue(handlerPluginTypeName, out var pluginTypeId))
            {
                var currentPluginType = await FindSingleAsync(
                    httpClient,
                    credential,
                    $"plugintypes?$select=plugintypeid,typename&$filter=typename eq '{EscapeODataLiteral(handlerPluginTypeName)}'",
                    cancellationToken).ConfigureAwait(false);
                if (GetGuid(currentPluginType, "plugintypeid") is Guid resolvedPluginTypeId)
                {
                    pluginTypeId = resolvedPluginTypeId;
                    pluginTypeIdsByLogicalName[handlerPluginTypeName] = resolvedPluginTypeId;
                }
                else
                {
                    continue;
                }
            }

            Guid sdkMessageId;
            if (!string.IsNullOrWhiteSpace(messageName)
                && sdkMessageIdsByName.TryGetValue(messageName, out sdkMessageId))
            {
            }
            else if (Guid.TryParse(sdkMessageIdValue, out var resolvedSdkMessageId))
            {
                sdkMessageId = resolvedSdkMessageId;
                if (!string.IsNullOrWhiteSpace(messageName))
                {
                    sdkMessageIdsByName[messageName] = resolvedSdkMessageId;
                }
            }
            else
            {
                var nonEmptyMessageName = messageName!;
                var message = await FindSingleAsync(
                    httpClient,
                    credential,
                    $"sdkmessages?$select=sdkmessageid,name&$filter=name eq '{EscapeODataLiteral(nonEmptyMessageName)}'",
                    cancellationToken).ConfigureAwait(false);
                if (GetGuid(message, "sdkmessageid") is not Guid resolvedMessageId)
                {
                    continue;
                }

                sdkMessageId = resolvedMessageId;
                sdkMessageIdsByName[nonEmptyMessageName] = resolvedMessageId;
            }

            Guid? sdkMessageFilterId = null;
            if (!string.IsNullOrWhiteSpace(primaryEntity))
            {
                var filterCacheKey = $"{messageName}|{primaryEntity}";
                if (!sdkMessageFilterIdsByMessageAndEntity.TryGetValue(filterCacheKey, out var cachedFilterId))
                {
                    var filter = await FindSingleAsync(
                        httpClient,
                        credential,
                        $"sdkmessagefilters?$select=sdkmessagefilterid,primaryobjecttypecode,_sdkmessageid_value&$filter=_sdkmessageid_value eq {FormatGuid(sdkMessageId)} and primaryobjecttypecode eq '{EscapeODataLiteral(primaryEntity)}'",
                        cancellationToken).ConfigureAwait(false);
                    if (GetGuid(filter, "sdkmessagefilterid") is Guid resolvedFilterId)
                    {
                        cachedFilterId = resolvedFilterId;
                        sdkMessageFilterIdsByMessageAndEntity[filterCacheKey] = resolvedFilterId;
                    }
                }

                if (cachedFilterId != Guid.Empty)
                {
                    sdkMessageFilterId = cachedFilterId;
                }
            }

            var current = await FindPluginStepAsync(httpClient, credential, stepName, pluginTypeId, sdkMessageId, sdkMessageFilterId, cancellationToken).ConfigureAwait(false);
            var payload = new JsonObject
            {
                ["name"] = stepName,
                ["sdkmessageid@odata.bind"] = $"/sdkmessages({sdkMessageId:D})",
                ["eventhandler_plugintype@odata.bind"] = $"/plugintypes({pluginTypeId:D})"
            };
            if (sdkMessageFilterId.HasValue)
            {
                payload["sdkmessagefilterid@odata.bind"] = $"/sdkmessagefilters({sdkMessageFilterId.Value:D})";
            }

            AddStringProperty(payload, "description", GetArtifactProperty(pluginStep, ArtifactPropertyKeys.Description));
            AddIntegerProperty(payload, "stage", GetArtifactProperty(pluginStep, ArtifactPropertyKeys.Stage));
            AddIntegerProperty(payload, "mode", GetArtifactProperty(pluginStep, ArtifactPropertyKeys.Mode));
            AddIntegerProperty(payload, "rank", GetArtifactProperty(pluginStep, ArtifactPropertyKeys.Rank));
            AddIntegerProperty(payload, "supporteddeployment", GetArtifactProperty(pluginStep, ArtifactPropertyKeys.SupportedDeployment));
            AddStringProperty(payload, "filteringattributes", GetArtifactProperty(pluginStep, ArtifactPropertyKeys.FilteringAttributes));
            AddStringProperty(payload, "introducedversion", GetArtifactProperty(pluginStep, ArtifactPropertyKeys.IntroducedVersion));

            var needsUpdate = current is null
                || !string.Equals(GetString(current, "description") ?? string.Empty, GetArtifactProperty(pluginStep, ArtifactPropertyKeys.Description) ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(GetString(current, "stage") ?? string.Empty, GetArtifactProperty(pluginStep, ArtifactPropertyKeys.Stage) ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(GetString(current, "mode") ?? string.Empty, GetArtifactProperty(pluginStep, ArtifactPropertyKeys.Mode) ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(GetString(current, "rank") ?? string.Empty, GetArtifactProperty(pluginStep, ArtifactPropertyKeys.Rank) ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(GetString(current, "supporteddeployment") ?? string.Empty, GetArtifactProperty(pluginStep, ArtifactPropertyKeys.SupportedDeployment) ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(NormalizeAttributeList(GetString(current, "filteringattributes")) ?? string.Empty, NormalizeAttributeList(GetArtifactProperty(pluginStep, ArtifactPropertyKeys.FilteringAttributes)) ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            if (needsUpdate)
            {
                if (GetGuid(current, "sdkmessageprocessingstepid") is Guid pluginStepId)
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Patch, $"sdkmessageprocessingsteps({pluginStepId:D})", payload, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Post, "sdkmessageprocessingsteps", payload, cancellationToken).ConfigureAwait(false);
                }

                changeCount++;
                current = await FindPluginStepAsync(httpClient, credential, stepName, pluginTypeId, sdkMessageId, sdkMessageFilterId, cancellationToken).ConfigureAwait(false);
            }

            if (GetGuid(current, "sdkmessageprocessingstepid") is not Guid currentPluginStepId)
            {
                continue;
            }

            pluginStepIdsByLogicalName[pluginStep.LogicalName] = currentPluginStepId;
            if (solutionId.HasValue)
            {
                var added = await EnsureSolutionComponentAsync(
                    httpClient,
                    credential,
                    solutionId.Value,
                    solutionUniqueName,
                    currentPluginStepId,
                    92,
                    cancellationToken).ConfigureAwait(false);
                if (added)
                {
                    changeCount++;
                }
            }
        }

        foreach (var pluginStepImage in pluginStepImages.OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var imageName = pluginStepImage.DisplayName;
            var parentStepLogicalName = GetArtifactProperty(pluginStepImage, ArtifactPropertyKeys.ParentPluginStepLogicalName);
            if (string.IsNullOrWhiteSpace(imageName)
                || string.IsNullOrWhiteSpace(parentStepLogicalName)
                || !pluginStepIdsByLogicalName.TryGetValue(parentStepLogicalName, out var parentStepId))
            {
                continue;
            }

            var current = await FindSingleAsync(
                httpClient,
                credential,
                $"sdkmessageprocessingstepimages?$select=sdkmessageprocessingstepimageid,name,description,entityalias,imagetype,messagepropertyname,attributes,_sdkmessageprocessingstepid_value&$filter=_sdkmessageprocessingstepid_value eq {FormatGuid(parentStepId)} and name eq '{EscapeODataLiteral(imageName)}'",
                cancellationToken).ConfigureAwait(false);

            var payload = new JsonObject
            {
                ["name"] = imageName,
                ["sdkmessageprocessingstepid@odata.bind"] = $"/sdkmessageprocessingsteps({parentStepId:D})"
            };
            AddStringProperty(payload, "description", GetArtifactProperty(pluginStepImage, ArtifactPropertyKeys.Description));
            AddStringProperty(payload, "entityalias", GetArtifactProperty(pluginStepImage, ArtifactPropertyKeys.EntityAlias));
            AddIntegerProperty(payload, "imagetype", GetArtifactProperty(pluginStepImage, ArtifactPropertyKeys.ImageType));
            AddStringProperty(payload, "messagepropertyname", GetArtifactProperty(pluginStepImage, ArtifactPropertyKeys.MessagePropertyName));
            AddStringProperty(payload, "attributes", GetArtifactProperty(pluginStepImage, ArtifactPropertyKeys.SelectedAttributes));
            AddStringProperty(payload, "introducedversion", GetArtifactProperty(pluginStepImage, ArtifactPropertyKeys.IntroducedVersion));

            var needsUpdate = current is null
                || !string.Equals(GetString(current, "description") ?? string.Empty, GetArtifactProperty(pluginStepImage, ArtifactPropertyKeys.Description) ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(NormalizeLogicalName(GetString(current, "entityalias")) ?? string.Empty, NormalizeLogicalName(GetArtifactProperty(pluginStepImage, ArtifactPropertyKeys.EntityAlias)) ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(GetString(current, "imagetype") ?? string.Empty, GetArtifactProperty(pluginStepImage, ArtifactPropertyKeys.ImageType) ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(GetString(current, "messagepropertyname") ?? string.Empty, GetArtifactProperty(pluginStepImage, ArtifactPropertyKeys.MessagePropertyName) ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(NormalizeAttributeList(GetString(current, "attributes")) ?? string.Empty, NormalizeAttributeList(GetArtifactProperty(pluginStepImage, ArtifactPropertyKeys.SelectedAttributes)) ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            if (needsUpdate)
            {
                if (GetGuid(current, "sdkmessageprocessingstepimageid") is Guid pluginStepImageId)
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Patch, $"sdkmessageprocessingstepimages({pluginStepImageId:D})", payload, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await SendJsonAsync(httpClient, credential, HttpMethod.Post, "sdkmessageprocessingstepimages", payload, cancellationToken).ConfigureAwait(false);
                }

                changeCount++;
                current = await FindSingleAsync(
                    httpClient,
                    credential,
                    $"sdkmessageprocessingstepimages?$select=sdkmessageprocessingstepimageid,name,_sdkmessageprocessingstepid_value&$filter=_sdkmessageprocessingstepid_value eq {FormatGuid(parentStepId)} and name eq '{EscapeODataLiteral(imageName)}'",
                    cancellationToken).ConfigureAwait(false);
            }

        }

        return changeCount;
    }

    private async Task<Guid?> EnsureSolutionShellAsync(
        HttpClient httpClient,
        TokenCredential credential,
        CanonicalSolution model,
        ICollection<CompilerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var existingSolutionId = await ReadSolutionIdAsync(httpClient, credential, model.Identity.UniqueName, cancellationToken).ConfigureAwait(false);
        if (existingSolutionId.HasValue)
        {
            return existingSolutionId;
        }

        var publisherId = await EnsurePublisherAsync(httpClient, credential, model.Publisher, cancellationToken).ConfigureAwait(false);
        if (!publisherId.HasValue)
        {
            return null;
        }

        var solutionPayload = new JsonObject
        {
            ["uniquename"] = model.Identity.UniqueName,
            ["friendlyname"] = model.Identity.DisplayName,
            ["version"] = model.Identity.Version,
            ["publisherid@odata.bind"] = $"/publishers({publisherId.Value:D})"
        };

        await SendJsonAsync(httpClient, credential, HttpMethod.Post, "solutions", solutionPayload, cancellationToken).ConfigureAwait(false);

        var createdSolutionId = await ReadSolutionIdAsync(httpClient, credential, model.Identity.UniqueName, cancellationToken).ConfigureAwait(false);
        if (createdSolutionId.HasValue)
        {
            diagnostics.Add(new CompilerDiagnostic(
                "apply-created-solution-shell",
                DiagnosticSeverity.Info,
                $"Created live solution shell '{model.Identity.UniqueName}' before applying hybrid metadata families.",
                _serviceRoot?.ToString()));
        }

        return createdSolutionId;
    }

    private async Task<Guid?> EnsurePublisherAsync(
        HttpClient httpClient,
        TokenCredential credential,
        PublisherDefinition publisher,
        CancellationToken cancellationToken)
    {
        var existingPublisher = await FindSingleAsync(
            httpClient,
            credential,
            $"publishers?$select=publisherid,uniquename&$filter=uniquename eq '{EscapeODataLiteral(publisher.UniqueName)}'",
            cancellationToken).ConfigureAwait(false);
        if (GetGuid(existingPublisher, "publisherid") is Guid existingPublisherId)
        {
            return existingPublisherId;
        }

        var customizationPrefix = string.IsNullOrWhiteSpace(publisher.CustomizationPrefix)
            ? publisher.Prefix
            : publisher.CustomizationPrefix;
        var optionValuePrefix = await ResolvePublisherOptionValuePrefixAsync(httpClient, credential, publisher.UniqueName, cancellationToken).ConfigureAwait(false);
        var publisherPayload = new JsonObject
        {
            ["uniquename"] = publisher.UniqueName,
            ["friendlyname"] = publisher.DisplayName,
            ["customizationprefix"] = customizationPrefix,
            ["customizationoptionvalueprefix"] = optionValuePrefix
        };

        await SendJsonAsync(httpClient, credential, HttpMethod.Post, "publishers", publisherPayload, cancellationToken).ConfigureAwait(false);

        var createdPublisher = await FindSingleAsync(
            httpClient,
            credential,
            $"publishers?$select=publisherid,uniquename&$filter=uniquename eq '{EscapeODataLiteral(publisher.UniqueName)}'",
            cancellationToken).ConfigureAwait(false);
        return GetGuid(createdPublisher, "publisherid");
    }

    private async Task<int> ResolvePublisherOptionValuePrefixAsync(
        HttpClient httpClient,
        TokenCredential credential,
        string publisherUniqueName,
        CancellationToken cancellationToken)
    {
        var seed = 10000 + Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(publisherUniqueName)) % 80000;
        for (var offset = 0; offset < 1000; offset++)
        {
            var candidate = seed + offset;
            if (candidate > 99999)
            {
                candidate = 10000 + (candidate - 100000);
            }

            var existingPublisher = await FindSingleAsync(
                httpClient,
                credential,
                $"publishers?$select=publisherid,uniquename,customizationoptionvalueprefix&$filter=customizationoptionvalueprefix eq {candidate}",
                cancellationToken).ConfigureAwait(false);
            var existingUniqueName = GetString(existingPublisher, "uniquename");
            if (existingPublisher is null || string.Equals(existingUniqueName, publisherUniqueName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return 72727;
    }

    private async Task<JsonObject?> FindPluginTypeAsync(
        HttpClient httpClient,
        TokenCredential credential,
        string typeName,
        Guid pluginAssemblyId,
        CancellationToken cancellationToken) =>
        await FindSingleAsync(
            httpClient,
            credential,
            $"plugintypes?$select=plugintypeid,typename,friendlyname,description,workflowactivitygroupname,_pluginassemblyid_value&$filter=typename eq '{EscapeODataLiteral(typeName)}' and _pluginassemblyid_value eq {FormatGuid(pluginAssemblyId)}",
            cancellationToken).ConfigureAwait(false);

    private async Task<HashSet<string>> GetPluginTypeNamesAsync(
        HttpClient httpClient,
        TokenCredential credential,
        Guid pluginAssemblyId,
        CancellationToken cancellationToken)
    {
        var rows = await GetRowsAsync(
            httpClient,
            credential,
            $"plugintypes?$select=typename,_pluginassemblyid_value&$filter=_pluginassemblyid_value eq {FormatGuid(pluginAssemblyId)}",
            cancellationToken).ConfigureAwait(false);

        return rows
            .Select(row => NormalizeLogicalName(GetString(row, "typename")))
            .Where(typeName => !string.IsNullOrWhiteSpace(typeName))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task WaitForPluginTypesAsync(
        HttpClient httpClient,
        TokenCredential credential,
        Guid pluginAssemblyId,
        IReadOnlyCollection<string> expectedTypeNames,
        ICollection<CompilerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (expectedTypeNames.Count == 0)
        {
            return;
        }

        for (var attempt = 0; attempt < 30; attempt++)
        {
            var currentTypeNames = await GetPluginTypeNamesAsync(httpClient, credential, pluginAssemblyId, cancellationToken).ConfigureAwait(false);
            if (expectedTypeNames.All(currentTypeNames.Contains))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }

        diagnostics.Add(new CompilerDiagnostic(
            "apply-plugin-type-timeout",
            DiagnosticSeverity.Warning,
            $"Timed out waiting for Dataverse to surface one or more plug-in types after pushing assembly {pluginAssemblyId:D}. Step/image creation may be deferred until a later publish run.",
            pluginAssemblyId.ToString("D", CultureInfo.InvariantCulture)));
    }

    private async Task<JsonObject?> FindPluginStepAsync(
        HttpClient httpClient,
        TokenCredential credential,
        string stepName,
        Guid pluginTypeId,
        Guid sdkMessageId,
        Guid? sdkMessageFilterId,
        CancellationToken cancellationToken)
    {
        var rows = await GetRowsAsync(
            httpClient,
            credential,
            $"sdkmessageprocessingsteps?$select=sdkmessageprocessingstepid,name,description,stage,mode,rank,supporteddeployment,filteringattributes,_eventhandler_value,_sdkmessageid_value,_sdkmessagefilterid_value&$filter=name eq '{EscapeODataLiteral(stepName)}'",
            cancellationToken).ConfigureAwait(false);

        return rows.FirstOrDefault(row =>
            GetGuid(row, "_eventhandler_value") == pluginTypeId
            && GetGuid(row, "_sdkmessageid_value") == sdkMessageId
            && (sdkMessageFilterId is null || GetGuid(row, "_sdkmessagefilterid_value") == sdkMessageFilterId));
    }

    private async Task<IReadOnlyList<JsonObject>> GetRowsAsync(
        HttpClient httpClient,
        TokenCredential credential,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var node = await GetJsonAsync(httpClient, credential, relativePath, cancellationToken).ConfigureAwait(false);
        return GetProperty(node, "value") is JsonArray array
            ? array.OfType<JsonObject>().ToArray()
            : node is JsonObject jsonObject
                ? [jsonObject]
                : Array.Empty<JsonObject>();
    }

    private static void AddStringProperty(JsonObject payload, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            payload[propertyName] = value;
        }
    }

    private static void AddIntegerProperty(JsonObject payload, string propertyName, string? value, int? fallback = null)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            payload[propertyName] = parsed;
        }
        else if (fallback.HasValue)
        {
            payload[propertyName] = fallback.Value;
        }
    }

    private static void AddBooleanProperty(JsonObject payload, string propertyName, string? value)
    {
        var normalized = NormalizeBoolean(value);
        if (string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase))
        {
            payload[propertyName] = true;
        }
        else if (string.Equals(normalized, "false", StringComparison.OrdinalIgnoreCase))
        {
            payload[propertyName] = false;
        }
    }

    private static string BuildServiceEndpointLookupQuery(string? logicalName, string name)
    {
        return $"serviceendpoints?$select=serviceendpointid,name,description,contract,connectionmode,authtype,namespaceaddress,path,url,messageformat,messagecharset,introducedversion&$filter=name eq '{EscapeODataLiteral(name)}'";
    }

    private static string BuildConnectorLookupQuery(string? logicalName, string? connectorInternalId, string name)
    {
        var filters = new List<string> { $"name eq '{EscapeODataLiteral(name)}'" };
        if (!string.IsNullOrWhiteSpace(connectorInternalId))
        {
            filters.Insert(0, $"connectorinternalid eq '{EscapeODataLiteral(connectorInternalId)}'");
        }

        return $"connectors?$select=connectorid,name,displayname,description,connectorinternalid,connectortype,capabilities,introducedversion&$filter={string.Join(" or ", filters)}";
    }

    private static string BuildPluginAssemblyLookupQuery(PluginAssemblyIdentity identity, string assemblyName)
    {
        var filters = new List<string> { $"name eq '{EscapeODataLiteral(assemblyName)}'" };
        if (!string.IsNullOrWhiteSpace(identity.Version))
        {
            filters.Add($"version eq '{EscapeODataLiteral(identity.Version)}'");
        }

        if (!string.IsNullOrWhiteSpace(identity.Culture))
        {
            filters.Add($"culture eq '{EscapeODataLiteral(identity.Culture)}'");
        }

        if (!string.IsNullOrWhiteSpace(identity.PublicKeyToken))
        {
            filters.Add($"publickeytoken eq '{EscapeODataLiteral(identity.PublicKeyToken)}'");
        }

        return $"pluginassemblies?$select=pluginassemblyid,name,path,isolationmode,sourcetype,introducedversion,version,culture,publickeytoken&$filter={string.Join(" and ", filters)}";
    }

    private static string? ResolveConnectorCapabilitiesPayload(string? capabilitiesJson)
    {
        var normalized = NormalizeCapabilitiesComparisonJson(capabilitiesJson);
        if (string.IsNullOrWhiteSpace(normalized) || JsonNode.Parse(normalized) is not JsonArray array)
        {
            return null;
        }

        var values = array
            .Select(StringValue)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(ResolveConnectorCapabilityOptionValue)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();

        return values.Length == 0
            ? null
            : string.Join(",", values.Select(value => value.ToString(CultureInfo.InvariantCulture)));
    }

    private static string? NormalizeCapabilitiesComparisonJson(JsonNode? node) =>
        NormalizeCapabilitiesComparisonJson(StringValue(node));

    private static string? NormalizeCapabilitiesComparisonJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var normalizedJson = NormalizeJson(raw);
        if (!string.IsNullOrWhiteSpace(normalizedJson)
            && JsonNode.Parse(normalizedJson) is JsonArray parsedArray)
        {
            return JsonSerializer.Serialize(parsedArray
                .Select(StringValue)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => NormalizeConnectorCapabilityName(value!))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray());
        }

        return JsonSerializer.Serialize(raw
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeConnectorCapabilityName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    private static int? ResolveConnectorCapabilityOptionValue(string? value) =>
        NormalizeConnectorCapabilityName(value) switch
        {
            "composite" => 118690000,
            "tabular" => 118690001,
            "blob" => 118690002,
            "gateway" => 118690003,
            "cloud" => 118690004,
            "actions" => 118690005,
            var text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var explicitValue) => explicitValue,
            _ => null
        };

    private static string? NormalizeConnectorCapabilityName(string? value)
    {
        var normalized = NormalizeLogicalName(value);
        return normalized switch
        {
            null => null,
            "118690000" => "composite",
            "118690001" => "tabular",
            "118690002" => "blob",
            "118690003" => "gateway",
            "118690004" => "cloud",
            "118690005" => "actions",
            _ => normalized
        };
    }

    private static string? ResolveServiceEndpointMessageCharsetOptionValue(string? value) =>
        NormalizeServiceEndpointMessageCharset(value) switch
        {
            "65001" => "1",
            "1252" => "2",
            "0" or "default" => "0",
            var normalized => normalized
        };

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

    private static PluginAssemblyIdentity ParseAssemblyIdentity(string fullName)
    {
        var identity = new PluginAssemblyIdentity();
        foreach (var segment in fullName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var delimiter = segment.IndexOf('=');
            if (delimiter < 0)
            {
                identity.Name ??= segment.Trim();
                continue;
            }

            var key = segment[..delimiter].Trim();
            var value = segment[(delimiter + 1)..].Trim();
            if (key.Equals("Version", StringComparison.OrdinalIgnoreCase))
            {
                identity.Version = value;
            }
            else if (key.Equals("Culture", StringComparison.OrdinalIgnoreCase))
            {
                identity.Culture = value;
            }
            else if (key.Equals("PublicKeyToken", StringComparison.OrdinalIgnoreCase))
            {
                identity.PublicKeyToken = value;
            }
        }

        identity.Name ??= fullName.Split(',', 2)[0].Trim();
        return identity;
    }

    private static string? ResolvePluginAssemblyBinaryPath(FamilyArtifact artifact)
    {
        foreach (var relativePath in ReadArtifactAssetSourcePaths(artifact))
        {
            var materializedPath = ResolveArtifactMaterializedPath(artifact, relativePath);
            if (materializedPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && File.Exists(materializedPath))
            {
                return materializedPath;
            }
        }

        return null;
    }

    private bool TryRunPacPluginPush(
        Guid pluginAssemblyId,
        string assemblyBinaryPath,
        Uri environmentUrl,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        var request = new ProcessStartInfo
        {
            FileName = "pac",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(assemblyBinaryPath)) ?? Environment.CurrentDirectory
        };

        foreach (var argument in new[]
                 {
                     "plugin",
                     "push",
                     "--environment",
                     environmentUrl.ToString(),
                     "--pluginId",
                     pluginAssemblyId.ToString("D", CultureInfo.InvariantCulture),
                     "--pluginFile",
                     Path.GetFullPath(assemblyBinaryPath),
                     "--type",
                     "Assembly"
                 })
        {
            request.ArgumentList.Add(argument);
        }

        try
        {
            using var process = new Process { StartInfo = request };
            process.Start();
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            diagnostics.Add(new CompilerDiagnostic(
                "apply-plugin-assembly-pac-push",
                DiagnosticSeverity.Info,
                $"Ran PAC plug-in push for assembly {pluginAssemblyId:D}. Command: pac plugin push --environment {environmentUrl} --pluginId {pluginAssemblyId:D} --pluginFile {Path.GetFullPath(assemblyBinaryPath)} --type Assembly",
                assemblyBinaryPath));

            if (!string.IsNullOrWhiteSpace(standardOutput))
            {
                diagnostics.Add(new CompilerDiagnostic(
                    "apply-plugin-assembly-pac-push-stdout",
                    DiagnosticSeverity.Info,
                    standardOutput.Trim(),
                    assemblyBinaryPath));
            }

            if (!string.IsNullOrWhiteSpace(standardError))
            {
                diagnostics.Add(new CompilerDiagnostic(
                    "apply-plugin-assembly-pac-push-stderr",
                    process.ExitCode == 0 ? DiagnosticSeverity.Warning : DiagnosticSeverity.Error,
                    standardError.Trim(),
                    assemblyBinaryPath));
            }

            if (process.ExitCode == 0)
            {
                return true;
            }

            diagnostics.Add(new CompilerDiagnostic(
                "apply-plugin-assembly-pac-push-failed",
                DiagnosticSeverity.Error,
                $"PAC plug-in push exited with code {process.ExitCode}.",
                assemblyBinaryPath));
            return false;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            diagnostics.Add(new CompilerDiagnostic(
                "apply-plugin-assembly-pac-push-start-failed",
                DiagnosticSeverity.Error,
                $"PAC plug-in push could not start: {exception.Message}",
                assemblyBinaryPath));
            return false;
        }
    }

    private static IReadOnlyList<string> ReadArtifactAssetSourcePaths(FamilyArtifact artifact)
    {
        var assetMapJson = GetArtifactProperty(artifact, ArtifactPropertyKeys.AssetSourceMapJson);
        if (!string.IsNullOrWhiteSpace(assetMapJson)
            && JsonSerializer.Deserialize<List<ArtifactAssetMapEntry>>(assetMapJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) is { Count: > 0 } mappedAssets)
        {
            return mappedAssets
                .Where(entry => !string.IsNullOrWhiteSpace(entry.SourcePath))
                .Select(entry => entry.SourcePath)
                .ToArray();
        }

        var singleAssetPath = GetArtifactProperty(artifact, ArtifactPropertyKeys.AssetSourcePath);
        return string.IsNullOrWhiteSpace(singleAssetPath) ? [] : [singleAssetPath];
    }

    private static string ResolveArtifactMaterializedPath(FamilyArtifact artifact, string relativeOrAbsolutePath)
    {
        if (Path.IsPathRooted(relativeOrAbsolutePath))
        {
            return Path.GetFullPath(relativeOrAbsolutePath);
        }

        var metadataRelativePath = GetArtifactProperty(artifact, ArtifactPropertyKeys.MetadataSourcePath);
        if (!string.IsNullOrWhiteSpace(metadataRelativePath)
            && !string.IsNullOrWhiteSpace(artifact.SourcePath)
            && File.Exists(artifact.SourcePath))
        {
            var sourceRoot = artifact.SourcePath;
            foreach (var _ in metadataRelativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                sourceRoot = Path.GetDirectoryName(sourceRoot) ?? string.Empty;
            }

            return Path.GetFullPath(Path.Combine(sourceRoot, relativeOrAbsolutePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        return Path.GetFullPath(relativeOrAbsolutePath);
    }

    private async Task<JsonObject?> FindSingleAsync(
        HttpClient httpClient,
        TokenCredential credential,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var node = await GetJsonAsync(httpClient, credential, relativePath, cancellationToken).ConfigureAwait(false);
        return node switch
        {
            JsonObject jsonObject when GetProperty(jsonObject, "value") is JsonArray array => array.OfType<JsonObject>().FirstOrDefault(),
            JsonObject jsonObject => jsonObject,
            JsonArray jsonArray => jsonArray.OfType<JsonObject>().FirstOrDefault(),
            _ => null
        };
    }

    private async Task<JsonNode?> GetJsonAsync(HttpClient httpClient, TokenCredential credential, string relativePath, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildRequestUri(relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(credential, cancellationToken).ConfigureAwait(false));

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Dataverse apply GET failed for '{relativePath}' with {(int)response.StatusCode} {response.StatusCode}: {content}",
                null,
                response.StatusCode);
        }

        return string.IsNullOrWhiteSpace(content) ? null : JsonNode.Parse(content);
    }

    private async Task<Guid?> ReadSolutionIdAsync(HttpClient httpClient, TokenCredential credential, string solutionUniqueName, CancellationToken cancellationToken)
    {
        var solution = await FindSingleAsync(
            httpClient,
            credential,
            $"solutions?$select=solutionid,uniquename&$filter=uniquename eq '{EscapeODataLiteral(solutionUniqueName)}'",
            cancellationToken).ConfigureAwait(false);
        return GetGuid(solution, "solutionid");
    }

    private async Task<bool> EnsureSolutionComponentAsync(
        HttpClient httpClient,
        TokenCredential credential,
        Guid solutionId,
        string solutionUniqueName,
        Guid componentId,
        int componentType,
        CancellationToken cancellationToken)
    {
        var existing = await GetJsonAsync(
            httpClient,
            credential,
            $"solutioncomponents?$select=solutioncomponentid&$filter=_solutionid_value eq {FormatGuid(solutionId)} and objectid eq {FormatGuid(componentId)} and componenttype eq {componentType}",
            cancellationToken).ConfigureAwait(false);
        if (GetProperty(existing, "value") is JsonArray existingRows && existingRows.Count > 0)
        {
            return false;
        }

        if (componentType is 91 or 92)
        {
            return TryRunPacAddSolutionComponent(solutionUniqueName, componentId, componentType, BuildEnvironmentUrl(), addRequiredComponents: componentType is 91 or 92, cancellationToken);
        }

        await SendJsonAsync(
            httpClient,
            credential,
            HttpMethod.Post,
            "AddSolutionComponent",
            new JsonObject
            {
                ["ComponentId"] = componentId,
                ["ComponentType"] = componentType,
                ["SolutionUniqueName"] = solutionUniqueName,
                ["AddRequiredComponents"] = false
            },
            cancellationToken).ConfigureAwait(false);

        return true;
    }

    private bool TryRunPacAddSolutionComponent(
        string solutionUniqueName,
        Guid componentId,
        int componentType,
        Uri environmentUrl,
        bool addRequiredComponents,
        CancellationToken cancellationToken)
    {
        var request = new ProcessStartInfo
        {
            FileName = "pac",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory
        };

        foreach (var argument in new[]
                 {
                     "solution",
                     "add-solution-component",
                     "--environment",
                     environmentUrl.ToString(),
                     "--solutionUniqueName",
                     solutionUniqueName,
                     "--component",
                     componentId.ToString("D", CultureInfo.InvariantCulture),
                     "--componentType",
                     componentType.ToString(CultureInfo.InvariantCulture)
                 })
        {
            request.ArgumentList.Add(argument);
        }

        if (addRequiredComponents)
        {
            request.ArgumentList.Add("--AddRequiredComponents");
        }

        using var process = new Process { StartInfo = request };
        process.Start();
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        cancellationToken.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"PAC add-solution-component failed for component {componentId:D} (type {componentType}) in solution {solutionUniqueName}: {standardError}".Trim());
        }

        return true;
    }

    private async Task SendJsonAsync(
        HttpClient httpClient,
        TokenCredential credential,
        HttpMethod method,
        string relativePath,
        JsonObject payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, BuildRequestUri(relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(credential, cancellationToken).ConfigureAwait(false));
        request.Content = new StringContent(payload.ToJsonString(JsonOptions), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException(
                $"Dataverse apply {method} failed for '{relativePath}' with {(int)response.StatusCode} {response.StatusCode}: {content}",
                null,
                response.StatusCode);
        }
    }

    private async Task<Guid?> SendCreateJsonAsync(
        HttpClient httpClient,
        TokenCredential credential,
        string relativePath,
        JsonObject payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildRequestUri(relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(credential, cancellationToken).ConfigureAwait(false));
        request.Content = new StringContent(payload.ToJsonString(JsonOptions), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Dataverse apply POST failed for '{relativePath}' with {(int)response.StatusCode} {response.StatusCode}: {content}",
                null,
                response.StatusCode);
        }

        var entityId = response.Headers.TryGetValues("OData-EntityId", out var entityIdValues)
            ? entityIdValues.FirstOrDefault()
            : response.Headers.Location?.ToString();
        if (!string.IsNullOrWhiteSpace(entityId) && TryExtractGuidFromReference(entityId!, out var createdId))
        {
            return createdId;
        }

        if (!string.IsNullOrWhiteSpace(content) && JsonNode.Parse(content) is JsonObject jsonObject)
        {
            return GetGuid(jsonObject, "pluginassemblyid");
        }

        return null;
    }

    private async Task<string> GetAccessTokenAsync(TokenCredential credential, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken))
        {
            return _accessToken;
        }

        if (_serviceRoot is null)
        {
            throw new InvalidOperationException("Service root must be initialized before acquiring an access token.");
        }

        var resourceRoot = $"{_serviceRoot.Scheme}://{_serviceRoot.Host}";
        foreach (var scope in new[] { $"{resourceRoot}/.default", $"{resourceRoot}/user_impersonation" })
        {
            try
            {
                var token = await credential.GetTokenAsync(new TokenRequestContext([scope]), cancellationToken).ConfigureAwait(false);
                _accessToken = token.Token;
                return token.Token;
            }
            catch (AuthenticationFailedException)
            {
                // Try the next Dataverse scope.
            }
        }

        throw new AuthenticationFailedException("Failed to acquire a Dataverse Web API access token for live apply.");
    }

    private Uri BuildRequestUri(string relativePath) =>
        _serviceRoot is null
            ? throw new InvalidOperationException("Service root must be initialized before issuing requests.")
            : Uri.TryCreate(relativePath, UriKind.Absolute, out var absolute)
                ? absolute
                : new Uri(_serviceRoot, relativePath);

    private Uri BuildEnvironmentUrl() =>
        _serviceRoot is null
            ? throw new InvalidOperationException("Service root must be initialized before deriving the Dataverse environment URL.")
            : new($"{_serviceRoot.Scheme}://{_serviceRoot.Host}/", UriKind.Absolute);

    private static Uri BuildServiceRoot(Uri dataverseUrl) =>
        new($"{dataverseUrl.ToString().TrimEnd('/')}/api/data/v9.2/", UriKind.Absolute);

    private static TokenCredential CreateCredential(ApplyRequest request)
    {
        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrWhiteSpace(request.Environment.TenantId))
        {
            credentialOptions.TenantId = request.Environment.TenantId;
        }

        return new DefaultAzureCredential(credentialOptions);
    }

    private static JsonNode? GetProperty(JsonNode? node, params string[] names)
    {
        if (node is not JsonObject jsonObject)
        {
            return null;
        }

        foreach (var name in names)
        {
            foreach (var property in jsonObject)
            {
                if (string.Equals(property.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value;
                }
            }
        }

        return null;
    }

    private static string? GetArtifactProperty(FamilyArtifact artifact, string key) =>
        artifact.Properties is not null && artifact.Properties.TryGetValue(key, out var value)
            ? value
            : null;

    private static string? GetString(JsonNode? node, params string[] names) =>
        StringValue(GetProperty(node, names));

    private static string? StringValue(JsonNode? node) =>
        node switch
        {
            null => null,
            JsonValue value => value.TryGetValue<string>(out var stringValue)
                ? stringValue
                : value.TryGetValue<bool>(out var boolValue)
                    ? NormalizeBoolean(boolValue ? "true" : "false")
                    : value.ToJsonString(),
            JsonObject jsonObject => GetString(jsonObject, "Value"),
            _ => node.ToJsonString()
        };

    private static Guid? GetGuid(JsonNode? node, params string[] names)
    {
        var value = GetString(node, names);
        return Guid.TryParse(value, out var guid) ? guid : null;
    }

    private static bool TryExtractGuidFromReference(string value, out Guid guid)
    {
        var start = value.LastIndexOf('(');
        var end = value.LastIndexOf(')');
        if (start >= 0 && end > start)
        {
            var candidate = value[(start + 1)..end];
            if (Guid.TryParse(candidate, out guid))
            {
                return true;
            }
        }

        return Guid.TryParse(value, out guid);
    }

    private static string NormalizeBoolean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "false";
        }

        return value.Trim() switch
        {
            "1" => "true",
            "0" => "false",
            var text when text.Equals("true", StringComparison.OrdinalIgnoreCase) => "true",
            var text when text.Equals("false", StringComparison.OrdinalIgnoreCase) => "false",
            _ => value.Trim().ToLowerInvariant()
        };
    }

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

    private static string? NormalizeJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(value)?.ToJsonString(JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? NormalizeEntityAnalyticsDataSource(string? value)
    {
        var normalized = NormalizeLogicalName(value);
        return normalized switch
        {
            null => null,
            "0" or "none" => "none",
            "1" or "dataverse" => "dataverse",
            "2" or "fnotables" => "fnotables",
            _ => normalized
        };
    }

    private static string BuildAiProjectTypeResourceInfo(string? displayName, string? description) =>
        new JsonObject
        {
            ["displayName"] = displayName,
            ["description"] = description
        }.ToJsonString(JsonOptions);

    private static string BuildAiProjectCreationContext(string logicalName, string? description, string? targetEntity) =>
        new JsonObject
        {
            ["logicalName"] = logicalName,
            ["description"] = description,
            ["targetEntity"] = targetEntity
        }.ToJsonString(JsonOptions);

    private static string BuildAiConfigurationResourceInfo(string logicalName, string? parentProjectLogicalName) =>
        new JsonObject
        {
            ["logicalName"] = logicalName,
            ["parentProjectLogicalName"] = parentProjectLogicalName
        }.ToJsonString(JsonOptions);

    private static string? GetAiProjectTypeResourceInfoValue(JsonNode? row, string propertyName) =>
        GetString(ParseJsonObjectSafe(GetString(row, "msdyn_resourceinfo")), propertyName);

    private static string? GetAiProjectContextValue(JsonNode? row, string propertyName) =>
        GetString(ParseJsonObjectSafe(GetString(row, "msdyn_modelcreationcontext")), propertyName);

    private static string? GetAiConfigurationResourceInfoValue(JsonNode? row, string propertyName) =>
        GetString(ParseJsonObjectSafe(GetString(row, "msdyn_resourceinfo")), propertyName);

    private static JsonObject? ParseJsonObjectSafe(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(value) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int ResolveEntityAnalyticsDataSourceOptionValue(string? value) =>
        NormalizeEntityAnalyticsDataSource(value) switch
        {
            "none" => 0,
            "fnotables" => 2,
            _ => 1
        };

    private static string? NormalizeAiConfigurationKind(string? value)
    {
        var normalized = NormalizeLogicalName(value);
        return normalized switch
        {
            null => null,
            "190690000" or "trainingconfiguration" or "training" => "training",
            "190690001" or "runconfiguration" or "run" => "run",
            _ => normalized
        };
    }

    private static int ResolveAiConfigurationTypeOptionValue(string? value) =>
        NormalizeAiConfigurationKind(value) switch
        {
            "run" => 190690001,
            _ => 190690000
        };

    private static string? NormalizeLogicalName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string EscapeODataLiteral(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);

    private static string FormatGuid(Guid value) =>
        value.ToString("D");

    private sealed class PluginAssemblyIdentity
    {
        public string? Name { get; set; }

        public string? Version { get; set; }

        public string? Culture { get; set; }

        public string? PublicKeyToken { get; set; }
    }

    private sealed record ArtifactAssetMapEntry(string SourcePath, string PackageRelativePath);
}
