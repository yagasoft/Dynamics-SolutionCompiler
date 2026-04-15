using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Azure.Core;
using Azure.Identity;

var environmentUrl = new Uri("https://ldv-rd-min.crm4.dynamics.com/");
var serviceRoot = new Uri($"{environmentUrl.ToString().TrimEnd('/')}/api/data/v9.2/");
var credential = new DefaultAzureCredential();
var token = await credential.GetTokenAsync(new TokenRequestContext([$"{environmentUrl.Scheme}://{environmentUrl.Host}/.default"]));

using var client = new HttpClient();
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
client.DefaultRequestHeaders.Add("OData-Version", "4.0");

if (args.Contains("cleanup-wave7-owned", StringComparer.OrdinalIgnoreCase))
{
    await DeleteWave7OwnedArtifactsAsync();
    return;
}

if (args.Contains("dump-wave7-owned", StringComparer.OrdinalIgnoreCase))
{
    await DumpWave7OwnedArtifactsAsync();
    return;
}

Console.WriteLine("Usage:");
Console.WriteLine("  dotnet run --project live-cleanup.csproj -- cleanup-wave7-owned");
Console.WriteLine("  dotnet run --project live-cleanup.csproj -- dump-wave7-owned");

async Task DeleteWave7OwnedArtifactsAsync()
{
    foreach (var logicalName in new[]
    {
        "cdxmeta_wave7checkpoint",
        "cdxmeta_wave7category",
        "cdxmeta_wave7photoasset",
        "cdxmeta_wave7workitem",
        "cdxmeta_checkpoint",
        "cdxmeta_category",
        "cdxmeta_photoasset",
        "cdxmeta_workitem"
    })
    {
        await TryDeleteAsync($"entity definition {logicalName}", () => DeleteEntityDefinitionIfExistsAsync(logicalName));
    }

    await TryDeleteAsync("global option set cdxmeta_priorityband", () => DeleteGlobalOptionSetIfExistsAsync("cdxmeta_priorityband"));
    await TryDeleteAsync("global option set cdxmeta_wave7priorityband", () => DeleteGlobalOptionSetIfExistsAsync("cdxmeta_wave7priorityband"));
    await TryDeleteAsync("global option set cdxmeta_wave7prioritybandb", () => DeleteGlobalOptionSetIfExistsAsync("cdxmeta_wave7prioritybandb"));
    await TryDeleteAsync("global option set cdxmeta_wave7prioritybandc", () => DeleteGlobalOptionSetIfExistsAsync("cdxmeta_wave7prioritybandc"));
    await TryDeleteAsync("global option set cdxmeta_wave7prioritybandd", () => DeleteGlobalOptionSetIfExistsAsync("cdxmeta_wave7prioritybandd"));
    await TryDeleteAsync("global option set cdxmeta_wave7prioritybande", () => DeleteGlobalOptionSetIfExistsAsync("cdxmeta_wave7prioritybande"));

    await TryDeleteAsync("environment variable cdxmeta_AdvancedUiMode", () => DeleteIfExistsAsync("environmentvariabledefinitions", "environmentvariabledefinitionid", "schemaname eq 'cdxmeta_AdvancedUiMode'"));
    await TryDeleteAsync("app module codex_metadata_advanced_ui_924e69cb", () => DeleteIfExistsAsync("appmodules", "appmoduleid", "uniquename eq 'codex_metadata_advanced_ui_924e69cb'"));
    await TryDeleteAsync("web resource icon.svg", () => DeleteIfExistsAsync("webresourceset", "webresourceid", "name eq 'cdxmeta_/advancedui/icon.svg'"));
    await TryDeleteAsync("web resource landing.html", () => DeleteIfExistsAsync("webresourceset", "webresourceid", "name eq 'cdxmeta_/advancedui/landing.html'"));
    await TryDeleteAsync("canvas app Overview", () => DeleteIfExistsAsync("canvasapps", "canvasappid", "name eq 'Overview'"));
    await TryDeleteAsync("service endpoint codex_webhook_endpoint", () => DeleteIfExistsAsync("serviceendpoints", "serviceendpointid", "name eq 'codex_webhook_endpoint'"));
    await TryDeleteAsync("connector codex_shared_connector", () => DeleteIfExistsAsync("connectors", "connectorid", "name eq 'codex_shared_connector'"));
    await TryDeleteAsync("routing rule Codex Metadata Routing Rule", () => DeleteIfExistsAsync("routingrules", "routingruleid", "name eq 'Codex Metadata Routing Rule'"));
    await TryDeleteAsync("mobile offline profile Codex Metadata Mobile Offline Profile", () => DeleteIfExistsAsync("mobileofflineprofiles", "mobileofflineprofileid", "name eq 'Codex Metadata Mobile Offline Profile'"));
    await TryDeleteAsync("field security profile Codex Metadata Seed Field Security", () => DeleteIfExistsAsync("fieldsecurityprofiles", "fieldsecurityprofileid", "name eq 'Codex Metadata Seed Field Security'"));
    await TryDeleteAsync("connection role Codex Metadata Seed Connection Role", () => DeleteIfExistsAsync("connectionroles", "connectionroleid", "name eq 'Codex Metadata Seed Connection Role'"));
    await TryDeleteAsync("role Codex Metadata Seed Role", () => DeleteIfExistsAsync("roles", "roleid", "name eq 'Codex Metadata Seed Role'"));
    await TryDeleteAsync("plugin step image Account PreImage", () => DeleteIfExistsAsync("sdkmessageprocessingstepimages", "sdkmessageprocessingstepimageid", "name eq 'Account PreImage'"));
    await TryDeleteAsync("plugin step Account Update Trace Step", () => DeleteIfExistsAsync("sdkmessageprocessingsteps", "sdkmessageprocessingstepid", "name eq 'Account Update Trace Step'"));
    await TryDeleteAsync("plugin type Codex.Metadata.Plugins.AccountUpdateTrace", () => DeleteIfExistsAsync("plugintypes", "plugintypeid", "typename eq 'Codex.Metadata.Plugins.AccountUpdateTrace'"));
    await TryDeleteAsync("plugin assembly Codex.Metadata.Plugins", () => DeleteIfExistsAsync("pluginassemblies", "pluginassemblyid", "name eq 'Codex.Metadata.Plugins'"));
}

async Task DumpWave7OwnedArtifactsAsync()
{
    await DumpAsync("Entities", "EntityDefinitions?$select=LogicalName,SchemaName,MetadataId&$filter=LogicalName eq 'cdxmeta_category' or LogicalName eq 'cdxmeta_checkpoint' or LogicalName eq 'cdxmeta_photoasset' or LogicalName eq 'cdxmeta_workitem' or LogicalName eq 'cdxmeta_wave7category' or LogicalName eq 'cdxmeta_wave7checkpoint' or LogicalName eq 'cdxmeta_wave7photoasset' or LogicalName eq 'cdxmeta_wave7workitem'");
    await DumpAsync("Global Option Sets", "GlobalOptionSetDefinitions?$select=Name,MetadataId");
    await DumpAsync("Environment Variables", "environmentvariabledefinitions?$select=environmentvariabledefinitionid,schemaname&$filter=schemaname eq 'cdxmeta_AdvancedUiMode'");
    await DumpAsync("App Modules", "appmodules?$select=appmoduleid,uniquename,name&$filter=uniquename eq 'codex_metadata_advanced_ui_924e69cb'");
    await DumpAsync("Canvas Apps", "canvasapps?$select=canvasappid,name&$filter=name eq 'Overview'");
    await DumpAsync("Service Endpoints", "serviceendpoints?$select=serviceendpointid,name&$filter=name eq 'codex_webhook_endpoint'");
    await DumpAsync("Connectors", "connectors?$select=connectorid,name,displayname&$filter=name eq 'codex_shared_connector'");
    await DumpAsync("Mobile Offline Profiles", "mobileofflineprofiles?$select=mobileofflineprofileid,name");
    await DumpAsync("Mobile Offline Profile Items", "mobileofflineprofileitems?$top=5");
}

async Task DumpAsync(string label, string relativePath)
{
    var uri = new Uri(serviceRoot, relativePath);
    using var response = await client.GetAsync(uri);
    var json = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"## {label}");
    Console.WriteLine($"{(int)response.StatusCode} {response.StatusCode}");
    Console.WriteLine(json);
    Console.WriteLine();
}

async Task TryDeleteAsync(string label, Func<Task> action)
{
    try
    {
        await action();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Skipping {label}: {ex.Message}");
    }
}

async Task DeleteIfExistsAsync(string entitySetName, string idColumn, string filter)
{
    var lookupUri = new Uri(serviceRoot, $"{entitySetName}?$select={idColumn}&$filter={filter}");
    using var lookupResponse = await client.GetAsync(lookupUri);
    if (lookupResponse.StatusCode == HttpStatusCode.NotFound)
    {
        return;
    }

    lookupResponse.EnsureSuccessStatusCode();
    var lookupJson = await lookupResponse.Content.ReadAsStringAsync();
    var node = JsonNode.Parse(lookupJson);
    var row = node?["value"]?.AsArray().OfType<JsonObject>().FirstOrDefault();
    var id = row?[idColumn]?.GetValue<string>();
    if (string.IsNullOrWhiteSpace(id))
    {
        return;
    }

    var deleteUri = new Uri(serviceRoot, $"{entitySetName}({id})");
    using var deleteResponse = await client.DeleteAsync(deleteUri);
    if (deleteResponse.StatusCode == HttpStatusCode.NotFound)
    {
        return;
    }

    if (!deleteResponse.IsSuccessStatusCode)
    {
        Console.WriteLine(await deleteResponse.Content.ReadAsStringAsync());
        deleteResponse.EnsureSuccessStatusCode();
    }

    Console.WriteLine($"Deleted {entitySetName} {id}");
}

async Task DeleteEntityDefinitionIfExistsAsync(string logicalName)
{
    var lookupUri = new Uri(serviceRoot, $"EntityDefinitions?$select=LogicalName,MetadataId&$filter=LogicalName eq '{logicalName}'");
    using var lookupResponse = await client.GetAsync(lookupUri);
    if (lookupResponse.StatusCode == HttpStatusCode.NotFound)
    {
        return;
    }

    lookupResponse.EnsureSuccessStatusCode();
    var lookupJson = await lookupResponse.Content.ReadAsStringAsync();
    var node = JsonNode.Parse(lookupJson);
    var row = node?["value"]?.AsArray().OfType<JsonObject>().FirstOrDefault();
    var metadataId = row?["MetadataId"]?.GetValue<string>();
    if (string.IsNullOrWhiteSpace(metadataId))
    {
        return;
    }

    var deleteUri = new Uri(serviceRoot, $"EntityDefinitions({metadataId})");
    using var deleteResponse = await client.DeleteAsync(deleteUri);
    if (deleteResponse.StatusCode == HttpStatusCode.NotFound)
    {
        return;
    }

    if (!deleteResponse.IsSuccessStatusCode)
    {
        Console.WriteLine($"Entity delete failed for {logicalName} using {deleteUri}");
        Console.WriteLine(await deleteResponse.Content.ReadAsStringAsync());
        deleteResponse.EnsureSuccessStatusCode();
    }

    Console.WriteLine($"Deleted entity definition {logicalName}");
}

async Task DeleteGlobalOptionSetIfExistsAsync(string name)
{
    var lookupUri = new Uri(serviceRoot, "GlobalOptionSetDefinitions?$select=Name,MetadataId");
    using var lookupResponse = await client.GetAsync(lookupUri);
    if (lookupResponse.StatusCode == HttpStatusCode.NotFound)
    {
        return;
    }

    lookupResponse.EnsureSuccessStatusCode();
    var lookupJson = await lookupResponse.Content.ReadAsStringAsync();
    var node = JsonNode.Parse(lookupJson);
    var row = node?["value"]?.AsArray().OfType<JsonObject>().FirstOrDefault(item =>
        string.Equals(item["Name"]?.GetValue<string>(), name, StringComparison.OrdinalIgnoreCase));
    var metadataId = row?["MetadataId"]?.GetValue<string>();
    if (string.IsNullOrWhiteSpace(metadataId))
    {
        return;
    }

    var deleteUri = new Uri(serviceRoot, $"GlobalOptionSetDefinitions({metadataId})");
    using var deleteResponse = await client.DeleteAsync(deleteUri);
    if (deleteResponse.StatusCode == HttpStatusCode.NotFound)
    {
        return;
    }

    if (!deleteResponse.IsSuccessStatusCode)
    {
        Console.WriteLine($"Global option set delete failed for {name} using {deleteUri}");
        Console.WriteLine(await deleteResponse.Content.ReadAsStringAsync());
        deleteResponse.EnsureSuccessStatusCode();
    }

    Console.WriteLine($"Deleted global option set {name}");
}
