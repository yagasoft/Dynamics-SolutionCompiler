using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Emission;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Emitters.TrackedSource;

public sealed partial class IntentSpecEmitter : ISolutionEmitter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private static readonly IReadOnlySet<ComponentFamily> SupportedFamilies = new HashSet<ComponentFamily>
    {
        ComponentFamily.SolutionShell,
        ComponentFamily.Publisher,
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
        ComponentFamily.EnvironmentVariableValue
    };

    public EmittedArtifacts Emit(CanonicalSolution model, EmitRequest request)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(request);

        var outputRoot = Path.GetFullPath(request.OutputRoot);
        var intentRoot = GetContainedPath(outputRoot, "intent-spec");
        Directory.CreateDirectory(intentRoot);

        var diagnostics = new List<CompilerDiagnostic>();
        var unsupportedEntries = new List<IntentReportEntry>();
        var emittedFamilies = new HashSet<string>(StringComparer.Ordinal);
        var preservedIds = new List<PreservedIdEntry>();
        var sourceBackedArtifactsIncluded = new List<SourceBackedArtifactEntry>();
        var emittedFiles = new List<EmittedArtifact>();
        var sourceBackedArtifacts = BuildSourceBackedArtifactSpecs(
            model.Artifacts,
            intentRoot,
            unsupportedEntries,
            sourceBackedArtifactsIncluded,
            emittedFamilies,
            emittedFiles);
        var sourceBackedArtifactKeys = sourceBackedArtifactsIncluded
            .Select(entry => $"{entry.Family}|{entry.Artifact}")
            .ToHashSet(StringComparer.Ordinal);

        unsupportedEntries.AddRange(BuildUnsupportedArtifactEntries(model.Artifacts, sourceBackedArtifactKeys));
        unsupportedEntries.AddRange(BuildUnsupportedDiagnosticEntries(model.Diagnostics));

        var solutionArtifact = model.Artifacts.FirstOrDefault(artifact => artifact.Family == ComponentFamily.SolutionShell);
        var publisherArtifact = model.Artifacts.FirstOrDefault(artifact => artifact.Family == ComponentFamily.Publisher);

        var globalOptionSets = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.OptionSet && GetBoolProperty(artifact, ArtifactPropertyKeys.IsGlobal))
            .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
            .Select(BuildGlobalOptionSetSpec)
            .Where(spec => spec is not null)
            .Cast<IntentGlobalOptionSetSpec>()
            .ToArray();
        if (globalOptionSets.Length > 0)
        {
            emittedFamilies.Add(ComponentFamily.OptionSet.ToString());
        }

        var relationshipsByTableAndAttribute = BuildSupportedRelationshipLookup(model.Artifacts, unsupportedEntries);
        var localOptionSetsByColumn = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.OptionSet && !GetBoolProperty(artifact, ArtifactPropertyKeys.IsGlobal))
            .ToDictionary(artifact => artifact.LogicalName, artifact => artifact, StringComparer.OrdinalIgnoreCase);

        var formsByTable = GroupArtifactsByEntity(model.Artifacts, ComponentFamily.Form);
        var viewsByTable = GroupArtifactsByEntity(model.Artifacts, ComponentFamily.View);
        var keysByTable = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.Key)
            .GroupBy(artifact => NormalizeLogicalName(GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName)) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.OrdinalIgnoreCase);

        var tableSpecs = BuildTableSpecs(
            model.Artifacts,
            localOptionSetsByColumn,
            relationshipsByTableAndAttribute,
            keysByTable,
            formsByTable,
            viewsByTable,
            unsupportedEntries,
            preservedIds,
            emittedFamilies);

        var siteMapsByLogicalName = BuildSiteMapSpecs(model.Artifacts, unsupportedEntries, emittedFamilies);
        var appModuleSpecs = BuildAppModuleSpecs(model.Artifacts, siteMapsByLogicalName, unsupportedEntries, emittedFamilies);
        var environmentVariables = BuildEnvironmentVariableSpecs(model.Artifacts, unsupportedEntries);
        if (environmentVariables.Count > 0)
        {
            emittedFamilies.Add(ComponentFamily.EnvironmentVariableDefinition.ToString());
            emittedFamilies.Add(ComponentFamily.EnvironmentVariableValue.ToString());
        }

        var document = new IntentSpecDocument
        {
            SpecVersion = "1.0",
            Solution = new IntentSolutionSpec
            {
                UniqueName = model.Identity.UniqueName,
                DisplayName = model.Identity.DisplayName,
                Description = solutionArtifact is null ? null : GetProperty(solutionArtifact, ArtifactPropertyKeys.Description),
                Version = model.Identity.Version,
                LayeringIntent = model.Identity.LayeringIntent.ToString()
            },
            Publisher = new IntentPublisherSpec
            {
                UniqueName = model.Publisher.UniqueName,
                Prefix = model.Publisher.Prefix,
                DisplayName = model.Publisher.DisplayName,
                Description = publisherArtifact is null ? null : GetProperty(publisherArtifact, ArtifactPropertyKeys.Description)
            },
            GlobalOptionSets = globalOptionSets,
            EnvironmentVariables = environmentVariables,
            AppModules = appModuleSpecs,
            Tables = tableSpecs,
            SourceBackedArtifacts = sourceBackedArtifacts
        };

        var report = new ReverseGenerationReport
        {
            InputKind = DetectInputKind(model),
            IsPartial = unsupportedEntries.Count > 0,
            SupportedFamiliesEmitted = emittedFamilies.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            UnsupportedFamiliesOmitted = unsupportedEntries
                .OrderBy(entry => entry.Family, StringComparer.Ordinal)
                .ThenBy(entry => entry.Artifact, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            PreservedIdsIncluded = preservedIds
                .OrderBy(entry => entry.Family, StringComparer.Ordinal)
                .ThenBy(entry => entry.Artifact, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            SourceBackedArtifactsIncluded = sourceBackedArtifactsIncluded
                .OrderBy(entry => entry.Family, StringComparer.Ordinal)
                .ThenBy(entry => entry.Artifact, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };

        WriteJson(intentRoot, "intent-spec.json", document, emittedFiles, "Reverse-generated compiler-native intent spec.");
        WriteJson(intentRoot, "reverse-generation-report.json", report, emittedFiles, "Reverse-generation coverage and omission report.");

        diagnostics.Add(new CompilerDiagnostic(
            unsupportedEntries.Count > 0 ? "intent-spec-reverse-partial" : "intent-spec-reverse-full",
            unsupportedEntries.Count > 0 ? DiagnosticSeverity.Warning : DiagnosticSeverity.Info,
            unsupportedEntries.Count > 0
                ? $"Reverse-generated intent-spec JSON is partial: {unsupportedEntries.Count} unsupported, platform-generated, or fidelity omission(s) were recorded in reverse-generation-report.json."
                : "Reverse-generated intent-spec JSON covered the supported subset without omissions.",
            unsupportedEntries.Count > 0
                ? Path.Combine(intentRoot, "reverse-generation-report.json")
                : Path.Combine(intentRoot, "intent-spec.json")));

        return new EmittedArtifacts(
            Success: true,
            OutputRoot: outputRoot,
            Files: emittedFiles,
            Diagnostics: diagnostics);
    }

    private static void WriteJson(string outputRoot, string relativePath, object document, List<EmittedArtifact> emittedFiles, string description)
    {
        var fullPath = GetContainedPath(outputRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var json = JsonSerializer.Serialize(document, JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal);
        File.WriteAllText(fullPath, json + "\n", Utf8NoBom);
        emittedFiles.Add(new EmittedArtifact($"intent-spec/{relativePath.Replace('\\', '/')}", EmittedArtifactRole.IntentSpec, description));
    }

    private static string GetContainedPath(string root, string relativePath)
    {
        var rootFullPath = Path.GetFullPath(root);
        var candidatePath = Path.GetFullPath(Path.Combine(rootFullPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = rootFullPath.EndsWith(Path.DirectorySeparatorChar) ? rootFullPath : rootFullPath + Path.DirectorySeparatorChar;
        if (!candidatePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !candidatePath.Equals(rootFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Refusing to write outside the intent-spec root: {relativePath}");
        }

        return candidatePath;
    }
}
