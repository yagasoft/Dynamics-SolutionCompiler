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

if (args.Contains("delete-plugin-seed", StringComparer.OrdinalIgnoreCase))
{
    await DeleteIfExistsAsync("sdkmessageprocessingstepimages", "sdkmessageprocessingstepimageid", "name eq 'Account PreImage'");
    await DeleteIfExistsAsync("sdkmessageprocessingsteps", "sdkmessageprocessingstepid", "name eq 'Account Update Trace Step'");
    await DeleteIfExistsAsync("pluginassemblies", "pluginassemblyid", "name eq 'Codex.Metadata.Plugins'");
}

await DumpAsync("Assemblies", $"pluginassemblies?$select=pluginassemblyid,pluginassemblyidunique,name,version,culture,publickeytoken,path,createdon,modifiedon&$filter=name eq 'Codex.Metadata.Plugins'");
await DumpAsync("Types", $"plugintypes?$select=plugintypeid,typename,name,_pluginassemblyid_value,createdon,modifiedon&$filter=typename eq 'Codex.Metadata.Plugins.AccountUpdateTrace'");
await DumpAsync("Steps", $"sdkmessageprocessingsteps?$select=sdkmessageprocessingstepid,name,_eventhandler_value,createdon,modifiedon&$filter=name eq 'Account Update Trace Step'");
await DumpAsync("Images", $"sdkmessageprocessingstepimages?$select=sdkmessageprocessingstepimageid,name,_sdkmessageprocessingstepid_value,createdon,modifiedon&$filter=name eq 'Account PreImage'");
await DumpAsync("Recent Steps", "sdkmessageprocessingsteps?$select=sdkmessageprocessingstepid,name,_eventhandler_value,createdon,modifiedon&$orderby=createdon desc&$top=10");
await DumpAsync("Recent Images", "sdkmessageprocessingstepimages?$select=sdkmessageprocessingstepimageid,name,_sdkmessageprocessingstepid_value,createdon,modifiedon&$orderby=createdon desc&$top=10");

async Task DumpAsync(string label, string relativePath)
{
    var uri = new Uri(serviceRoot, relativePath);
    var json = await client.GetStringAsync(uri);
    Console.WriteLine($"## {label}");
    Console.WriteLine(json);
    Console.WriteLine();
}

async Task DeleteIfExistsAsync(string entitySetName, string idColumn, string filter)
{
    var lookupUri = new Uri(serviceRoot, $"{entitySetName}?$select={idColumn}&$filter={filter}");
    var lookupJson = await client.GetStringAsync(lookupUri);
    var node = JsonNode.Parse(lookupJson);
    var row = node?["value"]?.AsArray().OfType<JsonObject>().FirstOrDefault();
    var id = row?[idColumn]?.GetValue<string>();
    if (string.IsNullOrWhiteSpace(id))
    {
        return;
    }

    var deleteUri = new Uri(serviceRoot, $"{entitySetName}({id})");
    using var response = await client.DeleteAsync(deleteUri);
    response.EnsureSuccessStatusCode();
}
