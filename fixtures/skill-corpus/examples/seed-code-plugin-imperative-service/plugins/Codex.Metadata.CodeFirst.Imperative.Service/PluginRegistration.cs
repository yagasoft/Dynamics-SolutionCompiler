using System;
using Microsoft.Xrm.Sdk;

namespace Codex.Metadata.CodeFirst.Imperative.Service;

public static class PluginRegistration
{
    private const string StepEntityLogicalName = "sdkmessageprocessingstep";
    private const string StepImageEntityLogicalName = "sdkmessageprocessingstepimage";
    private const string HandlerPluginTypeName = "Codex.Metadata.CodeFirst.Imperative.Service.AccountCreateDescriptionPlugin";
    private const string MessageName = "Create";
    private const string PrimaryEntity = "account";

    private static readonly object? RegistrationService = null;

    public static readonly DbmPluginAssemblyRegistration Assembly = new DbmPluginAssemblyRegistration
    {
        AssemblyFullName = "Codex.Metadata.CodeFirst.Imperative.Service, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9d006cbbfeff5098",
        AssemblyFileName = "Codex.Metadata.CodeFirst.Imperative.Service.dll",
        IsolationMode = new OptionSetValue(2),
        SourceType = new OptionSetValue(0),
        IntroducedVersion = "1.0",
        Types =
        [
            new DbmPluginTypeRegistration
            {
                LogicalName = HandlerPluginTypeName,
                FriendlyName = "Imperative Service Account Create Description Plugin",
                WorkflowActivityGroupName = "Codex.Metadata.CodeFirst.Imperative.Service (1.0.0.0)",
                Description = "Imperative service-aware code-first proof plug-in that stamps account description on create.",
                AssemblyQualifiedName = "Codex.Metadata.CodeFirst.Imperative.Service.AccountCreateDescriptionPlugin, Codex.Metadata.CodeFirst.Imperative.Service, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9d006cbbfeff5098"
            }
        ],
        Steps = BuildImperativeSteps()
    };

    private static DbmPluginStepRegistration[] BuildImperativeSteps()
    {
        Entity? existingStep = null;
        var step = existingStep ?? new Entity(StepEntityLogicalName);
        var stepName = "Imperative Service Account Create Description Stamp";
        var stage = (int)SdkPluginStage.PreOperation;
        var imageType = 1;
        var message = GetMessage(RegistrationService, PrimaryEntity, MessageName, HandlerPluginTypeName);

        step["name"] = stepName;
        step["description"] = "Imperative service-aware proof step for sdkmessageprocessingstep.";
        step["stage"] = new OptionSetValue(stage);
        step["mode"] = new OptionSetValue(0);
        step["rank"] = 1;
        step["supporteddeployment"] = new OptionSetValue(0);
        step["filteringattributes"] = "name";
        step["sdkmessageid"] = new EntityReference("sdkmessage", message.MessageId);
        step["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", message.FilteredId);
        step["eventhandler"] = new EntityReference("plugintype", message.PluginTypeId);

        var image = new Entity(StepImageEntityLogicalName);
        image["name"] = "Imperative Service Account Step Image";
        image["description"] = "Imperative service-aware proof image for sdkmessageprocessingstepimage.";
        image["entityalias"] = "postimage";
        image["imagetype"] = new OptionSetValue(imageType);
        image["messagepropertyname"] = MessageName == "Create" ? "Id" : "Target";
        image["attributes"] = "name,description";

        return DbmImperativeRegistrationSupport.ToRegistrations(step, image, message);
    }

    private static SdkMessageInfo GetMessage(object? service, string primaryEntity, string messageName, string handlerPluginTypeName)
    {
        _ = service;
        return new SdkMessageInfo
        {
            MessageId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FilteredId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            PluginTypeId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            MessageName = messageName,
            PrimaryEntity = primaryEntity,
            HandlerPluginTypeName = handlerPluginTypeName
        };
    }
}
