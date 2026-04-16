namespace Codex.Metadata.CodeFirst.Helper;

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
