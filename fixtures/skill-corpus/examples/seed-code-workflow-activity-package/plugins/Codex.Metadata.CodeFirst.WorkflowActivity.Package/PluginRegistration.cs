using Microsoft.Xrm.Sdk;

namespace Codex.Metadata.CodeFirst.WorkflowActivity.Package;

public static class PluginRegistration
{
    public static readonly DbmPluginAssemblyRegistration Assembly = new DbmPluginAssemblyRegistration
    {
        AssemblyFullName = "Codex.Metadata.CodeFirst.WorkflowActivity.Package, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9d006cbbfeff5098",
        AssemblyFileName = "Codex.Metadata.CodeFirst.WorkflowActivity.Package.dll",
        IsolationMode = new OptionSetValue(2),
        SourceType = new OptionSetValue(0),
        IntroducedVersion = "1.0",
        Types =
        [
            new DbmPluginTypeRegistration
            {
                LogicalName = "Codex.Metadata.CodeFirst.WorkflowActivity.Package.AccountDescriptionActivity",
                FriendlyName = "Account Description Activity",
                WorkflowActivityGroupName = "Codex.Metadata.CodeFirst.WorkflowActivity.Package (1.0.0.0)",
                Description = "Negative custom workflow activity package boundary registration.",
                AssemblyQualifiedName = "Codex.Metadata.CodeFirst.WorkflowActivity.Package.AccountDescriptionActivity, Codex.Metadata.CodeFirst.WorkflowActivity.Package, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9d006cbbfeff5098"
            }
        ],
        Steps = []
    };
}
