using System;
using Microsoft.Xrm.Sdk;

namespace Codex.Metadata.CodeFirst.Imperative;

public sealed class AccountCreateDescriptionPlugin : IPlugin
{
    internal const string ProofMarker = "B015-IMPERATIVE-PROOF";

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
            target["description"] = ProofMarker;
        }
    }
}
