using Microsoft.Xrm.Sdk;

namespace Codex.Metadata.CodeFirst.Package;

public static class PluginRegistration
{
    private const string StepEntityLogicalName = "sdkmessageprocessingstep";
    private const string StepImageEntityLogicalName = "sdkmessageprocessingstepimage";

    public static readonly DbmPluginAssemblyRegistration Assembly = new DbmPluginAssemblyRegistration
    {
        AssemblyFullName = "Codex.Metadata.CodeFirst.Package, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
        AssemblyFileName = "Codex.Metadata.CodeFirst.Package.dll",
        IsolationMode = new OptionSetValue(2),
        SourceType = new OptionSetValue(0),
        IntroducedVersion = "1.0",
        Types =
        [
            new DbmPluginTypeRegistration
            {
                LogicalName = "Codex.Metadata.CodeFirst.Package.AccountCreatePackagePlugin",
                FriendlyName = "Account Create Package Plugin",
                WorkflowActivityGroupName = "Codex.Metadata.CodeFirst.Package (1.0.0.0)",
                Description = "Plug-in package proof handler that reads its marker from a dependent assembly.",
                AssemblyQualifiedName = "Codex.Metadata.CodeFirst.Package.AccountCreatePackagePlugin, Codex.Metadata.CodeFirst.Package, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
            }
        ],
        Steps =
        [
            new DbmPluginStepRegistration
            {
                Name = "Account Create Package Stamp",
                HandlerPluginTypeName = "Codex.Metadata.CodeFirst.Package.AccountCreatePackagePlugin",
                MessageName = "Create",
                PrimaryEntity = "account",
                Stage = new OptionSetValue(20),
                Mode = new OptionSetValue(0),
                Rank = 1,
                SupportedDeployment = 0,
                FilteringAttributes = "name",
                Description = "Plug-in package proof step for sdkmessageprocessingstep.",
                Images =
                [
                    new DbmPluginStepImageRegistration
                    {
                        Name = "Account Post Image",
                        EntityAlias = "postimage",
                        ImageType = new OptionSetValue(1),
                        MessagePropertyName = "Target",
                        SelectedAttributes = "name,description",
                        Description = "Plug-in package proof image for sdkmessageprocessingstepimage."
                    }
                ]
            }
        ]
    };
}
