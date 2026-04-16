using System;
using Microsoft.Xrm.Sdk;
using Codex.Metadata.CodeFirst.Package.Dependency;

namespace Codex.Metadata.CodeFirst.Package;

public sealed class AccountCreatePackagePlugin : IPlugin
{
    public void Execute(IServiceProvider serviceProvider)
    {
        var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
        if (!string.Equals(context?.MessageName, "Create", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (context.InputParameters.TryGetValue("Target", out var targetValue)
            && targetValue is Entity target
            && string.Equals(target.LogicalName, "account", StringComparison.OrdinalIgnoreCase))
        {
            target["description"] = ProofMarkerProvider.Value;
        }
    }
}
