using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using DataverseSolutionCompiler.Cli;
using FluentAssertions;

namespace DataverseSolutionCompiler.E2ETests;

public sealed class CodeFirstPluginWorkflowProofTests
{
    private const string RepoRoot = @"C:\Git\Dataverse-Solution-KB";
    private static readonly string SeedCodePluginClassicPath = Path.Combine(
        RepoRoot,
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-code-plugin-classic");
    private static readonly string SeedCodePluginPackagePath = Path.Combine(
        RepoRoot,
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-code-plugin-package");
    private static readonly string SeedCodePluginImperativePath = Path.Combine(
        RepoRoot,
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-code-plugin-imperative");
    private static readonly string SeedCodePluginHelperPath = Path.Combine(
        RepoRoot,
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-code-plugin-helper");
    private static readonly string SeedCodePluginImperativeServicePath = Path.Combine(
        RepoRoot,
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-code-plugin-imperative-service");
    private static readonly string SeedCodeWorkflowActivityClassicPath = Path.Combine(
        RepoRoot,
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-code-workflow-activity-classic");

    [Fact]
    public async Task Apply_dev_classic_code_first_seed_stamps_account_description_when_environment_is_configured()
    {
        var settings = LoadSettingsOrSkip();
        if (!settings.IsEnabled)
        {
            return;
        }

        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-e2e-code-classic-{Guid.NewGuid():N}");

        try
        {
            RunCli(["emit", SeedCodePluginClassicPath, "--layout", "tracked-source", "--output", Path.Combine(outputRoot, "tracked-source")]);
            RunCli(["emit", SeedCodePluginClassicPath, "--layout", "intent-spec", "--output", Path.Combine(outputRoot, "intent-spec")]);
            RunCli(["apply-dev", SeedCodePluginClassicPath, "--environment", settings.EnvironmentUrl, "--solution", settings.ClassicSolutionUniqueName]);

            using var httpClient = await CreateDataverseClientAsync(settings.EnvironmentUrl);
            var accountId = await CreateAccountAsync(httpClient, $"codex-b014-classic-{Guid.NewGuid():N}");
            try
            {
                var description = await GetAccountDescriptionAsync(httpClient, accountId);
                description.Should().Be("B014-CLASSIC-PROOF");
            }
            finally
            {
                await DeleteAccountIfExistsAsync(httpClient, accountId);
            }
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Publish_plugin_package_seed_stamps_account_description_from_dependent_assembly_when_environment_is_configured()
    {
        var settings = LoadSettingsOrSkip();
        if (!settings.IsEnabled)
        {
            return;
        }

        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-e2e-code-package-{Guid.NewGuid():N}");

        try
        {
            RunCli(["emit", SeedCodePluginPackagePath, "--layout", "tracked-source", "--output", Path.Combine(outputRoot, "tracked-source")]);
            RunCli(["emit", SeedCodePluginPackagePath, "--layout", "intent-spec", "--output", Path.Combine(outputRoot, "intent-spec")]);
            RunCli([
                "publish",
                SeedCodePluginPackagePath,
                "--output",
                Path.Combine(outputRoot, "publish"),
                "--environment",
                settings.EnvironmentUrl,
                "--solution",
                settings.PackageSolutionUniqueName
            ]);
            RunCli(["readback", SeedCodePluginPackagePath, "--environment", settings.EnvironmentUrl, "--solution", settings.PackageSolutionUniqueName]);
            RunCli(["diff", SeedCodePluginPackagePath, "--environment", settings.EnvironmentUrl, "--solution", settings.PackageSolutionUniqueName]);

            using var httpClient = await CreateDataverseClientAsync(settings.EnvironmentUrl);
            var accountId = await CreateAccountAsync(httpClient, $"codex-b014-package-{Guid.NewGuid():N}");
            try
            {
                var description = await GetAccountDescriptionAsync(httpClient, accountId);
                description.Should().Be("B014-PACKAGE-PROOF-FROM-DEPENDENCY");
            }
            finally
            {
                await DeleteAccountIfExistsAsync(httpClient, accountId);
            }
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Apply_dev_imperative_code_first_seed_stamps_account_description_when_environment_is_configured()
    {
        var settings = LoadSettingsOrSkip();
        if (!settings.IsEnabled)
        {
            return;
        }

        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-e2e-code-imperative-{Guid.NewGuid():N}");

        try
        {
            RunCli(["emit", SeedCodePluginImperativePath, "--layout", "tracked-source", "--output", Path.Combine(outputRoot, "tracked-source")]);
            RunCli(["emit", SeedCodePluginImperativePath, "--layout", "intent-spec", "--output", Path.Combine(outputRoot, "intent-spec")]);
            RunCli(["apply-dev", SeedCodePluginImperativePath, "--environment", settings.EnvironmentUrl, "--solution", settings.ImperativeSolutionUniqueName]);

            using var httpClient = await CreateDataverseClientAsync(settings.EnvironmentUrl);
            var accountId = await CreateAccountAsync(httpClient, $"codex-b015-imperative-{Guid.NewGuid():N}");
            try
            {
                var description = await GetAccountDescriptionAsync(httpClient, accountId);
                description.Should().Be("B015-IMPERATIVE-PROOF");
            }
            finally
            {
                await DeleteAccountIfExistsAsync(httpClient, accountId);
            }
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Apply_dev_helper_code_first_seed_stamps_account_description_when_environment_is_configured()
    {
        var settings = LoadSettingsOrSkip();
        if (!settings.IsEnabled)
        {
            return;
        }

        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-e2e-code-helper-{Guid.NewGuid():N}");

        try
        {
            RunCli(["emit", SeedCodePluginHelperPath, "--layout", "tracked-source", "--output", Path.Combine(outputRoot, "tracked-source")]);
            RunCli(["emit", SeedCodePluginHelperPath, "--layout", "intent-spec", "--output", Path.Combine(outputRoot, "intent-spec")]);
            RunCli(["apply-dev", SeedCodePluginHelperPath, "--environment", settings.EnvironmentUrl, "--solution", settings.HelperSolutionUniqueName]);

            using var httpClient = await CreateDataverseClientAsync(settings.EnvironmentUrl);
            var accountId = await CreateAccountAsync(httpClient, $"codex-b016-helper-{Guid.NewGuid():N}");
            try
            {
                var description = await GetAccountDescriptionAsync(httpClient, accountId);
                description.Should().Be("B016-HELPER-PROOF");
            }
            finally
            {
                await DeleteAccountIfExistsAsync(httpClient, accountId);
            }
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Apply_dev_service_aware_imperative_code_first_seed_stamps_account_description_when_environment_is_configured()
    {
        var settings = LoadSettingsOrSkip();
        if (!settings.IsEnabled)
        {
            return;
        }

        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-e2e-code-imperative-service-{Guid.NewGuid():N}");

        try
        {
            RunCli(["emit", SeedCodePluginImperativeServicePath, "--layout", "tracked-source", "--output", Path.Combine(outputRoot, "tracked-source")]);
            RunCli(["emit", SeedCodePluginImperativeServicePath, "--layout", "intent-spec", "--output", Path.Combine(outputRoot, "intent-spec")]);
            RunCli(["apply-dev", SeedCodePluginImperativeServicePath, "--environment", settings.EnvironmentUrl, "--solution", settings.ImperativeServiceSolutionUniqueName]);

            using var httpClient = await CreateDataverseClientAsync(settings.EnvironmentUrl);
            var accountId = await CreateAccountAsync(httpClient, $"codex-b016-imperative-service-{Guid.NewGuid():N}");
            try
            {
                var description = await GetAccountDescriptionAsync(httpClient, accountId);
                description.Should().Be("B016-IMPERATIVE-SERVICE-PROOF");
            }
            finally
            {
                await DeleteAccountIfExistsAsync(httpClient, accountId);
            }
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Apply_dev_custom_workflow_activity_seed_registers_plugin_type_when_environment_is_configured()
    {
        var settings = LoadSettingsOrSkip();
        if (!settings.IsEnabled)
        {
            return;
        }

        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-e2e-code-workflow-activity-{Guid.NewGuid():N}");

        try
        {
            RunCli(["emit", SeedCodeWorkflowActivityClassicPath, "--layout", "tracked-source", "--output", Path.Combine(outputRoot, "tracked-source")]);
            RunCli(["emit", SeedCodeWorkflowActivityClassicPath, "--layout", "intent-spec", "--output", Path.Combine(outputRoot, "intent-spec")]);
            RunCli(["apply-dev", SeedCodeWorkflowActivityClassicPath, "--environment", settings.EnvironmentUrl, "--solution", settings.WorkflowActivitySolutionUniqueName]);
            RunCli(["readback", SeedCodeWorkflowActivityClassicPath, "--environment", settings.EnvironmentUrl, "--solution", settings.WorkflowActivitySolutionUniqueName]);
            RunCli(["diff", SeedCodeWorkflowActivityClassicPath, "--environment", settings.EnvironmentUrl, "--solution", settings.WorkflowActivitySolutionUniqueName]);

            using var httpClient = await CreateDataverseClientAsync(settings.EnvironmentUrl);
            var pluginType = await FindPluginTypeAsync(httpClient, "Codex.Metadata.CodeFirst.WorkflowActivity.Classic.AccountDescriptionActivity");
            pluginType.Should().NotBeNull();
            pluginType!.Value.workflowActivityGroupName.Should().Be("Codex.Metadata.CodeFirst.WorkflowActivity.Classic (1.0.0.0)");
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    private static EnvironmentSettings LoadSettingsOrSkip()
    {
        var environmentUrl = Environment.GetEnvironmentVariable("DSC_E2E_DATAVERSE_URL");
        if (string.IsNullOrWhiteSpace(environmentUrl))
        {
            return EnvironmentSettings.Disabled;
        }

        return new EnvironmentSettings(
            environmentUrl,
            Environment.GetEnvironmentVariable("DSC_E2E_CODE_PLUGIN_CLASSIC_SOLUTION") ?? "CodexMetadataSeedCodePluginClassic",
            Environment.GetEnvironmentVariable("DSC_E2E_CODE_PLUGIN_PACKAGE_SOLUTION") ?? "CodexMetadataSeedCodePluginPackage",
            Environment.GetEnvironmentVariable("DSC_E2E_CODE_PLUGIN_IMPERATIVE_SOLUTION") ?? "CodexMetadataSeedCodePluginImperative",
            Environment.GetEnvironmentVariable("DSC_E2E_CODE_PLUGIN_HELPER_SOLUTION") ?? "CodexMetadataSeedCodePluginHelper",
            Environment.GetEnvironmentVariable("DSC_E2E_CODE_PLUGIN_IMPERATIVE_SERVICE_SOLUTION") ?? "CodexMetadataSeedCodePluginImperativeService",
            Environment.GetEnvironmentVariable("DSC_E2E_CODE_WORKFLOW_ACTIVITY_SOLUTION") ?? "CodexMetadataSeedCodeWorkflowActivityClassic");
    }

    private static void RunCli(string[] arguments)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = CliApplication.Run(arguments, output, error);
        if (exitCode == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"CLI command failed with exit code {exitCode}: {string.Join(' ', arguments)}{Environment.NewLine}STDOUT:{Environment.NewLine}{output}{Environment.NewLine}STDERR:{Environment.NewLine}{error}");
    }

    private static async Task<HttpClient> CreateDataverseClientAsync(string environmentUrl)
    {
        var environmentUri = new Uri(environmentUrl, UriKind.Absolute);
        var scope = $"{environmentUri.GetLeftPart(UriPartial.Authority).TrimEnd('/')}/.default";
        var credential = new DefaultAzureCredential();
        var token = await credential.GetTokenAsync(new TokenRequestContext([scope]), CancellationToken.None);

        var client = new HttpClient
        {
            BaseAddress = new Uri(environmentUri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/")
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
        client.DefaultRequestHeaders.Add("OData-Version", "4.0");
        return client;
    }

    private static async Task<Guid> CreateAccountAsync(HttpClient client, string uniqueName)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/data/v9.2/accounts");
        request.Headers.Add("Prefer", "return=representation");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                name = uniqueName
            }),
            Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(responseText);
        return document.RootElement.GetProperty("accountid").GetGuid();
    }

    private static async Task<string?> GetAccountDescriptionAsync(HttpClient client, Guid accountId)
    {
        using var response = await client.GetAsync($"api/data/v9.2/accounts({accountId:D})?$select=description");
        var responseText = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(responseText);
        return document.RootElement.TryGetProperty("description", out var description)
            ? description.GetString()
            : null;
    }

    private static async Task DeleteAccountIfExistsAsync(HttpClient client, Guid accountId)
    {
        using var response = await client.DeleteAsync($"api/data/v9.2/accounts({accountId:D})");
        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    private static async Task<(string typeName, string? workflowActivityGroupName)?> FindPluginTypeAsync(HttpClient client, string typeName)
    {
        using var response = await client.GetAsync(
            $"api/data/v9.2/plugintypes?$select=typename,workflowactivitygroupname&$filter=typename eq '{typeName}'");
        var responseText = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(responseText);
        var values = document.RootElement.GetProperty("value");
        if (values.GetArrayLength() == 0)
        {
            return null;
        }

        var row = values[0];
        return (
            row.GetProperty("typename").GetString() ?? string.Empty,
            row.TryGetProperty("workflowactivitygroupname", out var workflowActivityGroupName)
                ? workflowActivityGroupName.GetString()
                : null);
    }

    private sealed record EnvironmentSettings(
        string EnvironmentUrl,
        string ClassicSolutionUniqueName,
        string PackageSolutionUniqueName,
        string ImperativeSolutionUniqueName,
        string HelperSolutionUniqueName,
        string ImperativeServiceSolutionUniqueName,
        string WorkflowActivitySolutionUniqueName)
    {
        public static EnvironmentSettings Disabled { get; } = new(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);

        public bool IsEnabled => !string.IsNullOrWhiteSpace(EnvironmentUrl);
    }
}
