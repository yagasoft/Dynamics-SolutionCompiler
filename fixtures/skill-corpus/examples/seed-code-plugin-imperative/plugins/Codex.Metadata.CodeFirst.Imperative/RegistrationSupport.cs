using System;
using Microsoft.Xrm.Sdk;

namespace Codex.Metadata.CodeFirst.Imperative;

public sealed class DbmPluginAssemblyRegistration
{
    public string? AssemblyFullName { get; set; }

    public string? AssemblyFileName { get; set; }

    public object? IsolationMode { get; set; }

    public object? SourceType { get; set; }

    public string? IntroducedVersion { get; set; }

    public DbmPluginTypeRegistration[]? Types { get; set; }

    public DbmPluginStepRegistration[]? Steps { get; set; }
}

public sealed class DbmPluginTypeRegistration
{
    public string? LogicalName { get; set; }

    public string? FriendlyName { get; set; }

    public string? WorkflowActivityGroupName { get; set; }

    public string? Description { get; set; }

    public string? AssemblyQualifiedName { get; set; }
}

public sealed class DbmPluginStepRegistration
{
    public string? Name { get; set; }

    public string? HandlerPluginTypeName { get; set; }

    public string? MessageName { get; set; }

    public string? PrimaryEntity { get; set; }

    public object? Stage { get; set; }

    public object? Mode { get; set; }

    public int Rank { get; set; }

    public int SupportedDeployment { get; set; }

    public string? FilteringAttributes { get; set; }

    public string? Description { get; set; }

    public DbmPluginStepImageRegistration[]? Images { get; set; }
}

public sealed class DbmPluginStepImageRegistration
{
    public string? Name { get; set; }

    public string? EntityAlias { get; set; }

    public object? ImageType { get; set; }

    public string? MessagePropertyName { get; set; }

    public string? SelectedAttributes { get; set; }

    public string? Description { get; set; }
}

public enum SdkPluginStage
{
    PreValidation = 10,
    PreOperation = 20,
    PostOperation = 40
}

public sealed class SdkMessageInfo
{
    public Guid MessageId { get; set; }

    public Guid FilteredId { get; set; }

    public Guid PluginTypeId { get; set; }

    public string? MessageName { get; set; }

    public string? PrimaryEntity { get; set; }

    public string? HandlerPluginTypeName { get; set; }
}

public static class DbmImperativeRegistrationSupport
{
    public static DbmPluginStepRegistration[] ToRegistrations(Entity step, Entity image, SdkMessageInfo message) =>
    [
        new DbmPluginStepRegistration
        {
            Name = step.GetAttributeValue<string>("name"),
            HandlerPluginTypeName = message.HandlerPluginTypeName,
            MessageName = message.MessageName,
            PrimaryEntity = message.PrimaryEntity,
            Stage = step.GetAttributeValue<OptionSetValue>("stage"),
            Mode = step.GetAttributeValue<OptionSetValue>("mode"),
            Rank = step.GetAttributeValue<int>("rank"),
            SupportedDeployment = step.GetAttributeValue<OptionSetValue>("supporteddeployment")?.Value ?? 0,
            FilteringAttributes = step.GetAttributeValue<string>("filteringattributes"),
            Description = step.GetAttributeValue<string>("description"),
            Images =
            [
                new DbmPluginStepImageRegistration
                {
                    Name = image.GetAttributeValue<string>("name"),
                    EntityAlias = image.GetAttributeValue<string>("entityalias"),
                    ImageType = image.GetAttributeValue<OptionSetValue>("imagetype"),
                    MessagePropertyName = image.GetAttributeValue<string>("messagepropertyname"),
                    SelectedAttributes = image.GetAttributeValue<string>("attributes"),
                    Description = image.GetAttributeValue<string>("description")
                }
            ]
        }
    ];
}
