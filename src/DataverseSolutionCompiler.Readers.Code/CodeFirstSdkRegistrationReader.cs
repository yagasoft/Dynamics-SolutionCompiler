using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Build;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Read;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DataverseSolutionCompiler.Readers.Code;

public sealed partial class CodeFirstSdkRegistrationReader : ISolutionReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private const string AssemblyRegistrationMarker = "DbmPluginAssemblyRegistration";
    private const string WorkflowActivityMarker = "WorkflowActivityGroupName";
    private static readonly string[] StepRegistrationMarkers =
    [
        "sdkmessageprocessingstep",
        "sdkmessageprocessingstepimage"
    ];

    public CanonicalSolution Read(ReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);

        var rootPath = ResolveInputRoot(request.SourcePath);
        var projectPaths = EnumerateProjectPaths(rootPath).ToArray();
        if (projectPaths.Length == 0)
        {
            throw new InvalidOperationException($"Code-first SDK registration input '{request.SourcePath}' does not contain any buildable C# projects.");
        }

        var sourceFiles = EnumerateSourceFiles(rootPath).ToArray();
        if (!ContainsRegistrationMarkers(sourceFiles))
        {
            throw new InvalidOperationException($"Code-first SDK registration input '{request.SourcePath}' does not contain the required raw SDK registration markers.");
        }

        var diagnostics = new List<CompilerDiagnostic>();
        var pluginAssemblyArtifacts = new List<FamilyArtifact>();
        var pluginTypeArtifacts = new List<FamilyArtifact>();
        var pluginStepArtifacts = new List<FamilyArtifact>();
        var pluginStepImageArtifacts = new List<FamilyArtifact>();

        var projectInfos = projectPaths
            .Select(path => ReadProjectInfo(rootPath, path))
            .ToDictionary(info => info.FullPath, StringComparer.OrdinalIgnoreCase);
        var projectSyntaxRoots = projectInfos.Keys
            .ToDictionary(
                projectPath => projectPath,
                LoadProjectSyntaxRoots,
                StringComparer.OrdinalIgnoreCase);

        foreach (var sourceFile in sourceFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile), path: sourceFile);
            var root = tree.GetRoot();
            var assemblyRegistrations = root
                .DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Where(node => string.Equals(GetTypeName(node.Type), "DbmPluginAssemblyRegistration", StringComparison.Ordinal))
                .ToArray();

            if (assemblyRegistrations.Length == 0)
            {
                continue;
            }

            var owningProjectPath = ResolveOwningProjectPath(projectInfos.Keys, sourceFile);
            if (owningProjectPath is null || !projectInfos.TryGetValue(owningProjectPath, out var projectInfo))
            {
                diagnostics.Add(CreateDiagnostic(
                    "code-first-registration-project-unresolved",
                    DataverseSolutionCompiler.Domain.Diagnostics.DiagnosticSeverity.Error,
                    "Could not resolve a containing C# project for the code-first registration file.",
                    sourceFile,
                    tree.GetRoot().GetLocation()));
                continue;
            }

            var assetMapJson = SerializeJson(CollectAssetMap(rootPath, projectInfo));
            foreach (var assemblyRegistration in assemblyRegistrations)
            {
                if (!TryParsePluginAssemblyRegistration(
                        tree,
                        sourceFile,
                        rootPath,
                        projectInfo,
                        projectSyntaxRoots[projectInfo.FullPath],
                        assetMapJson,
                        assemblyRegistration,
                        diagnostics,
                        out var assemblyArtifact,
                        out var typeArtifacts,
                        out var stepArtifacts,
                        out var imageArtifacts))
                {
                    continue;
                }

                pluginAssemblyArtifacts.Add(assemblyArtifact!);
                pluginTypeArtifacts.AddRange(typeArtifacts);
                pluginStepArtifacts.AddRange(stepArtifacts);
                pluginStepImageArtifacts.AddRange(imageArtifacts);
            }
        }

        if (pluginAssemblyArtifacts.Count == 0)
        {
            diagnostics.Add(new CompilerDiagnostic(
                "code-first-registration-none-supported",
                DataverseSolutionCompiler.Domain.Diagnostics.DiagnosticSeverity.Error,
                "The code-first SDK registration reader did not find any supported DBM-style plugin assembly registrations.",
                rootPath));
        }

        var solutionName = BuildSolutionName(rootPath);
        diagnostics.Add(new CompilerDiagnostic(
            "code-first-registration-read",
            DataverseSolutionCompiler.Domain.Diagnostics.DiagnosticSeverity.Info,
            $"Code-first SDK registration reader projected {pluginAssemblyArtifacts.Count} plug-in assembly registration(s) into the canonical Dataverse model.",
            rootPath));

        return new CanonicalSolution(
            new SolutionIdentity(solutionName, solutionName, "1.0.0.0", LayeringIntent.Hybrid),
            new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler"),
            [
                new FamilyArtifact(ComponentFamily.SolutionShell, solutionName, solutionName, rootPath, EvidenceKind.Source),
                new FamilyArtifact(ComponentFamily.Publisher, "dsc", "Dataverse Solution Compiler", rootPath, EvidenceKind.Source),
                .. pluginAssemblyArtifacts.OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase),
                .. pluginTypeArtifacts.OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase),
                .. pluginStepArtifacts.OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase),
                .. pluginStepImageArtifacts.OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
            ],
            [],
            [],
            diagnostics);
    }

    public static bool IsProbableCodeFirstRegistrationRoot(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return false;
        }

        try
        {
            var root = ResolveInputRoot(inputPath);
            return EnumerateProjectPaths(root).Any()
                   && ContainsRegistrationMarkers(EnumerateSourceFiles(root));
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParsePluginAssemblyRegistration(
        SyntaxTree tree,
        string sourceFile,
        string rootPath,
        CodeProjectInfo projectInfo,
        IReadOnlyList<SyntaxNode> projectSyntaxRoots,
        string assetMapJson,
        ObjectCreationExpressionSyntax assemblyRegistration,
        ICollection<CompilerDiagnostic> diagnostics,
        out FamilyArtifact? assemblyArtifact,
        out IReadOnlyList<FamilyArtifact> typeArtifacts,
        out IReadOnlyList<FamilyArtifact> stepArtifacts,
        out IReadOnlyList<FamilyArtifact> imageArtifacts)
    {
        assemblyArtifact = null;
        typeArtifacts = [];
        stepArtifacts = [];
        imageArtifacts = [];

        var root = tree.GetRoot();
        var declaredTypes = DiscoverDeclaredTypes(projectSyntaxRoots);
        var enumValues = DiscoverEnumValues(projectSyntaxRoots);
        var baseContext = new ImperativeEvaluationContext(
            DiscoverGlobalStringConstants(projectSyntaxRoots),
            DiscoverGlobalIntConstants(projectSyntaxRoots, enumValues),
            new Dictionary<string, ImperativeMessageContext>(StringComparer.Ordinal),
            enumValues,
            declaredTypes,
            new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal));
        var properties = ReadObjectInitializerProperties(assemblyRegistration.Initializer);
        if (properties.Count == 0)
        {
            diagnostics.Add(CreateUnsupportedPatternDiagnostic(
                "code-first-registration-assembly-initializer-required",
                "DbmPluginAssemblyRegistration entries must use an object initializer with constant values.",
                sourceFile,
                assemblyRegistration.GetLocation()));
            return false;
        }

        var assemblyFullName = ReadRequiredString(tree, sourceFile, properties, "AssemblyFullName", baseContext, diagnostics);
        if (string.IsNullOrWhiteSpace(assemblyFullName))
        {
            return false;
        }

        var assemblyIdentity = ParseAssemblyIdentity(assemblyFullName);
        var assemblyName = assemblyIdentity.Name ?? assemblyFullName.Split(',', 2)[0].Trim();
        var assemblyFileName = ReadOptionalString(tree, sourceFile, properties, "AssemblyFileName", baseContext, diagnostics)
            ?? $"{assemblyName}.dll";
        var isolationMode = ReadOptionalInt(tree, sourceFile, properties, "IsolationMode", baseContext, diagnostics) ?? 2;
        var sourceType = ReadOptionalInt(tree, sourceFile, properties, "SourceType", baseContext, diagnostics) ?? 0;
        var introducedVersion = ReadOptionalString(tree, sourceFile, properties, "IntroducedVersion", baseContext, diagnostics) ?? "1.0";

        var typeRegistrations = ReadObjectArray(projectSyntaxRoots, tree, sourceFile, properties, "Types", "DbmPluginTypeRegistration", baseContext, diagnostics);
        var stepRegistrations = ReadObjectArray(projectSyntaxRoots, tree, sourceFile, properties, "Steps", "DbmPluginStepRegistration", baseContext, diagnostics, emitHelperDiagnostics: false);

        var metadataRelativePath = NormalizeRelativePath(Path.GetRelativePath(rootPath, sourceFile));
        var assemblySummaryJson = SerializeJson(new
        {
            fullName = assemblyFullName,
            fileName = assemblyFileName,
            isolationMode = isolationMode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            sourceType = sourceType.ToString(System.Globalization.CultureInfo.InvariantCulture),
            introducedVersion
        });

        assemblyArtifact = new FamilyArtifact(
            ComponentFamily.PluginAssembly,
            assemblyFullName,
            assemblyName,
            sourceFile,
            EvidenceKind.Source,
            CreateCodeFirstProperties(
                metadataRelativePath,
                assetMapJson,
                projectInfo,
                null,
                (ArtifactPropertyKeys.AssemblyFullName, assemblyFullName),
                (ArtifactPropertyKeys.AssemblyFileName, assemblyFileName),
                (ArtifactPropertyKeys.IsolationMode, isolationMode.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.SourceType, sourceType.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.IntroducedVersion, introducedVersion),
                (ArtifactPropertyKeys.SummaryJson, assemblySummaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(assemblySummaryJson))));

        var parsedTypes = new List<FamilyArtifact>();
        foreach (var typeRegistration in typeRegistrations)
        {
            var typeProperties = ReadObjectInitializerProperties(typeRegistration.ObjectCreation.Initializer);
            var logicalName = ReadRequiredString(tree, sourceFile, typeProperties, "LogicalName", typeRegistration.Context, diagnostics);
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            var friendlyName = ReadOptionalString(tree, sourceFile, typeProperties, "FriendlyName", typeRegistration.Context, diagnostics) ?? logicalName;
            var workflowActivityGroupName = ReadOptionalString(tree, sourceFile, typeProperties, "WorkflowActivityGroupName", typeRegistration.Context, diagnostics);
            var description = ReadOptionalString(tree, sourceFile, typeProperties, "Description", typeRegistration.Context, diagnostics);
            var assemblyQualifiedName = ReadOptionalString(tree, sourceFile, typeProperties, "AssemblyQualifiedName", typeRegistration.Context, diagnostics);
            var pluginTypeKind = ResolvePluginTypeKind(projectSyntaxRoots, logicalName, friendlyName, description, workflowActivityGroupName);
            var persistedWorkflowActivityGroupName = string.Equals(pluginTypeKind, "customWorkflowActivity", StringComparison.Ordinal)
                ? workflowActivityGroupName
                : null;
            var summaryJson = SerializeJson(new
            {
                typeName = logicalName,
                fullName = assemblyFullName,
                assemblyQualifiedName,
                pluginTypeKind
            });

            parsedTypes.Add(new FamilyArtifact(
                ComponentFamily.PluginType,
                logicalName,
                logicalName,
                sourceFile,
                EvidenceKind.Source,
                CreateCodeFirstProperties(
                    metadataRelativePath,
                    assetMapJson,
                    projectInfo,
                    logicalName,
                    (ArtifactPropertyKeys.AssemblyFullName, assemblyFullName),
                    (ArtifactPropertyKeys.AssemblyQualifiedName, assemblyQualifiedName),
                    (ArtifactPropertyKeys.PluginTypeKind, pluginTypeKind),
                    (ArtifactPropertyKeys.FriendlyName, friendlyName),
                    (ArtifactPropertyKeys.WorkflowActivityGroupName, persistedWorkflowActivityGroupName),
                    (ArtifactPropertyKeys.Description, description),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson)))));
        }

        var parsedSteps = new List<FamilyArtifact>();
        var parsedImages = new List<FamilyArtifact>();
        foreach (var stepRegistration in stepRegistrations)
        {
            var stepProperties = ReadObjectInitializerProperties(stepRegistration.ObjectCreation.Initializer);
            var stepName = ReadRequiredString(tree, sourceFile, stepProperties, "Name", stepRegistration.Context, diagnostics);
            var handlerPluginTypeName = ReadRequiredString(tree, sourceFile, stepProperties, "HandlerPluginTypeName", stepRegistration.Context, diagnostics);
            var messageName = ReadRequiredString(tree, sourceFile, stepProperties, "MessageName", stepRegistration.Context, diagnostics);
            var primaryEntity = NormalizeLogicalName(ReadRequiredString(tree, sourceFile, stepProperties, "PrimaryEntity", stepRegistration.Context, diagnostics));
            var stage = ReadOptionalInt(tree, sourceFile, stepProperties, "Stage", stepRegistration.Context, diagnostics)?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var mode = ReadOptionalInt(tree, sourceFile, stepProperties, "Mode", stepRegistration.Context, diagnostics)?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var rank = ReadOptionalInt(tree, sourceFile, stepProperties, "Rank", stepRegistration.Context, diagnostics)?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "1";
            var supportedDeployment = ReadOptionalInt(tree, sourceFile, stepProperties, "SupportedDeployment", stepRegistration.Context, diagnostics)?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0";
            var filteringAttributes = NormalizeAttributeList(ReadOptionalString(tree, sourceFile, stepProperties, "FilteringAttributes", stepRegistration.Context, diagnostics));
            var description = ReadOptionalString(tree, sourceFile, stepProperties, "Description", stepRegistration.Context, diagnostics);

            if (string.IsNullOrWhiteSpace(stepName)
                || string.IsNullOrWhiteSpace(handlerPluginTypeName)
                || string.IsNullOrWhiteSpace(messageName)
                || string.IsNullOrWhiteSpace(primaryEntity)
                || string.IsNullOrWhiteSpace(stage)
                || string.IsNullOrWhiteSpace(mode))
            {
                continue;
            }

            var stepLogicalName = BuildPluginStepLogicalName(handlerPluginTypeName, messageName, primaryEntity, stage, mode, stepName);
            var stepSummaryJson = SerializeJson(new
            {
                stepName,
                stage,
                mode,
                rank,
                supportedDeployment,
                messageName,
                sdkMessageId = string.Empty,
                primaryEntity,
                handlerPluginTypeName,
                filteringAttributes
            });

            parsedSteps.Add(new FamilyArtifact(
                ComponentFamily.PluginStep,
                stepLogicalName,
                stepName,
                sourceFile,
                EvidenceKind.Source,
                CreateCodeFirstProperties(
                    metadataRelativePath,
                    assetMapJson,
                    projectInfo,
                    stepLogicalName,
                    (ArtifactPropertyKeys.Description, description),
                    (ArtifactPropertyKeys.Stage, stage),
                    (ArtifactPropertyKeys.Mode, mode),
                    (ArtifactPropertyKeys.Rank, rank),
                    (ArtifactPropertyKeys.SupportedDeployment, supportedDeployment),
                    (ArtifactPropertyKeys.MessageName, messageName),
                    (ArtifactPropertyKeys.PrimaryEntity, primaryEntity),
                    (ArtifactPropertyKeys.HandlerPluginTypeName, handlerPluginTypeName),
                    (ArtifactPropertyKeys.FilteringAttributes, filteringAttributes),
                    (ArtifactPropertyKeys.SummaryJson, stepSummaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(stepSummaryJson)))));

            foreach (var imageRegistration in ReadObjectArray(projectSyntaxRoots, tree, sourceFile, stepProperties, "Images", "DbmPluginStepImageRegistration", stepRegistration.Context, diagnostics))
            {
                var imageProperties = ReadObjectInitializerProperties(imageRegistration.ObjectCreation.Initializer);
                var imageName = ReadRequiredString(tree, sourceFile, imageProperties, "Name", imageRegistration.Context, diagnostics);
                var entityAlias = NormalizeLogicalName(ReadRequiredString(tree, sourceFile, imageProperties, "EntityAlias", imageRegistration.Context, diagnostics));
                var imageType = ReadOptionalInt(tree, sourceFile, imageProperties, "ImageType", imageRegistration.Context, diagnostics)?.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var messagePropertyName = ReadRequiredString(tree, sourceFile, imageProperties, "MessagePropertyName", imageRegistration.Context, diagnostics);
                var selectedAttributes = NormalizeAttributeList(ReadOptionalString(tree, sourceFile, imageProperties, "SelectedAttributes", imageRegistration.Context, diagnostics));
                var imageDescription = ReadOptionalString(tree, sourceFile, imageProperties, "Description", imageRegistration.Context, diagnostics);

                if (string.IsNullOrWhiteSpace(imageName)
                    || string.IsNullOrWhiteSpace(entityAlias)
                    || string.IsNullOrWhiteSpace(imageType)
                    || string.IsNullOrWhiteSpace(messagePropertyName))
                {
                    continue;
                }

                var imageLogicalName = BuildPluginStepImageLogicalName(stepLogicalName, imageName, entityAlias, imageType);
                var imageSummaryJson = SerializeJson(new
                {
                    imageName,
                    parentStepLogicalName = stepLogicalName,
                    entityAlias,
                    imageType,
                    messagePropertyName,
                    selectedAttributes
                });

                parsedImages.Add(new FamilyArtifact(
                    ComponentFamily.PluginStepImage,
                    imageLogicalName,
                    imageName,
                    sourceFile,
                    EvidenceKind.Source,
                    CreateCodeFirstProperties(
                        metadataRelativePath,
                        assetMapJson,
                        projectInfo,
                        imageLogicalName,
                        (ArtifactPropertyKeys.Description, imageDescription),
                        (ArtifactPropertyKeys.ParentPluginStepLogicalName, stepLogicalName),
                        (ArtifactPropertyKeys.EntityAlias, entityAlias),
                        (ArtifactPropertyKeys.ImageType, imageType),
                        (ArtifactPropertyKeys.MessagePropertyName, messagePropertyName),
                        (ArtifactPropertyKeys.SelectedAttributes, selectedAttributes),
                        (ArtifactPropertyKeys.SummaryJson, imageSummaryJson),
                        (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(imageSummaryJson)))));
            }
        }

        var imperativeSteps = stepRegistrations.Count == 0
            ? ReadImperativeStepDefinitions(projectSyntaxRoots, root, sourceFile, properties, diagnostics)
            : [];
        foreach (var imperativeStep in imperativeSteps)
        {
            if (string.IsNullOrWhiteSpace(imperativeStep.Name)
                || string.IsNullOrWhiteSpace(imperativeStep.HandlerPluginTypeName)
                || string.IsNullOrWhiteSpace(imperativeStep.MessageName)
                || string.IsNullOrWhiteSpace(imperativeStep.PrimaryEntity)
                || string.IsNullOrWhiteSpace(imperativeStep.Stage)
                || string.IsNullOrWhiteSpace(imperativeStep.Mode))
            {
                continue;
            }

            var stepLogicalName = BuildPluginStepLogicalName(
                imperativeStep.HandlerPluginTypeName,
                imperativeStep.MessageName,
                imperativeStep.PrimaryEntity,
                imperativeStep.Stage,
                imperativeStep.Mode,
                imperativeStep.Name);
            var stepSummaryJson = SerializeJson(new
            {
                stepName = imperativeStep.Name,
                stage = imperativeStep.Stage,
                mode = imperativeStep.Mode,
                rank = imperativeStep.Rank,
                supportedDeployment = imperativeStep.SupportedDeployment,
                messageName = imperativeStep.MessageName,
                sdkMessageId = string.Empty,
                primaryEntity = imperativeStep.PrimaryEntity,
                handlerPluginTypeName = imperativeStep.HandlerPluginTypeName,
                filteringAttributes = imperativeStep.FilteringAttributes
            });

            parsedSteps.Add(new FamilyArtifact(
                ComponentFamily.PluginStep,
                stepLogicalName,
                imperativeStep.Name,
                sourceFile,
                EvidenceKind.Source,
                CreateCodeFirstProperties(
                    metadataRelativePath,
                    assetMapJson,
                    projectInfo,
                    stepLogicalName,
                    (ArtifactPropertyKeys.Description, imperativeStep.Description),
                    (ArtifactPropertyKeys.Stage, imperativeStep.Stage),
                    (ArtifactPropertyKeys.Mode, imperativeStep.Mode),
                    (ArtifactPropertyKeys.Rank, imperativeStep.Rank),
                    (ArtifactPropertyKeys.SupportedDeployment, imperativeStep.SupportedDeployment),
                    (ArtifactPropertyKeys.MessageName, imperativeStep.MessageName),
                    (ArtifactPropertyKeys.PrimaryEntity, imperativeStep.PrimaryEntity),
                    (ArtifactPropertyKeys.HandlerPluginTypeName, imperativeStep.HandlerPluginTypeName),
                    (ArtifactPropertyKeys.FilteringAttributes, imperativeStep.FilteringAttributes),
                    (ArtifactPropertyKeys.SummaryJson, stepSummaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(stepSummaryJson)))));

            foreach (var imperativeImage in imperativeStep.Images)
            {
                if (string.IsNullOrWhiteSpace(imperativeImage.Name)
                    || string.IsNullOrWhiteSpace(imperativeImage.EntityAlias)
                    || string.IsNullOrWhiteSpace(imperativeImage.ImageType)
                    || string.IsNullOrWhiteSpace(imperativeImage.MessagePropertyName))
                {
                    continue;
                }

                var imageLogicalName = BuildPluginStepImageLogicalName(
                    stepLogicalName,
                    imperativeImage.Name,
                    imperativeImage.EntityAlias,
                    imperativeImage.ImageType);
                var imageSummaryJson = SerializeJson(new
                {
                    imageName = imperativeImage.Name,
                    parentStepLogicalName = stepLogicalName,
                    entityAlias = imperativeImage.EntityAlias,
                    imageType = imperativeImage.ImageType,
                    messagePropertyName = imperativeImage.MessagePropertyName,
                    selectedAttributes = imperativeImage.SelectedAttributes
                });

                parsedImages.Add(new FamilyArtifact(
                    ComponentFamily.PluginStepImage,
                    imageLogicalName,
                    imperativeImage.Name,
                    sourceFile,
                    EvidenceKind.Source,
                    CreateCodeFirstProperties(
                        metadataRelativePath,
                        assetMapJson,
                        projectInfo,
                        imageLogicalName,
                        (ArtifactPropertyKeys.Description, imperativeImage.Description),
                        (ArtifactPropertyKeys.ParentPluginStepLogicalName, stepLogicalName),
                        (ArtifactPropertyKeys.EntityAlias, imperativeImage.EntityAlias),
                        (ArtifactPropertyKeys.ImageType, imperativeImage.ImageType),
                        (ArtifactPropertyKeys.MessagePropertyName, imperativeImage.MessagePropertyName),
                        (ArtifactPropertyKeys.SelectedAttributes, imperativeImage.SelectedAttributes),
                        (ArtifactPropertyKeys.SummaryJson, imageSummaryJson),
                        (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(imageSummaryJson)))));
            }
        }

        typeArtifacts = parsedTypes
            .GroupBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToArray();
        stepArtifacts = parsedSteps
            .GroupBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToArray();
        imageArtifacts = parsedImages
            .GroupBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToArray();
        return true;
    }

    private static IReadOnlyDictionary<string, string>? CreateCodeFirstProperties(
        string metadataRelativePath,
        string assetMapJson,
        CodeProjectInfo projectInfo,
        string? logicalName,
        params (string Key, string? Value)[] properties) =>
        CreateProperties(
            [
                (ArtifactPropertyKeys.MetadataSourcePath, metadataRelativePath),
                (ArtifactPropertyKeys.PackageRelativePath, metadataRelativePath),
                (ArtifactPropertyKeys.AssetSourceMapJson, assetMapJson),
                (ArtifactPropertyKeys.DeploymentFlavor, projectInfo.DeploymentFlavor.ToString()),
                (ArtifactPropertyKeys.CodeProjectPath, projectInfo.RelativePath),
                (ArtifactPropertyKeys.PackageId, projectInfo.PackageId),
                (ArtifactPropertyKeys.PackageUniqueName, projectInfo.PackageUniqueName),
                (ArtifactPropertyKeys.PackageVersion, projectInfo.Version),
                (ArtifactPropertyKeys.CodeRegistrationProvenanceJson, SerializeJson(new
                {
                    project = projectInfo.RelativePath,
                    sourceFile = metadataRelativePath,
                    deploymentFlavor = projectInfo.DeploymentFlavor.ToString(),
                    logicalName
                })),
                .. properties
            ]);

    private static string ResolveInputRoot(string inputPath)
    {
        if (Directory.Exists(inputPath))
        {
            return Path.GetFullPath(inputPath);
        }

        if (File.Exists(inputPath))
        {
            if (string.Equals(Path.GetExtension(inputPath), ".csproj", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(Path.GetFullPath(inputPath)) ?? Path.GetFullPath(inputPath);
            }

            if (string.Equals(Path.GetExtension(inputPath), ".cs", StringComparison.OrdinalIgnoreCase))
            {
                var fullPath = Path.GetFullPath(inputPath);
                var directory = Path.GetDirectoryName(fullPath);
                while (!string.IsNullOrWhiteSpace(directory))
                {
                    if (Directory.EnumerateFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly).Any())
                    {
                        return directory;
                    }

                    directory = Path.GetDirectoryName(directory);
                }

                return Path.GetDirectoryName(fullPath) ?? fullPath;
            }
        }

        return Path.GetFullPath(inputPath);
    }

    private static IEnumerable<string> EnumerateProjectPaths(string rootPath) =>
        Directory.EnumerateFiles(rootPath, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsIgnoredDirectory(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> EnumerateSourceFiles(string rootPath) =>
        Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsIgnoredDirectory(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

    private static bool IsIgnoredDirectory(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("/.git/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsRegistrationMarkers(IEnumerable<string> sourceFiles)
    {
        var hasAssemblyRegistrationMarker = false;
        var hasStepRegistrationMarker = false;
        var hasWorkflowActivityMarker = false;
        foreach (var sourceFile in sourceFiles)
        {
            var contents = File.ReadAllText(sourceFile);
            if (!hasAssemblyRegistrationMarker
                && contents.Contains(AssemblyRegistrationMarker, StringComparison.Ordinal))
            {
                hasAssemblyRegistrationMarker = true;
            }

            if (!hasWorkflowActivityMarker
                && contents.Contains(WorkflowActivityMarker, StringComparison.Ordinal))
            {
                hasWorkflowActivityMarker = true;
            }

            if (!hasStepRegistrationMarker
                && StepRegistrationMarkers.Any(marker => contents.Contains($"\"{marker}\"", StringComparison.Ordinal)))
            {
                hasStepRegistrationMarker = true;
            }
        }

        return hasAssemblyRegistrationMarker && (hasStepRegistrationMarker || hasWorkflowActivityMarker);
    }

    private static IReadOnlyList<SyntaxNode> LoadProjectSyntaxRoots(string projectPath)
    {
        var syntaxRoots = new List<SyntaxNode>();
        foreach (var closureProjectPath in GetProjectClosure(projectPath))
        {
            var projectDirectory = Path.GetDirectoryName(closureProjectPath);
            if (string.IsNullOrWhiteSpace(projectDirectory) || !Directory.Exists(projectDirectory))
            {
                continue;
            }

            foreach (var sourceFile in Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                         .Where(path => !IsIgnoredDirectory(path))
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                syntaxRoots.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile), path: sourceFile).GetRoot());
            }
        }

        return syntaxRoots;
    }

    private static CodeProjectInfo ReadProjectInfo(string rootPath, string projectPath)
    {
        var document = XDocument.Load(projectPath);
        var properties = document.Root?
            .Elements()
            .Where(element => element.Name.LocalName.Equals("PropertyGroup", StringComparison.OrdinalIgnoreCase))
            .Elements()
            .GroupBy(element => element.Name.LocalName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value?.Trim(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        var version = properties.GetValueOrDefault("Version")
            ?? properties.GetValueOrDefault("AssemblyVersion")
            ?? "1.0.0.0";
        var deploymentFlavor = ParseDeploymentFlavor(properties.GetValueOrDefault("DataversePluginDeploymentFlavor"));
        var packageId = properties.GetValueOrDefault("PackageId")
            ?? Path.GetFileNameWithoutExtension(projectPath);
        var packageUniqueName = properties.GetValueOrDefault("DataversePluginPackageUniqueName")
            ?? NormalizePackageUniqueName(packageId);

        return new CodeProjectInfo(
            Path.GetFullPath(projectPath),
            NormalizeRelativePath(Path.GetRelativePath(rootPath, projectPath)),
            version,
            deploymentFlavor,
            packageId,
            packageUniqueName);
    }

    private static CodeAssetDeploymentFlavor ParseDeploymentFlavor(string? value) =>
        value?.Trim() switch
        {
            var text when string.Equals(text, nameof(CodeAssetDeploymentFlavor.PluginPackage), StringComparison.OrdinalIgnoreCase) => CodeAssetDeploymentFlavor.PluginPackage,
            _ => CodeAssetDeploymentFlavor.ClassicAssembly
        };

    private static IReadOnlyList<CodeAssetMapEntry> CollectAssetMap(string rootPath, CodeProjectInfo projectInfo)
    {
        var projectDirectories = GetProjectClosure(projectInfo.FullPath)
            .Select(Path.GetDirectoryName)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return projectDirectories
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(path => !IsIgnoredDirectory(path))
                .Select(path => new CodeAssetMapEntry(
                    Path.GetFullPath(path),
                    NormalizeRelativePath(Path.GetRelativePath(rootPath, path)))))
            .Distinct()
            .OrderBy(entry => entry.PackageRelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> GetProjectClosure(string projectPath)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();
        Visit(projectPath);
        return ordered;

        void Visit(string currentPath)
        {
            var fullPath = Path.GetFullPath(currentPath);
            if (!visited.Add(fullPath))
            {
                return;
            }

            ordered.Add(fullPath);
            var document = XDocument.Load(fullPath);
            foreach (var projectReference in document.Root?
                         .Descendants()
                         .Where(element => element.Name.LocalName.Equals("ProjectReference", StringComparison.OrdinalIgnoreCase))
                         .Select(element => element.Attribute("Include")?.Value)
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Select(value => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fullPath) ?? string.Empty, value!)))
                         .Where(File.Exists)
                     ?? [])
            {
                Visit(projectReference);
            }
        }
    }

    private static string? ResolveOwningProjectPath(IEnumerable<string> projectPaths, string sourceFile) =>
        projectPaths
            .Where(projectPath =>
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(projectPath)) ?? string.Empty;
                var fullSourcePath = Path.GetFullPath(sourceFile);
                return fullSourcePath.StartsWith(directory, StringComparison.OrdinalIgnoreCase);
            })
            .OrderByDescending(projectPath => projectPath.Length)
            .FirstOrDefault();

    private static Dictionary<string, ExpressionSyntax> ReadObjectInitializerProperties(InitializerExpressionSyntax? initializer)
    {
        var properties = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);
        if (initializer is null)
        {
            return properties;
        }

        foreach (var assignment in initializer.Expressions.OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is IdentifierNameSyntax identifier)
            {
                properties[identifier.Identifier.ValueText] = assignment.Right;
            }
        }

        return properties;
    }

    private static IReadOnlyList<BoundObjectCreationExpression> ReadObjectArray(
        IReadOnlyList<SyntaxNode> projectSyntaxRoots,
        SyntaxTree tree,
        string sourceFile,
        IReadOnlyDictionary<string, ExpressionSyntax> properties,
        string propertyName,
        string expectedTypeName,
        ImperativeEvaluationContext context,
        ICollection<CompilerDiagnostic> diagnostics,
        bool emitHelperDiagnostics = true)
    {
        _ = tree;
        if (!properties.TryGetValue(propertyName, out var expression))
        {
            return [];
        }

        var items = ResolveCollectionItems(
            projectSyntaxRoots,
            sourceFile,
            propertyName,
            expression,
            context,
            diagnostics,
            emitHelperDiagnostics);

        var objects = new List<BoundObjectCreationExpression>();
        foreach (var item in items)
        {
            if (TryResolveBoundObjectCreationExpression(
                    projectSyntaxRoots,
                    sourceFile,
                    item,
                    expectedTypeName,
                    context,
                    diagnostics,
                    out var objectCreation))
            {
                objects.Add(objectCreation!);
                continue;
            }

            if (!emitHelperDiagnostics)
            {
                continue;
            }

            diagnostics.Add(CreateUnsupportedPatternDiagnostic(
                "code-first-registration-unsupported-collection-item",
                $"{propertyName} items must be '{expectedTypeName}' object initializers with constant values.",
                sourceFile,
                item.GetLocation()));
        }

        return objects;
    }

    private static IReadOnlyList<ExpressionSyntax> ResolveCollectionItems(
        IReadOnlyList<SyntaxNode> projectSyntaxRoots,
        string sourceFile,
        string propertyName,
        ExpressionSyntax expression,
        ImperativeEvaluationContext context,
        ICollection<CompilerDiagnostic> diagnostics,
        bool emitHelperDiagnostics)
    {
        if (TryGetCollectionItems(expression, out var directItems))
        {
            return directItems;
        }

        if (expression is not InvocationExpressionSyntax invocation)
        {
            return [];
        }

        var methodName = GetInvocationName(invocation);
        if (string.IsNullOrWhiteSpace(methodName))
        {
            return [];
        }

        var method = projectSyntaxRoots
            .SelectMany(root => root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Identifier.ValueText, methodName, StringComparison.Ordinal)
                && candidate.ParameterList.Parameters.Count == invocation.ArgumentList.Arguments.Count);
        if (method is null)
        {
            if (emitHelperDiagnostics)
            {
                diagnostics.Add(CreateUnsupportedPatternDiagnostic(
                    "code-first-registration-helper-unresolved",
                    $"Could not resolve the zero-argument helper method used for '{propertyName}'.",
                    sourceFile,
                    invocation.GetLocation()));
            }

            return [];
        }

        var methodContext = BindMethodContext(method, invocation, context);
        if (!TryResolveHelperReturnExpressions(method, out var returnExpressions))
        {
            if (emitHelperDiagnostics)
            {
                diagnostics.Add(CreateUnsupportedPatternDiagnostic(
                    "code-first-registration-helper-return-unsupported",
                    $"{propertyName} helper method '{methodName}' must return a direct collection expression, a reducible helper-local collection, or a simple yield-return sequence.",
                    sourceFile,
                    method.Identifier.GetLocation()));
            }

            return [];
        }

        var helperItems = new List<ExpressionSyntax>();
        foreach (var returnExpression in returnExpressions)
        {
            if (returnExpression is null)
            {
                continue;
            }

            if (TryGetCollectionItems(returnExpression, out var collectionItems))
            {
                helperItems.AddRange(collectionItems);
                continue;
            }

            helperItems.Add(returnExpression);
        }

        if (helperItems.Count > 0)
        {
            return helperItems;
        }

        if (emitHelperDiagnostics && returnExpressions.Length > 0)
        {
            diagnostics.Add(CreateUnsupportedPatternDiagnostic(
                "code-first-registration-helper-return-unsupported",
                $"{propertyName} helper method '{methodName}' must return a direct collection expression, a reducible helper-local collection, or a simple yield-return sequence.",
                sourceFile,
                returnExpressions[0]!.GetLocation()));
        }

        return [];
    }

    private static bool TryResolveHelperReturnExpressions(
        MethodDeclarationSyntax method,
        out ExpressionSyntax?[] returnExpressions)
    {
        if (method.ExpressionBody is not null)
        {
            returnExpressions = [method.ExpressionBody.Expression];
            return true;
        }

        if (method.Body is null)
        {
            returnExpressions = [];
            return false;
        }

        var yieldedExpressions = method.Body.Statements
            .OfType<YieldStatementSyntax>()
            .Where(statement => statement.Expression is not null && statement.Kind() == SyntaxKind.YieldReturnStatement)
            .Select(statement => statement.Expression!)
            .ToArray();
        if (yieldedExpressions.Length > 0)
        {
            returnExpressions = yieldedExpressions;
            return true;
        }

        var returnStatement = method.Body.Statements
            .OfType<ReturnStatementSyntax>()
            .FirstOrDefault(statement => statement.Expression is not null);
        if (returnStatement?.Expression is null)
        {
            returnExpressions = [];
            return false;
        }

        if (returnStatement.Expression is IdentifierNameSyntax identifier)
        {
            var localDeclaration = method.Body.Statements
                .OfType<LocalDeclarationStatementSyntax>()
                .SelectMany(statement => statement.Declaration.Variables)
                .LastOrDefault(variable =>
                    string.Equals(variable.Identifier.ValueText, identifier.Identifier.ValueText, StringComparison.Ordinal)
                    && variable.Initializer?.Value is not null);
            if (localDeclaration?.Initializer?.Value is { } initializer)
            {
                returnExpressions = [initializer];
                return true;
            }
        }

        returnExpressions = [returnStatement.Expression];
        return true;
    }

    private static bool TryGetCollectionItems(
        ExpressionSyntax expression,
        out IReadOnlyList<ExpressionSyntax> items)
    {
        items = expression switch
        {
            ImplicitArrayCreationExpressionSyntax implicitArray => implicitArray.Initializer.Expressions.ToArray(),
            ArrayCreationExpressionSyntax arrayCreation when arrayCreation.Initializer is not null => arrayCreation.Initializer.Expressions.ToArray(),
            CollectionExpressionSyntax collectionExpression => collectionExpression.Elements.OfType<ExpressionElementSyntax>().Select(element => element.Expression).ToArray(),
            InitializerExpressionSyntax initializer => initializer.Expressions.ToArray(),
            _ => Array.Empty<ExpressionSyntax>()
        };

        return items.Count > 0;
    }

    private static string? ReadRequiredString(
        SyntaxTree tree,
        string sourceFile,
        IReadOnlyDictionary<string, ExpressionSyntax> properties,
        string propertyName,
        ImperativeEvaluationContext context,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        var value = ReadOptionalString(tree, sourceFile, properties, propertyName, context, diagnostics);
        if (string.IsNullOrWhiteSpace(value))
        {
            diagnostics.Add(CreateDiagnostic(
                "code-first-registration-missing-required-value",
                DataverseSolutionCompiler.Domain.Diagnostics.DiagnosticSeverity.Error,
                $"'{propertyName}' must be a constant string value in the supported code-first registration subset.",
                sourceFile,
                tree.GetRoot().GetLocation()));
        }

        return value;
    }

    private static string? ReadOptionalString(
        SyntaxTree tree,
        string sourceFile,
        IReadOnlyDictionary<string, ExpressionSyntax> properties,
        string propertyName,
        ImperativeEvaluationContext context,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        _ = tree;
        if (!properties.TryGetValue(propertyName, out var expression))
        {
            return null;
        }

        return TryEvaluateStringExpression(expression, context, out var value)
            ? value
            : ReportUnsupportedScalar<string?>(
                diagnostics,
                "code-first-registration-unsupported-string-expression",
                $"'{propertyName}' must use a reducible string expression in the supported code-first registration subset.",
                sourceFile,
                expression.GetLocation(),
                null);
    }

    private static int? ReadOptionalInt(
        SyntaxTree tree,
        string sourceFile,
        IReadOnlyDictionary<string, ExpressionSyntax> properties,
        string propertyName,
        ImperativeEvaluationContext context,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        _ = tree;
        if (!properties.TryGetValue(propertyName, out var expression))
        {
            return null;
        }

        return TryEvaluateIntExpression(expression, context, out var value)
            ? value
            : ReportUnsupportedScalar<int?>(
                diagnostics,
                "code-first-registration-unsupported-int-expression",
                $"'{propertyName}' must use a reducible numeric expression in the supported code-first registration subset.",
                sourceFile,
                expression.GetLocation(),
                null);
    }

    private static bool TryResolveBoundObjectCreationExpression(
        IReadOnlyList<SyntaxNode> projectSyntaxRoots,
        string sourceFile,
        ExpressionSyntax expression,
        string expectedTypeName,
        ImperativeEvaluationContext context,
        ICollection<CompilerDiagnostic> diagnostics,
        out BoundObjectCreationExpression? boundObject)
    {
        if (expression is ObjectCreationExpressionSyntax directObjectCreation
            && string.Equals(GetTypeName(directObjectCreation.Type), expectedTypeName, StringComparison.Ordinal))
        {
            boundObject = new BoundObjectCreationExpression(directObjectCreation, context);
            return true;
        }

        if (expression is InvocationExpressionSyntax invocation
            && TryResolveMethodInvocation(projectSyntaxRoots, invocation, context, out var method, out var methodContext)
            && TryResolveHelperReturnExpressions(method!, out var returnExpressions))
        {
            foreach (var returnExpression in returnExpressions)
            {
                if (returnExpression is null)
                {
                    continue;
                }

                if (TryResolveBoundObjectCreationExpression(
                        projectSyntaxRoots,
                        sourceFile,
                        returnExpression,
                        expectedTypeName,
                        methodContext!,
                        diagnostics,
                        out boundObject))
                {
                    return true;
                }
            }
        }

        if (expression is IdentifierNameSyntax identifier
            && context.ParameterBindings.TryGetValue(identifier.Identifier.ValueText, out var parameterBinding)
            && TryResolveBoundObjectCreationExpression(projectSyntaxRoots, sourceFile, parameterBinding, expectedTypeName, context, diagnostics, out boundObject))
        {
            return true;
        }

        boundObject = null;
        return false;
    }

    private static bool TryResolveMethodInvocation(
        IReadOnlyList<SyntaxNode> projectSyntaxRoots,
        InvocationExpressionSyntax invocation,
        ImperativeEvaluationContext context,
        out MethodDeclarationSyntax? method,
        out ImperativeEvaluationContext? methodContext)
    {
        var methodName = GetInvocationName(invocation);
        if (string.IsNullOrWhiteSpace(methodName))
        {
            method = null;
            methodContext = null;
            return false;
        }

        method = projectSyntaxRoots
            .SelectMany(root => root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Identifier.ValueText, methodName, StringComparison.Ordinal)
                && candidate.ParameterList.Parameters.Count == invocation.ArgumentList.Arguments.Count);
        if (method is null)
        {
            methodContext = null;
            return false;
        }

        methodContext = BindMethodContext(method, invocation, context);
        return true;
    }

    private static ImperativeEvaluationContext BindMethodContext(
        MethodDeclarationSyntax method,
        InvocationExpressionSyntax invocation,
        ImperativeEvaluationContext parentContext)
    {
        var boundContext = parentContext with
        {
            Strings = new Dictionary<string, string?>(parentContext.Strings, StringComparer.Ordinal),
            Ints = new Dictionary<string, int>(parentContext.Ints, StringComparer.Ordinal),
            Messages = new Dictionary<string, ImperativeMessageContext>(parentContext.Messages, StringComparer.Ordinal),
            ParameterBindings = new Dictionary<string, ExpressionSyntax>(parentContext.ParameterBindings, StringComparer.Ordinal)
        };

        for (var index = 0; index < method.ParameterList.Parameters.Count; index++)
        {
            var parameter = method.ParameterList.Parameters[index];
            var argumentExpression = invocation.ArgumentList.Arguments[index].Expression;
            boundContext.ParameterBindings[parameter.Identifier.ValueText] = argumentExpression;

            if (TryEvaluateStringExpression(argumentExpression, parentContext, out var stringValue))
            {
                boundContext.Strings[parameter.Identifier.ValueText] = stringValue;
            }
            else if (TryEvaluateIntExpression(argumentExpression, parentContext, out var intValue))
            {
                boundContext.Ints[parameter.Identifier.ValueText] = intValue;
            }
        }

        if (method.Body is not null)
        {
            foreach (var local in method.Body.Statements
                         .OfType<LocalDeclarationStatementSyntax>()
                         .SelectMany(statement => statement.Declaration.Variables))
            {
                if (local.Initializer?.Value is not { } initializer)
                {
                    continue;
                }

                if (TryEvaluateStringExpression(initializer, boundContext, out var stringValue))
                {
                    boundContext.Strings[local.Identifier.ValueText] = stringValue;
                }
                else if (TryEvaluateIntExpression(initializer, boundContext, out var intValue))
                {
                    boundContext.Ints[local.Identifier.ValueText] = intValue;
                }
            }
        }

        return boundContext;
    }

    private static bool TryEvaluateStringExpression(ExpressionSyntax expression, out string? value)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                value = literal.Token.ValueText;
                return true;
            case InvocationExpressionSyntax invocation
                when invocation.Expression is IdentifierNameSyntax identifier
                     && string.Equals(identifier.Identifier.ValueText, "nameof", StringComparison.Ordinal)
                     && invocation.ArgumentList.Arguments.Count == 1:
                value = invocation.ArgumentList.Arguments[0].Expression.ToString().Split('.').LastOrDefault();
                return !string.IsNullOrWhiteSpace(value);
            case IdentifierNameSyntax identifier:
                return TryResolveConstString(expression.SyntaxTree.GetRoot(), identifier.Identifier.ValueText, out value);
            case MemberAccessExpressionSyntax memberAccess
                when string.Equals(memberAccess.Name.Identifier.ValueText, "FullName", StringComparison.Ordinal)
                     && memberAccess.Expression is TypeOfExpressionSyntax typeOfExpression:
                value = typeOfExpression.Type.ToString();
                return !string.IsNullOrWhiteSpace(value);
            default:
                value = null;
                return false;
        }
    }

    private static bool TryResolveConstString(
        SyntaxNode root,
        string identifier,
        out string? value)
    {
        foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            if (!field.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.ConstKeyword)))
            {
                continue;
            }

            foreach (var variable in field.Declaration.Variables)
            {
                if (!string.Equals(variable.Identifier.ValueText, identifier, StringComparison.Ordinal)
                    || variable.Initializer?.Value is not { } initializer
                    || initializer is IdentifierNameSyntax initializerIdentifier
                       && string.Equals(initializerIdentifier.Identifier.ValueText, identifier, StringComparison.Ordinal))
                {
                    continue;
                }

                if (TryEvaluateStringExpression(initializer, out value))
                {
                    return !string.IsNullOrWhiteSpace(value);
                }
            }
        }

        value = null;
        return false;
    }

    private static bool TryEvaluateIntExpression(ExpressionSyntax expression, out int value)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal when literal.Token.Value is int intValue:
                value = intValue;
                return true;
            case PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.UnaryMinusExpression)
                                                       && unary.Operand is LiteralExpressionSyntax literal
                                                       && literal.Token.Value is int negativeValue:
                value = -negativeValue;
                return true;
            case ObjectCreationExpressionSyntax objectCreation
                when string.Equals(GetTypeName(objectCreation.Type), "OptionSetValue", StringComparison.Ordinal)
                     && objectCreation.ArgumentList?.Arguments.Count == 1:
                return TryEvaluateIntExpression(objectCreation.ArgumentList.Arguments[0].Expression, out value);
            default:
                value = default;
                return false;
        }
    }

    private static T ReportUnsupportedScalar<T>(
        ICollection<CompilerDiagnostic> diagnostics,
        string code,
        string message,
        string sourceFile,
        Location location,
        T fallback)
    {
        diagnostics.Add(CreateUnsupportedPatternDiagnostic(code, message, sourceFile, location));
        return fallback;
    }

    private static CompilerDiagnostic CreateUnsupportedPatternDiagnostic(
        string code,
        string message,
        string sourceFile,
        Location location) =>
        CreateDiagnostic(code, DataverseSolutionCompiler.Domain.Diagnostics.DiagnosticSeverity.Error, message, sourceFile, location);

    private static CompilerDiagnostic CreateDiagnostic(
        string code,
        DataverseSolutionCompiler.Domain.Diagnostics.DiagnosticSeverity severity,
        string message,
        string sourceFile,
        Location location)
    {
        var lineSpan = location.GetLineSpan();
        var line = lineSpan.StartLinePosition.Line + 1;
        var column = lineSpan.StartLinePosition.Character + 1;
        return new CompilerDiagnostic(code, severity, message, $"{Path.GetFullPath(sourceFile)}:{line}:{column}");
    }

    private static string GetTypeName(TypeSyntax typeSyntax) =>
        typeSyntax switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualifiedName => qualifiedName.Right.Identifier.ValueText,
            GenericNameSyntax genericName => genericName.Identifier.ValueText,
            _ => typeSyntax.ToString().Split('.').Last()
        };

    private static IReadOnlyDictionary<string, string>? CreateProperties(params (string Key, string? Value)[] properties)
    {
        var dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in properties)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            dictionary[key] = value;
        }

        return dictionary.Count == 0 ? null : dictionary;
    }

    private static string NormalizeRelativePath(string path) =>
        path.Replace('\\', '/').TrimStart('/');

    private sealed record BoundObjectCreationExpression(
        ObjectCreationExpressionSyntax ObjectCreation,
        ImperativeEvaluationContext Context);

    private static string NormalizePackageUniqueName(string packageId)
    {
        var builder = new StringBuilder(packageId.Length);
        foreach (var character in packageId)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '_');
        }

        return builder.ToString();
    }

    private static string? NormalizeLogicalName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string? NormalizeAttributeList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var values = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeLogicalName)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values.Length == 0 ? null : string.Join(",", values);
    }

    private static string BuildSolutionName(string inputPath)
    {
        var rawName = Directory.Exists(inputPath)
            ? new DirectoryInfo(inputPath).Name
            : Path.GetFileNameWithoutExtension(inputPath);

        var normalized = new string(rawName.Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? "codefirstregistration" : normalized.ToLowerInvariant();
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
                primaryEntity ?? "*",
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
                entityAlias ?? "alias",
                imageType?.Trim() ?? "type"
            });

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

    private static string SerializeJson<T>(T value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    private static string ComputeSignature(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class PluginAssemblyIdentity
    {
        public string? Name { get; set; }
        public string? Version { get; set; }
        public string? Culture { get; set; }
        public string? PublicKeyToken { get; set; }
    }

    private sealed record CodeProjectInfo(
        string FullPath,
        string RelativePath,
        string Version,
        CodeAssetDeploymentFlavor DeploymentFlavor,
        string PackageId,
        string PackageUniqueName);

    private sealed record CodeAssetMapEntry(string SourcePath, string PackageRelativePath);
}
