using System.Globalization;
using System.Net;
using Azure.Core;
using Azure.Identity;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Live;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Readers.Live;

internal sealed record DataverseWebApiLiveReaderOptions(
    string ApiVersion = "v9.2",
    int PageSize = 250,
    bool EnableEntityScopedUiFallback = true);

internal sealed partial class DataverseWebApiLiveReader
{
    private static readonly HashSet<ComponentFamily> SupportedFamilies =
    [
        ComponentFamily.SolutionShell,
        ComponentFamily.Table,
        ComponentFamily.Column,
        ComponentFamily.Relationship,
        ComponentFamily.OptionSet,
        ComponentFamily.Key,
        ComponentFamily.ImageConfiguration,
        ComponentFamily.Form,
        ComponentFamily.View,
        ComponentFamily.AppModule,
        ComponentFamily.AppSetting,
        ComponentFamily.SiteMap,
        ComponentFamily.EnvironmentVariableDefinition,
        ComponentFamily.EnvironmentVariableValue,
        ComponentFamily.ImportMap,
        ComponentFamily.DataSourceMapping,
        ComponentFamily.PluginAssembly,
        ComponentFamily.PluginType,
        ComponentFamily.PluginStep,
        ComponentFamily.PluginStepImage,
        ComponentFamily.ServiceEndpoint,
        ComponentFamily.Connector,
        ComponentFamily.DuplicateRule,
        ComponentFamily.DuplicateRuleCondition,
        ComponentFamily.RoutingRule,
        ComponentFamily.RoutingRuleItem,
        ComponentFamily.MobileOfflineProfile,
        ComponentFamily.MobileOfflineProfileItem,
        ComponentFamily.Role,
        ComponentFamily.RolePrivilege,
        ComponentFamily.FieldSecurityProfile,
        ComponentFamily.FieldPermission,
        ComponentFamily.ConnectionRole,
        ComponentFamily.Sla,
        ComponentFamily.SlaItem,
        ComponentFamily.SimilarityRule,
        ComponentFamily.AiProjectType,
        ComponentFamily.AiProject,
        ComponentFamily.AiConfiguration,
        ComponentFamily.EntityAnalyticsConfiguration,
        ComponentFamily.CanvasApp
    ];

    private readonly HttpClient _httpClient;
    private readonly TokenCredential _credential;
    private readonly DataverseWebApiLiveReaderOptions _options;
    private string? _accessToken;
    private Uri? _serviceRoot;

    internal DataverseWebApiLiveReader(
        HttpClient httpClient,
        TokenCredential credential,
        DataverseWebApiLiveReaderOptions? options = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _options = options ?? new DataverseWebApiLiveReaderOptions();
    }

    public async Task<LiveSnapshot> ReadAsync(ReadbackRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Environment.DataverseUrl is null)
        {
            return CreateSnapshot(
                request,
                [],
                [
                    new CompilerDiagnostic(
                        "live-readback-missing-url",
                        DiagnosticSeverity.Error,
                        "Live Dataverse readback requires Environment.DataverseUrl.")
                ]);
        }

        if (string.IsNullOrWhiteSpace(request.SolutionUniqueName))
        {
            return CreateSnapshot(
                request,
                [],
                [
                    new CompilerDiagnostic(
                        "live-readback-missing-solution",
                        DiagnosticSeverity.Error,
                        "Library-first live readback currently requires SolutionUniqueName.",
                        request.Environment.DataverseUrl.ToString())
                ]);
        }

        var diagnostics = new List<CompilerDiagnostic>();
        var requestedFamilies = ResolveRequestedFamilies(request, diagnostics);
        if (requestedFamilies.Count == 0)
        {
            return CreateSnapshot(request, [], diagnostics);
        }

        AddKnownBoundaryDiagnostics(requestedFamilies, diagnostics);

        _serviceRoot = BuildServiceRoot(request.Environment.DataverseUrl, _options.ApiVersion);

        var artifacts = new List<FamilyArtifact>();
        SolutionRecord? solution;

        try
        {
            solution = await ReadSolutionAsync(request.SolutionUniqueName!, cancellationToken).ConfigureAwait(false);
        }
        catch (DataverseWebApiException exception)
        {
            diagnostics.Add(ToDiagnostic(exception, DiagnosticSeverity.Error));
            return CreateSnapshot(request, artifacts, diagnostics);
        }
        catch (AuthenticationFailedException exception)
        {
            diagnostics.Add(ToDiagnostic(exception, DiagnosticSeverity.Error, location: request.Environment.DataverseUrl.ToString()));
            return CreateSnapshot(request, artifacts, diagnostics);
        }

        if (solution is null)
        {
            diagnostics.Add(new CompilerDiagnostic(
                "live-readback-solution-not-found",
                DiagnosticSeverity.Error,
                $"The solution '{request.SolutionUniqueName}' was not found in live Dataverse readback.",
                request.Environment.DataverseUrl.ToString()));
            return CreateSnapshot(request, artifacts, diagnostics);
        }

        if (requestedFamilies.Contains(ComponentFamily.SolutionShell))
        {
            artifacts.Add(CreateSolutionArtifact(solution));
        }

        var scope = new SolutionComponentScope();
        try
        {
            scope = await ReadSolutionComponentScopeAsync(solution, requestedFamilies, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is DataverseWebApiException or AuthenticationFailedException)
        {
            diagnostics.Add(ToDiagnostic(exception, DiagnosticSeverity.Warning));
        }

        if (ShouldReadAny(requestedFamilies, ComponentFamily.Table, ComponentFamily.Column, ComponentFamily.Relationship, ComponentFamily.OptionSet, ComponentFamily.Key, ComponentFamily.ImageConfiguration, ComponentFamily.Form, ComponentFamily.View))
        {
            await ReadSchemaFamiliesAsync(solution, scope, requestedFamilies, artifacts, diagnostics, cancellationToken).ConfigureAwait(false);
        }

        if (ShouldReadAny(requestedFamilies, ComponentFamily.PluginAssembly, ComponentFamily.PluginType, ComponentFamily.PluginStep, ComponentFamily.PluginStepImage, ComponentFamily.ServiceEndpoint, ComponentFamily.Connector))
        {
            await ReadCodeExtensibilityFamiliesAsync(scope, requestedFamilies, artifacts, diagnostics, cancellationToken).ConfigureAwait(false);
        }

        if (ShouldReadAny(requestedFamilies, ComponentFamily.DuplicateRule, ComponentFamily.DuplicateRuleCondition, ComponentFamily.RoutingRule, ComponentFamily.RoutingRuleItem, ComponentFamily.MobileOfflineProfile, ComponentFamily.MobileOfflineProfileItem))
        {
            await ReadProcessPolicyFamiliesAsync(scope, requestedFamilies, artifacts, diagnostics, cancellationToken).ConfigureAwait(false);
        }

        if (ShouldReadAny(requestedFamilies, ComponentFamily.Role, ComponentFamily.RolePrivilege, ComponentFamily.FieldSecurityProfile, ComponentFamily.FieldPermission, ComponentFamily.ConnectionRole))
        {
            await ReadSecurityFamiliesAsync(scope, requestedFamilies, artifacts, diagnostics, cancellationToken).ConfigureAwait(false);
        }

        if (ShouldReadAny(requestedFamilies, ComponentFamily.AppModule, ComponentFamily.AppSetting, ComponentFamily.SiteMap, ComponentFamily.EnvironmentVariableDefinition, ComponentFamily.EnvironmentVariableValue, ComponentFamily.AiProjectType, ComponentFamily.AiProject, ComponentFamily.AiConfiguration, ComponentFamily.EntityAnalyticsConfiguration, ComponentFamily.CanvasApp))
        {
            await ReadAppShellFamiliesAsync(scope, requestedFamilies, artifacts, diagnostics, cancellationToken).ConfigureAwait(false);
        }

        diagnostics.Add(new CompilerDiagnostic(
            "live-readback-library-first",
            DiagnosticSeverity.Info,
            "Live Dataverse readback projects the currently proven family set into the shared canonical IR and keeps explicit best-effort boundaries for source-first families.",
            request.SolutionUniqueName));

        return CreateSnapshot(request, artifacts, diagnostics);
    }

    private static bool ShouldReadAny(IReadOnlySet<ComponentFamily> requestedFamilies, params ComponentFamily[] families) =>
        families.Any(requestedFamilies.Contains);

    private static IReadOnlySet<ComponentFamily> ResolveRequestedFamilies(ReadbackRequest request, ICollection<CompilerDiagnostic> diagnostics)
    {
        if (request.Families is null || request.Families.Count == 0)
        {
            return SupportedFamilies;
        }

        var requested = new HashSet<ComponentFamily>(request.Families.Where(SupportedFamilies.Contains));
        var unsupported = request.Families.Where(family => !SupportedFamilies.Contains(family)).Distinct().ToArray();
        if (unsupported.Length > 0)
        {
            diagnostics.Add(new CompilerDiagnostic(
                "live-readback-unsupported-families",
                DiagnosticSeverity.Info,
                $"Live readback still defers these families in the library-first slice: {string.Join(", ", unsupported.OrderBy(name => name.ToString(), StringComparer.Ordinal))}."));
        }

        return requested;
    }

    private static void AddKnownBoundaryDiagnostics(IReadOnlySet<ComponentFamily> requestedFamilies, ICollection<CompilerDiagnostic> diagnostics)
    {
        if (requestedFamilies.Contains(ComponentFamily.ImportMap) || requestedFamilies.Contains(ComponentFamily.DataSourceMapping))
        {
            diagnostics.Add(new CompilerDiagnostic(
                "live-readback-import-map-source-first",
                DiagnosticSeverity.Info,
                "ImportMap and DataSourceMapping remain explicit source-first families in the neutral corpus. Live readback does not project them into blocking overlap today.",
                "import-maps"));
        }

        var sourceFirstPolicyFamilies = new[]
        {
            ComponentFamily.SimilarityRule,
            ComponentFamily.Sla,
            ComponentFamily.SlaItem
        }.Where(requestedFamilies.Contains).ToArray();
        if (sourceFirstPolicyFamilies.Length > 0)
        {
            diagnostics.Add(new CompilerDiagnostic(
                "live-readback-source-first-process-policy",
                DiagnosticSeverity.Info,
                $"These process-policy families remain intentional source-first boundaries in the neutral corpus: {string.Join(", ", sourceFirstPolicyFamilies.OrderBy(family => family.ToString(), StringComparer.Ordinal))}.",
                "process-policy"));
        }
    }

    private static LiveSnapshot CreateSnapshot(
        ReadbackRequest request,
        IEnumerable<FamilyArtifact> artifacts,
        IEnumerable<CompilerDiagnostic> diagnostics) =>
        new(
            request.Environment,
            request.SolutionUniqueName,
            artifacts
                .GroupBy(artifact => $"{artifact.Family}:{artifact.LogicalName}".ToLowerInvariant(), StringComparer.Ordinal)
                .Select(group => group
                    .OrderBy(artifact => artifact.SourcePath, StringComparer.OrdinalIgnoreCase)
                    .First())
                .OrderBy(artifact => artifact.Family)
                .ThenBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(artifact => artifact.SourcePath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            diagnostics.ToArray());

    private static CompilerDiagnostic CreateFamilyFailureDiagnostic(ComponentFamily family, string location, Exception exception) =>
        ToDiagnostic(exception, DiagnosticSeverity.Warning, family, location);

    private static CompilerDiagnostic ToDiagnostic(Exception exception, DiagnosticSeverity severity, ComponentFamily? family = null, string? location = null) =>
        exception switch
        {
            DataverseWebApiException webApiException => new CompilerDiagnostic(
                webApiException.Code,
                severity,
                family is null
                    ? webApiException.Message
                    : $"{family} live readback failed: {webApiException.Message}",
                location ?? webApiException.Location),
            AuthenticationFailedException authenticationFailedException => new CompilerDiagnostic(
                "live-readback-auth-failure",
                severity,
                family is null
                    ? authenticationFailedException.Message
                    : $"{family} live readback failed: {authenticationFailedException.Message}",
                location),
            _ => new CompilerDiagnostic(
                "live-readback-unexpected-failure",
                severity,
                family is null
                    ? exception.Message
                    : $"{family} live readback failed: {exception.Message}",
                location)
        };

    private sealed record SolutionRecord(
        Guid Id,
        string UniqueName,
        string DisplayName,
        string Version,
        string Managed,
        string? PublisherUniqueName,
        string? PublisherPrefix,
        string? PublisherDisplayName);

    private sealed class SolutionComponentScope
    {
        public HashSet<string> EntityLogicalNames { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<Guid> EntityMetadataIds { get; } = [];
        public HashSet<string> GlobalOptionSetNames { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<Guid> GlobalOptionSetMetadataIds { get; } = [];
        public HashSet<Guid> KeyMetadataIds { get; } = [];
        public HashSet<Guid> AttributeImageConfigurationIds { get; } = [];
        public HashSet<Guid> EntityImageConfigurationIds { get; } = [];
        public HashSet<Guid> FormIds { get; } = [];
        public HashSet<Guid> ViewIds { get; } = [];
        public HashSet<Guid> AppModuleIds { get; } = [];
        public HashSet<Guid> SiteMapIds { get; } = [];
        public HashSet<Guid> EnvironmentVariableDefinitionIds { get; } = [];
        public HashSet<Guid> EnvironmentVariableValueIds { get; } = [];
        public HashSet<Guid> PluginAssemblyIds { get; } = [];
        public HashSet<Guid> PluginTypeIds { get; } = [];
        public HashSet<Guid> PluginStepIds { get; } = [];
        public HashSet<Guid> PluginStepImageIds { get; } = [];
        public HashSet<Guid> ServiceEndpointIds { get; } = [];
        public HashSet<Guid> ConnectorIds { get; } = [];
        public HashSet<Guid> DuplicateRuleIds { get; } = [];
        public HashSet<Guid> RoutingRuleIds { get; } = [];
        public HashSet<Guid> MobileOfflineProfileIds { get; } = [];
        public HashSet<Guid> RoleIds { get; } = [];
        public HashSet<Guid> FieldSecurityProfileIds { get; } = [];
        public HashSet<Guid> ConnectionRoleIds { get; } = [];
        public HashSet<Guid> AiProjectTypeIds { get; } = [];
        public HashSet<Guid> AiProjectIds { get; } = [];
        public HashSet<Guid> AiConfigurationIds { get; } = [];
        public HashSet<Guid> EntityAnalyticsConfigurationIds { get; } = [];
        public HashSet<Guid> CanvasAppIds { get; } = [];
        public HashSet<string> AttributeImageConfigurationLogicalNames { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> EntityImageConfigurationEntities { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record AppModuleContext(
        Guid Id,
        string UniqueName,
        string DisplayName,
        string? Description,
        string[] ComponentTypes,
        string[] RoleIds,
        AppModuleSetting[] NestedSettings);

    private sealed record AppModuleSetting(string SettingDefinitionUniqueName, string? Value);

    private sealed record FormSummary(
        string FormType,
        string FormId,
        int TabCount,
        int SectionCount,
        int ControlCount,
        int QuickFormCount,
        int SubgridCount,
        int HeaderControlCount,
        int FooterControlCount,
        IReadOnlyList<ControlDescription> ControlDescriptions);

    private sealed record ControlDescription(string Id, string DataFieldName, string Role);

    private sealed record ViewSummary(
        string TargetEntity,
        IReadOnlyList<string> LayoutColumns,
        IReadOnlyList<string> FetchAttributes,
        IReadOnlyList<ViewFilter> Filters,
        IReadOnlyList<ViewOrder> Orders);

    private sealed record ViewFilter(string Attribute, string Operator, string Value);

    private sealed record ViewOrder(string Attribute, string Descending);

    private sealed record SiteMapSummary(int AreaCount, int GroupCount, int SubAreaCount, int WebResourceSubAreaCount);
}

internal sealed class DataverseWebApiException : Exception
{
    public DataverseWebApiException(string code, HttpStatusCode? statusCode, string location, string message)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Location = location;
    }

    public string Code { get; }

    public HttpStatusCode? StatusCode { get; }

    public string Location { get; }
}
