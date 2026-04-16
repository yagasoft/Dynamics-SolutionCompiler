using Microsoft.Xrm.Sdk;

namespace Codex.Metadata.CodeFirst.WorkflowActivity.Classic;

public static class PluginRegistration
{
    public static readonly DbmPluginAssemblyRegistration Assembly = new DbmPluginAssemblyRegistration
    {
        AssemblyFullName = "Codex.Metadata.CodeFirst.WorkflowActivity.Classic, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9d006cbbfeff5098",
        AssemblyFileName = "Codex.Metadata.CodeFirst.WorkflowActivity.Classic.dll",
        IsolationMode = new OptionSetValue(2),
        SourceType = new OptionSetValue(0),
        IntroducedVersion = "1.0",
        Types =
        [
            new DbmPluginTypeRegistration
            {
                LogicalName = "Codex.Metadata.CodeFirst.WorkflowActivity.Classic.AccountDescriptionActivity",
                FriendlyName = "Account Description Activity",
                WorkflowActivityGroupName = "Codex.Metadata.CodeFirst.WorkflowActivity.Classic (1.0.0.0)",
                Description = "Classic custom workflow activity proof registration under the plug-in type lane.",
                AssemblyQualifiedName = "Codex.Metadata.CodeFirst.WorkflowActivity.Classic.AccountDescriptionActivity, Codex.Metadata.CodeFirst.WorkflowActivity.Classic, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9d006cbbfeff5098"
            }
        ],
        Steps = []
    };
}
