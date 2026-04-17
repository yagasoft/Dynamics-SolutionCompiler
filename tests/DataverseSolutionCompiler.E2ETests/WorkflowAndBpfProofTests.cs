using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Core;
using Azure.Identity;
using DataverseSolutionCompiler.Cli;
using FluentAssertions;

namespace DataverseSolutionCompiler.E2ETests;

public sealed class WorkflowAndBpfProofTests
{
    private const string RepoRoot = @"C:\Git\Dataverse-Solution-KB";
    private static readonly string SeedWorkflowClassicPath = Path.Combine(
        RepoRoot,
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-workflow-classic");
    private static readonly string SeedWorkflowActionPath = Path.Combine(
        RepoRoot,
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-workflow-action");
    private static readonly string SeedWorkflowBpfPath = Path.Combine(
        RepoRoot,
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-workflow-bpf");

    [Fact]
    public async Task Publish_classic_workflow_seed_stamps_account_when_runtime_proof_is_configured()
    {
        var settings = LoadSettingsOrSkip();
        if (!settings.IsEnabled || string.IsNullOrWhiteSpace(settings.WorkflowExpectedDescription))
        {
            return;
        }

        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-e2e-workflow-classic-{Guid.NewGuid():N}");

        try
        {
            RunCli([
                "publish",
                SeedWorkflowClassicPath,
                "--output",
                Path.Combine(outputRoot, "publish"),
                "--environment",
                settings.EnvironmentUrl,
                "--solution",
                settings.WorkflowSolutionUniqueName
            ]);
            RunCli(["readback", SeedWorkflowClassicPath, "--environment", settings.EnvironmentUrl, "--solution", settings.WorkflowSolutionUniqueName]);
            RunCli(["diff", SeedWorkflowClassicPath, "--environment", settings.EnvironmentUrl, "--solution", settings.WorkflowSolutionUniqueName]);

            using var httpClient = await CreateDataverseClientAsync(settings.EnvironmentUrl);
            var accountName = $"codex-b018-workflow-{Guid.NewGuid():N}";
            var accountId = await CreateAccountAsync(httpClient, accountName);
            try
            {
                var description = await WaitForAccountDescriptionAsync(httpClient, accountId);
                description.Should().Be(settings.WorkflowExpectedDescription);
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
    public async Task Publish_custom_action_seed_returns_expected_output_when_runtime_proof_is_configured()
    {
        var settings = LoadSettingsOrSkip();
        if (!settings.IsEnabled
            || string.IsNullOrWhiteSpace(settings.ActionExpectedOutputValue)
            || string.IsNullOrWhiteSpace(settings.ActionExpectedOutputName))
        {
            return;
        }

        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-e2e-workflow-action-{Guid.NewGuid():N}");

        try
        {
            RunCli([
                "publish",
                SeedWorkflowActionPath,
                "--output",
                Path.Combine(outputRoot, "publish"),
                "--environment",
                settings.EnvironmentUrl,
                "--solution",
                settings.ActionSolutionUniqueName
            ]);
            RunCli(["readback", SeedWorkflowActionPath, "--environment", settings.EnvironmentUrl, "--solution", settings.ActionSolutionUniqueName]);
            RunCli(["diff", SeedWorkflowActionPath, "--environment", settings.EnvironmentUrl, "--solution", settings.ActionSolutionUniqueName]);

            using var httpClient = await CreateDataverseClientAsync(settings.EnvironmentUrl);
            var accountName = $"codex-b018-action-{Guid.NewGuid():N}";
            var accountId = await CreateAccountAsync(httpClient, accountName);
            try
            {
                var route = (settings.ActionRoute ?? "api/data/v9.2/accounts({accountId})/Microsoft.Dynamics.CRM.cdxmeta_AccountStampAction")
                    .Replace("{accountId}", accountId.ToString("D"), StringComparison.OrdinalIgnoreCase)
                    .Replace("{accountName}", accountName, StringComparison.OrdinalIgnoreCase);
                var requestJson = (settings.ActionRequestJson ?? "{\"TargetName\":\"{accountName}\"}")
                    .Replace("{accountId}", accountId.ToString("D"), StringComparison.OrdinalIgnoreCase)
                    .Replace("{accountName}", accountName, StringComparison.OrdinalIgnoreCase);

                var response = await PostJsonAsync(httpClient, route, requestJson);
                response[settings.ActionExpectedOutputName!]?.GetValue<string>().Should().Be(settings.ActionExpectedOutputValue);
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
    public async Task Publish_single_table_bpf_seed_allows_runtime_stage_navigation_when_runtime_proof_is_configured()
    {
        var settings = LoadSettingsOrSkip();
        if (!settings.IsEnabled || !settings.EnableBpfRuntimeProof)
        {
            return;
        }

        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-e2e-workflow-bpf-{Guid.NewGuid():N}");

        try
        {
            RunCli([
                "publish",
                SeedWorkflowBpfPath,
                "--output",
                Path.Combine(outputRoot, "publish"),
                "--environment",
                settings.EnvironmentUrl,
                "--solution",
                settings.BpfSolutionUniqueName
            ]);
            RunCli(["readback", SeedWorkflowBpfPath, "--environment", settings.EnvironmentUrl, "--solution", settings.BpfSolutionUniqueName]);
            RunCli(["diff", SeedWorkflowBpfPath, "--environment", settings.EnvironmentUrl, "--solution", settings.BpfSolutionUniqueName]);

            using var httpClient = await CreateDataverseClientAsync(settings.EnvironmentUrl);
            var accountId = await CreateAccountAsync(httpClient, $"codex-b018-bpf-{Guid.NewGuid():N}");
            Guid? bpfInstanceId = null;

            try
            {
                var definition = await FindWorkflowAsync(httpClient, settings.BpfWorkflowUniqueName);
                definition.Should().NotBeNull();

                var runtimeLogicalName = definition!.UniqueName;
                var runtimeEntitySetName = await GetEntitySetNameAsync(httpClient, runtimeLogicalName);
                var stages = await GetProcessStagesAsync(httpClient, definition.WorkflowId);
                stages.Should().HaveCountGreaterThanOrEqualTo(2);

                var bindProperty = settings.BpfPrimaryBindProperty ?? $"bpf_{settings.BpfPrimaryEntityLogicalName}id";
                bpfInstanceId = await CreateBpfInstanceAsync(
                    httpClient,
                    runtimeEntitySetName,
                    bindProperty,
                    accountId,
                    stages[0].Id);

                var firstInstance = await GetBpfInstanceAsync(httpClient, runtimeEntitySetName, bpfInstanceId.Value);
                firstInstance["traversedpath"]?.GetValue<string>().Should().Contain(stages[0].Id.ToString("D"), Exactly.Once());
                NormalizeGuid(firstInstance["_activestageid_value"]?.GetValue<string>()).Should().Be(stages[0].Id.ToString("D"));

                var activePath = await GetActivePathAsync(httpClient, bpfInstanceId.Value);
                activePath.Select(stage => stage.Id).Should().ContainInOrder(stages[0].Id, stages[1].Id);

                await PatchJsonAsync(
                    httpClient,
                    $"api/data/v9.2/{runtimeEntitySetName}({bpfInstanceId.Value:D})",
                    new JsonObject
                    {
                        ["activestageid@odata.bind"] = $"/processstages({stages[1].Id:D})",
                        ["traversedpath"] = $"{stages[0].Id:D},{stages[1].Id:D}"
                    });

                var secondInstance = await GetBpfInstanceAsync(httpClient, runtimeEntitySetName, bpfInstanceId.Value);
                NormalizeGuid(secondInstance["_activestageid_value"]?.GetValue<string>()).Should().Be(stages[1].Id.ToString("D"));
                secondInstance["traversedpath"]?.GetValue<string>().Should().Be($"{stages[0].Id:D},{stages[1].Id:D}");
            }
            finally
            {
                if (bpfInstanceId.HasValue)
                {
                    var definition = await FindWorkflowAsync(httpClient, settings.BpfWorkflowUniqueName);
                    if (definition is not null)
                    {
                        var runtimeEntitySetName = await GetEntitySetNameAsync(httpClient, definition.UniqueName);
                        await DeleteIfExistsAsync(httpClient, $"api/data/v9.2/{runtimeEntitySetName}({bpfInstanceId.Value:D})");
                    }
                }

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

    private static WorkflowProofSettings LoadSettingsOrSkip()
    {
        var environmentUrl = Environment.GetEnvironmentVariable("DSC_E2E_DATAVERSE_URL");
        var enabled = string.Equals(
            Environment.GetEnvironmentVariable("DSC_E2E_WORKFLOW_XAML_ENABLE"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        if (!enabled || string.IsNullOrWhiteSpace(environmentUrl))
        {
            return WorkflowProofSettings.Disabled;
        }

        return new WorkflowProofSettings(
            environmentUrl,
            Environment.GetEnvironmentVariable("DSC_E2E_WORKFLOW_CLASSIC_SOLUTION") ?? "CodexMetadataSeedWorkflowClassic",
            Environment.GetEnvironmentVariable("DSC_E2E_WORKFLOW_ACTION_SOLUTION") ?? "CodexMetadataSeedWorkflowAction",
            Environment.GetEnvironmentVariable("DSC_E2E_WORKFLOW_BPF_SOLUTION") ?? "CodexMetadataSeedWorkflowBpf",
            Environment.GetEnvironmentVariable("DSC_E2E_WORKFLOW_CLASSIC_EXPECTED_DESCRIPTION"),
            Environment.GetEnvironmentVariable("DSC_E2E_WORKFLOW_ACTION_ROUTE"),
            Environment.GetEnvironmentVariable("DSC_E2E_WORKFLOW_ACTION_REQUEST_JSON"),
            Environment.GetEnvironmentVariable("DSC_E2E_WORKFLOW_ACTION_EXPECTED_OUTPUT_NAME") ?? "StampedDescription",
            Environment.GetEnvironmentVariable("DSC_E2E_WORKFLOW_ACTION_EXPECTED_OUTPUT_VALUE"),
            string.Equals(Environment.GetEnvironmentVariable("DSC_E2E_WORKFLOW_BPF_ENABLE"), "true", StringComparison.OrdinalIgnoreCase),
            Environment.GetEnvironmentVariable("DSC_E2E_WORKFLOW_BPF_UNIQUENAME") ?? "cdxmeta_accountsalesflow",
            Environment.GetEnvironmentVariable("DSC_E2E_WORKFLOW_BPF_PRIMARY_ENTITY") ?? "account",
            Environment.GetEnvironmentVariable("DSC_E2E_WORKFLOW_BPF_PRIMARY_BIND_PROPERTY"));
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

    private static async Task<string?> WaitForAccountDescriptionAsync(HttpClient client, Guid accountId)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var description = await GetAccountDescriptionAsync(client, accountId);
            if (!string.IsNullOrWhiteSpace(description))
            {
                return description;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        return await GetAccountDescriptionAsync(client, accountId);
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
        await DeleteIfExistsAsync(client, $"api/data/v9.2/accounts({accountId:D})");
    }

    private static async Task DeleteIfExistsAsync(HttpClient client, string relativePath)
    {
        using var response = await client.DeleteAsync(relativePath);
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    private static async Task<JsonObject> PostJsonAsync(HttpClient client, string relativePath, string requestJson)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, relativePath);
        request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        return string.IsNullOrWhiteSpace(responseText)
            ? new JsonObject()
            : JsonNode.Parse(responseText)?.AsObject() ?? new JsonObject();
    }

    private static async Task PatchJsonAsync(HttpClient client, string relativePath, JsonObject payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, relativePath)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
        };

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<WorkflowDefinition?> FindWorkflowAsync(HttpClient client, string workflowUniqueName)
    {
        using var response = await client.GetAsync(
            $"api/data/v9.2/workflows?$select=workflowid,uniquename,name&$filter=uniquename eq '{workflowUniqueName}'");
        var responseText = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(responseText);
        var values = document.RootElement.GetProperty("value");
        if (values.GetArrayLength() == 0)
        {
            return null;
        }

        var row = values[0];
        return new WorkflowDefinition(
            row.GetProperty("workflowid").GetGuid(),
            NormalizeLogicalName(row.GetProperty("uniquename").GetString()) ?? string.Empty,
            row.TryGetProperty("name", out var name) ? name.GetString() : null);
    }

    private static async Task<string> GetEntitySetNameAsync(HttpClient client, string logicalName)
    {
        using var response = await client.GetAsync(
            $"api/data/v9.2/EntityDefinitions(LogicalName='{logicalName}')?$select=EntitySetName");
        var responseText = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(responseText);
        return document.RootElement.GetProperty("EntitySetName").GetString()
            ?? throw new InvalidOperationException($"EntitySetName was missing for '{logicalName}'.");
    }

    private static async Task<IReadOnlyList<ProcessStageDefinition>> GetProcessStagesAsync(HttpClient client, Guid workflowId)
    {
        using var response = await client.GetAsync(
            $"api/data/v9.2/processstages?$select=processstageid,stagename&$filter=processid/workflowid eq {workflowId:D}");
        var responseText = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(responseText);
        return document.RootElement
            .GetProperty("value")
            .EnumerateArray()
            .Select(row => new ProcessStageDefinition(
                row.GetProperty("processstageid").GetGuid(),
                row.TryGetProperty("stagename", out var stageName) ? stageName.GetString() : null))
            .ToArray();
    }

    private static async Task<IReadOnlyList<ProcessStageDefinition>> GetActivePathAsync(HttpClient client, Guid processInstanceId)
    {
        using var response = await client.GetAsync(
            $"api/data/v9.2/RetrieveActivePath(ProcessInstanceId={processInstanceId:D})");
        var responseText = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(responseText);
        var values = document.RootElement.TryGetProperty("value", out var valueArray)
            ? valueArray
            : document.RootElement;

        return values.EnumerateArray()
            .Select(row => new ProcessStageDefinition(
                row.GetProperty("processstageid").GetGuid(),
                row.TryGetProperty("stagename", out var stageName) ? stageName.GetString() : null))
            .ToArray();
    }

    private static async Task<Guid> CreateBpfInstanceAsync(
        HttpClient client,
        string entitySetName,
        string primaryBindProperty,
        Guid accountId,
        Guid stageId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/data/v9.2/{entitySetName}");
        request.Headers.Add("Prefer", "return=representation");
        request.Content = new StringContent(
            new JsonObject
            {
                [$"{primaryBindProperty}@odata.bind"] = $"/accounts({accountId:D})",
                ["activestageid@odata.bind"] = $"/processstages({stageId:D})"
            }.ToJsonString(),
            Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        if (!string.IsNullOrWhiteSpace(responseText))
        {
            using var document = JsonDocument.Parse(responseText);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name.EndsWith("id", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String
                    && Guid.TryParse(property.Value.GetString(), out var parsedGuid))
                {
                    return parsedGuid;
                }
            }
        }

        if (response.Headers.TryGetValues("OData-EntityId", out var entityIdValues)
            && Uri.TryCreate(entityIdValues.Single(), UriKind.Absolute, out var entityIdUri))
        {
            var segment = entityIdUri.Segments.Last().Trim('/', '(', ')');
            if (Guid.TryParse(segment, out var parsedGuid))
            {
                return parsedGuid;
            }
        }

        throw new InvalidOperationException("BPF instance creation did not return a parseable record id.");
    }

    private static async Task<JsonObject> GetBpfInstanceAsync(HttpClient client, string entitySetName, Guid instanceId)
    {
        using var response = await client.GetAsync(
            $"api/data/v9.2/{entitySetName}({instanceId:D})?$select=traversedpath,_activestageid_value");
        var responseText = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        return JsonNode.Parse(responseText)?.AsObject()
            ?? throw new InvalidOperationException("BPF instance response was not a JSON object.");
    }

    private static string NormalizeGuid(string? value) =>
        Guid.TryParse(value?.Trim('{', '}'), out var guid) ? guid.ToString("D") : string.Empty;

    private static string? NormalizeLogicalName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private sealed record WorkflowDefinition(Guid WorkflowId, string UniqueName, string? Name);

    private sealed record ProcessStageDefinition(Guid Id, string? Name);

    private sealed record WorkflowProofSettings(
        string EnvironmentUrl,
        string WorkflowSolutionUniqueName,
        string ActionSolutionUniqueName,
        string BpfSolutionUniqueName,
        string? WorkflowExpectedDescription,
        string? ActionRoute,
        string? ActionRequestJson,
        string? ActionExpectedOutputName,
        string? ActionExpectedOutputValue,
        bool EnableBpfRuntimeProof,
        string BpfWorkflowUniqueName,
        string BpfPrimaryEntityLogicalName,
        string? BpfPrimaryBindProperty)
    {
        public static WorkflowProofSettings Disabled { get; } = new(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            null,
            null,
            null,
            false,
            string.Empty,
            string.Empty,
            null);

        public bool IsEnabled => !string.IsNullOrWhiteSpace(EnvironmentUrl);
    }
}
