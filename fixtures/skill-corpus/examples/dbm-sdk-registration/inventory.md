# SDK Registration Source Inventory

- Source path: `C:\Git\Dynamics-BusinessMachine\DbmSolution\Plugins`
- Files scanned: `8`
- Matching files: `2`
- Step registrations: `1`
- Step image registrations: `1`

## Logical Name Mentions
- `connector`: `3`
- `pluginassembly`: `1`
- `plugintype`: `8`
- `sdkmessage`: `31`
- `sdkmessagefilter`: `6`
- `sdkmessageprocessingstep`: `9`
- `sdkmessageprocessingstepimage`: `2`
- `serviceendpoint`: `1`

## Step Registrations
- `C:\Git\Dynamics-BusinessMachine\DbmSolution\Plugins\Common.cs:9557`: name `stepName`, stage `new OptionSetValue((int)pluginConfig.Stage)`, rank `999`, message `new EntityReference("sdkmessage", message.MessageId)`, filter `new EntityReference("sdkmessagefilter", message.FilteredId)`, handler `new EntityReference("plugintype", message.PluginTypeId)`

## Step Image Registrations
- `C:\Git\Dynamics-BusinessMachine\DbmSolution\Plugins\Common.cs:9623`: name `"image"`, image type `new OptionSetValue(imageType)`, alias `"image"`, message property `pluginConfig.Message == "Create" ? "Id" : "Target"`, step `new EntityReference("sdkmessageprocessingstep", stepId)`

## Query Patterns
- `C:\Git\Dynamics-BusinessMachine\DbmSolution\Plugins\Common.cs:9683`: `queryexpression` -> `sdkmessageprocessingstep`
- `C:\Git\Dynamics-BusinessMachine\DbmSolution\Plugins\Common.cs:9551`: `fetch-clue` -> `service.Delete("sdkmessageprocessingstep", step.Id);`
- `C:\Git\Dynamics-BusinessMachine\DbmSolution\Plugins\Common.cs:9557`: `fetch-clue` -> `?? new Entity("sdkmessageprocessingstep")`
- `C:\Git\Dynamics-BusinessMachine\DbmSolution\Plugins\Common.cs:9563`: `fetch-clue` -> `["sdkmessageid"] = new EntityReference("sdkmessage", message.MessageId),`
- `C:\Git\Dynamics-BusinessMachine\DbmSolution\Plugins\Common.cs:9564`: `fetch-clue` -> `["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", message.FilteredId),`
- `C:\Git\Dynamics-BusinessMachine\DbmSolution\Plugins\Common.cs:9623`: `fetch-clue` -> `new Entity("sdkmessageprocessingstepimage")`
- `C:\Git\Dynamics-BusinessMachine\DbmSolution\Plugins\Common.cs:9629`: `fetch-clue` -> `["sdkmessageprocessingstepid"] = new EntityReference("sdkmessageprocessingstep", stepId)`
- `C:\Git\Dynamics-BusinessMachine\DbmSolution\Plugins\Common.cs:9634`: `fetch-clue` -> `private static SdkMessageInfo GetMessage(IOrganizationService service, string entityName, string messageName,`
- `C:\Git\Dynamics-BusinessMachine\DbmSolution\Plugins\Common.cs:9643`: `fetch-clue` -> `"  <entity name='sdkmessage' >" +`
- `C:\Git\Dynamics-BusinessMachine\DbmSolution\Plugins\Common.cs:9645`: `fetch-clue` -> `"    <attribute name='sdkmessageid' />" +`
- `C:\Git\Dynamics-BusinessMachine\DbmSolution\Plugins\Common.cs:9649`: `fetch-clue` -> `"    <link-entity name='sdkmessagefilter' from='sdkmessageid' to='sdkmessageid' alias='messagefilter' >" +`
- `C:\Git\Dynamics-BusinessMachine\DbmSolution\Plugins\Common.cs:9650`: `fetch-clue` -> `"      <attribute name='sdkmessagefilterid' />" +`
- `C:\Git\Dynamics-BusinessMachine\DbmSolution\Plugins\Common.cs:9661`: `fetch-clue` -> `new SdkMessageInfo`
- `C:\Git\Dynamics-BusinessMachine\DbmSolution\Plugins\Common.cs:9663`: `fetch-clue` -> `MessageId = e.GetAttributeValue<Guid>("sdkmessageid"),`
- `C:\Git\Dynamics-BusinessMachine\DbmSolution\Plugins\Common.cs:9666`: `fetch-clue` -> `(e.GetAttributeValue<AliasedValue>("messagefilter.sdkmessagefilterid")?.Value ?? Guid.Empty)`
- `C:\Git\Dynamics-BusinessMachine\DbmSolution\Plugins\Common.cs:9683`: `fetch-clue` -> `new QueryExpression("sdkmessageprocessingstep")`
- `C:\Git\Dynamics-BusinessMachine\DbmSolution\Plugins\Common.cs:9713`: `fetch-clue` -> `public class SdkMessageInfo`

## Mention Files
- `connector`: `1` file(s)
- `pluginassembly`: `1` file(s)
- `plugintype`: `2` file(s)
- `sdkmessage`: `2` file(s)
- `sdkmessagefilter`: `2` file(s)
- `sdkmessageprocessingstep`: `2` file(s)
- `sdkmessageprocessingstepimage`: `2` file(s)
- `serviceendpoint`: `1` file(s)
