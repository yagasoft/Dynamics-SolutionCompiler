using Microsoft.Xrm.Sdk;

namespace Codex.Metadata.CodeFirst.Classic;

public static class PluginRegistration
{
    private const string StepEntityLogicalName = "sdkmessageprocessingstep";
    private const string StepImageEntityLogicalName = "sdkmessageprocessingstepimage";

    public static readonly DbmPluginAssemblyRegistration Assembly = new DbmPluginAssemblyRegistration
    {
        AssemblyFullName = "Codex.Metadata.CodeFirst.Classic, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9d006cbbfeff5098",
        AssemblyFileName = "Codex.Metadata.CodeFirst.Classic.dll",
        IsolationMode = new OptionSetValue(2),
        SourceType = new OptionSetValue(0),
        IntroducedVersion = "1.0",
        Types =
        [
            new DbmPluginTypeRegistration
            {
                LogicalName = "Codex.Metadata.CodeFirst.Classic.AccountCreateDescriptionPlugin",
                FriendlyName = "Account Create Description Plugin",
                WorkflowActivityGroupName = "Codex.Metadata.CodeFirst.Classic (1.0.0.0)",
                Description = "Classic assembly proof plug-in that stamps account description on create.",
                AssemblyQualifiedName = "Codex.Metadata.CodeFirst.Classic.AccountCreateDescriptionPlugin, Codex.Metadata.CodeFirst.Classic, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9d006cbbfeff5098"
            }
        ],
        Steps =
        [
            new DbmPluginStepRegistration
            {
                Name = "Account Create Description Stamp",
                HandlerPluginTypeName = "Codex.Metadata.CodeFirst.Classic.AccountCreateDescriptionPlugin",
                MessageName = "Create",
                PrimaryEntity = "account",
                Stage = new OptionSetValue(20),
                Mode = new OptionSetValue(0),
                Rank = 1,
                SupportedDeployment = 0,
                FilteringAttributes = "name",
                Description = "Classic assembly proof step for sdkmessageprocessingstep.",
                Images =
                [
                    new DbmPluginStepImageRegistration
                    {
                        Name = "Account Post Image",
                        EntityAlias = "postimage",
                        ImageType = new OptionSetValue(1),
                        MessagePropertyName = "Target",
                        SelectedAttributes = "name,description",
                        Description = "Classic assembly proof image for sdkmessageprocessingstepimage."
                    }
                ]
            }
        ]
    };
}
