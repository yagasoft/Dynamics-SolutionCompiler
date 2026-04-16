using System;
using Microsoft.Xrm.Sdk;

namespace Codex.Metadata.CodeFirst.Imperative;

public static class PluginRegistration
{
    private const string StepEntityLogicalName = "sdkmessageprocessingstep";
    private const string StepImageEntityLogicalName = "sdkmessageprocessingstepimage";
    private const string HandlerPluginTypeName = "Codex.Metadata.CodeFirst.Imperative.AccountCreateDescriptionPlugin";
    private const string MessageName = "Create";
    private const string PrimaryEntity = "account";

    public static readonly DbmPluginAssemblyRegistration Assembly = new DbmPluginAssemblyRegistration
    {
        AssemblyFullName = "Codex.Metadata.CodeFirst.Imperative, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9d006cbbfeff5098",
        AssemblyFileName = "Codex.Metadata.CodeFirst.Imperative.dll",
        IsolationMode = new OptionSetValue(2),
        SourceType = new OptionSetValue(0),
        IntroducedVersion = "1.0",
        Types =
        [
            new DbmPluginTypeRegistration
            {
                LogicalName = HandlerPluginTypeName,
                FriendlyName = "Imperative Account Create Description Plugin",
                Description = "Imperative code-first proof plug-in that stamps account description on create.",
                AssemblyQualifiedName = "Codex.Metadata.CodeFirst.Imperative.AccountCreateDescriptionPlugin, Codex.Metadata.CodeFirst.Imperative, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9d006cbbfeff5098"
            }
        ],
        Steps = BuildImperativeSteps()
    };

    private static DbmPluginStepRegistration[] BuildImperativeSteps()
    {
        Entity? existingStep = null;
        var step = existingStep ?? new Entity(StepEntityLogicalName);
        var stepName = "Imperative Account Create Description Stamp";
        var stage = (int)SdkPluginStage.PreOperation;
        var imageType = 1;
        var message = GetMessage(PrimaryEntity, MessageName, HandlerPluginTypeName);

        step["name"] = stepName;
        step["description"] = "Imperative classic assembly proof step for sdkmessageprocessingstep.";
        step["stage"] = new OptionSetValue(stage);
        step["mode"] = new OptionSetValue(0);
        step["rank"] = 1;
        step["supporteddeployment"] = new OptionSetValue(0);
        step["filteringattributes"] = "name";
        step["sdkmessageid"] = new EntityReference("sdkmessage", message.MessageId);
        step["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", message.FilteredId);
        step["eventhandler"] = new EntityReference("plugintype", message.PluginTypeId);

        var image = new Entity(StepImageEntityLogicalName);
        image["name"] = "Imperative Account Step Image";
        image["description"] = "Imperative classic assembly proof image for sdkmessageprocessingstepimage.";
        image["entityalias"] = "postimage";
        image["imagetype"] = new OptionSetValue(imageType);
        image["messagepropertyname"] = MessageName == "Create" ? "Id" : "Target";
        image["attributes"] = "name,description";

        return DbmImperativeRegistrationSupport.ToRegistrations(step, image, message);
    }

    private static SdkMessageInfo GetMessage(string primaryEntity, string messageName, string handlerPluginTypeName) =>
        new()
        {
            MessageId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FilteredId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            PluginTypeId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            MessageName = messageName,
            PrimaryEntity = primaryEntity,
            HandlerPluginTypeName = handlerPluginTypeName
        };
}
