using Microsoft.Xrm.Sdk;

namespace Codex.Metadata.CodeFirst.Helper;

public static class PluginRegistration
{
    private const string PluginTypeLogicalName = "Codex.Metadata.CodeFirst.Helper.AccountCreateDescriptionPlugin";
    private const string WorkflowActivityLogicalName = "Codex.Metadata.CodeFirst.Helper.AccountDescriptionActivity";

    public static readonly DbmPluginAssemblyRegistration Assembly = new DbmPluginAssemblyRegistration
    {
        AssemblyFullName = "Codex.Metadata.CodeFirst.Helper, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9d006cbbfeff5098",
        AssemblyFileName = "Codex.Metadata.CodeFirst.Helper.dll",
        IsolationMode = new OptionSetValue(2),
        SourceType = new OptionSetValue(0),
        IntroducedVersion = "1.0",
        Types = BuildTypes(),
        Steps = BuildSteps()
    };

    private static DbmPluginTypeRegistration[] BuildTypes()
    {
        DbmPluginTypeRegistration[] types =
        [
            new DbmPluginTypeRegistration
            {
                LogicalName = PluginTypeLogicalName,
                FriendlyName = "Account Create Description Helper Plugin",
                WorkflowActivityGroupName = "Codex.Metadata.CodeFirst.Helper (1.0.0.0)",
                Description = "Helper-based code-first proof plug-in that stamps account description on create.",
                AssemblyQualifiedName = "Codex.Metadata.CodeFirst.Helper.AccountCreateDescriptionPlugin, Codex.Metadata.CodeFirst.Helper, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9d006cbbfeff5098"
            },
            new DbmPluginTypeRegistration
            {
                LogicalName = WorkflowActivityLogicalName,
                FriendlyName = "Account Description Helper Activity",
                WorkflowActivityGroupName = "Codex.Metadata.CodeFirst.Helper (1.0.0.0)",
                Description = "Helper-based custom workflow activity proof registration under the plug-in type lane.",
                AssemblyQualifiedName = "Codex.Metadata.CodeFirst.Helper.AccountDescriptionActivity, Codex.Metadata.CodeFirst.Helper, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9d006cbbfeff5098"
            }
        ];

        return types;
    }

    private static DbmPluginStepRegistration[] BuildSteps()
    {
        DbmPluginStepRegistration[] steps =
        [
            new DbmPluginStepRegistration
            {
                Name = "Account Create Description Helper Stamp",
                HandlerPluginTypeName = PluginTypeLogicalName,
                MessageName = "Create",
                PrimaryEntity = "account",
                Stage = new OptionSetValue(20),
                Mode = new OptionSetValue(0),
                Rank = 1,
                SupportedDeployment = 0,
                FilteringAttributes = "name",
                Description = "Helper-based code-first proof step for sdkmessageprocessingstep.",
                Images = BuildImages()
            }
        ];

        return steps;
    }

    private static DbmPluginStepImageRegistration[] BuildImages()
    {
        DbmPluginStepImageRegistration[] images =
        [
            new DbmPluginStepImageRegistration
            {
                Name = "Account Helper Post Image",
                EntityAlias = "postimage",
                ImageType = new OptionSetValue(1),
                MessagePropertyName = "Target",
                SelectedAttributes = "name,description",
                Description = "Helper-based code-first proof image for sdkmessageprocessingstepimage."
            }
        ];

        return images;
    }
}
