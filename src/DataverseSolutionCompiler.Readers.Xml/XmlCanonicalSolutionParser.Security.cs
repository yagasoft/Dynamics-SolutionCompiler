using System.Globalization;
using System.Xml.Linq;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Readers.Xml;

internal sealed partial class XmlCanonicalSolutionParser
{
    private void ParseSecurityFamilies()
    {
        ParseRoles();
        ParseFieldSecurityProfiles();
        ParseConnectionRoles();
    }

    private void ParseRoles()
    {
        var rootDirectory = Path.Combine(_root, "Roles");
        if (!Directory.Exists(rootDirectory))
        {
            return;
        }

        foreach (var metadataPath in Directory.GetFiles(rootDirectory, "*.xml", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var root = LoadRoot(metadataPath);
            var displayName = root.AttributeValue("name") ?? Text(root.ElementLocal("Name")) ?? Path.GetFileNameWithoutExtension(metadataPath);
            var logicalName = NormalizeLogicalName(displayName);
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            var privileges = root.ElementLocal("RolePrivileges")
                ?.Elements()
                .Where(element => element.Name.LocalName.Equals("RolePrivilege", StringComparison.OrdinalIgnoreCase))
                .OrderBy(element => element.AttributeValue("name"), StringComparer.OrdinalIgnoreCase)
                .ThenBy(element => element.AttributeValue("level"), StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? [];

            var privilegeSummaryJson = SerializeJson(privileges.Select(element => new
            {
                name = element.AttributeValue("name"),
                level = element.AttributeValue("level")
            }).ToArray());
            var summaryJson = SerializeJson(new
            {
                logicalName,
                privilegeCount = privileges.Length
            });

            AddArtifact(
                ComponentFamily.Role,
                logicalName!,
                displayName,
                metadataPath,
                CreateProperties(
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(metadataPath)),
                    (ArtifactPropertyKeys.IsCustomizable, NormalizeBoolean(Text(root.ElementLocal("IsCustomizable")))),
                    (ArtifactPropertyKeys.PrivilegeCount, privileges.Length.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson)),
                    (ArtifactPropertyKeys.CapabilitiesJson, privilegeSummaryJson)));

            foreach (var privilegeElement in privileges)
            {
                var privilegeName = privilegeElement.AttributeValue("name");
                var accessLevel = privilegeElement.AttributeValue("level");
                if (string.IsNullOrWhiteSpace(privilegeName) || string.IsNullOrWhiteSpace(accessLevel))
                {
                    continue;
                }

                var privilegeLogicalName = $"{logicalName}|{privilegeName}|{accessLevel}";
                var privilegeSummary = SerializeJson(new
                {
                    logicalName = privilegeLogicalName,
                    parentRoleLogicalName = logicalName,
                    privilegeName,
                    accessLevel
                });

                AddArtifact(
                    ComponentFamily.RolePrivilege,
                    privilegeLogicalName,
                    privilegeName,
                    metadataPath,
                    CreateProperties(
                        (ArtifactPropertyKeys.ParentRoleLogicalName, logicalName),
                        (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(metadataPath)),
                        (ArtifactPropertyKeys.PrivilegeName, privilegeName),
                        (ArtifactPropertyKeys.AccessLevel, accessLevel),
                        (ArtifactPropertyKeys.SummaryJson, privilegeSummary),
                        (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(privilegeSummary))),
                    EvidenceKind.BestEffort);
            }
        }
    }

    private void ParseFieldSecurityProfiles()
    {
        var metadataPath = Path.Combine(_root, "Other", "FieldSecurityProfiles.xml");
        if (!File.Exists(metadataPath))
        {
            return;
        }

        var root = LoadRoot(metadataPath);
        var profiles = root.Elements()
            .Where(element => element.Name.LocalName.Equals("FieldSecurityProfile", StringComparison.OrdinalIgnoreCase))
            .OrderBy(element => element.AttributeValue("name"), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var profile in profiles)
        {
            var displayName = profile.AttributeValue("name") ?? Text(profile.ElementLocal("name"));
            var logicalName = NormalizeLogicalName(displayName);
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            var permissions = profile.ElementLocal("FieldPermissions")
                ?.Elements()
                .Where(element => element.Name.LocalName.Equals("FieldPermission", StringComparison.OrdinalIgnoreCase))
                .OrderBy(element => Text(element.ElementLocal("EntityName")), StringComparer.OrdinalIgnoreCase)
                .ThenBy(element => Text(element.ElementLocal("AttributeName")), StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? [];
            var summaryJson = SerializeJson(new
            {
                logicalName,
                permissionCount = permissions.Length
            });

            AddArtifact(
                ComponentFamily.FieldSecurityProfile,
                logicalName!,
                displayName,
                metadataPath,
                CreateProperties(
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(metadataPath)),
                    (ArtifactPropertyKeys.Description, Text(profile.ElementLocal("Description"))),
                    (ArtifactPropertyKeys.ItemCount, permissions.Length.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));

            foreach (var permission in permissions)
            {
                var entityLogicalName = NormalizeLogicalName(Text(permission.ElementLocal("EntityName")));
                var attributeLogicalName = NormalizeLogicalName(Text(permission.ElementLocal("AttributeName")));
                if (string.IsNullOrWhiteSpace(entityLogicalName) || string.IsNullOrWhiteSpace(attributeLogicalName))
                {
                    continue;
                }

                var permissionLogicalName = $"{logicalName}|{entityLogicalName}|{attributeLogicalName}";
                var permissionSummaryJson = SerializeJson(new
                {
                    logicalName = permissionLogicalName,
                    parentFieldSecurityProfileLogicalName = logicalName,
                    entityLogicalName,
                    attributeLogicalName,
                    canRead = Text(permission.ElementLocal("CanRead")),
                    canCreate = Text(permission.ElementLocal("CanCreate")),
                    canUpdate = Text(permission.ElementLocal("CanUpdate")),
                    canReadUnmasked = Text(permission.ElementLocal("CanReadUnmasked"))
                });

                AddArtifact(
                    ComponentFamily.FieldPermission,
                    permissionLogicalName,
                    $"{entityLogicalName}.{attributeLogicalName}",
                    metadataPath,
                    CreateProperties(
                        (ArtifactPropertyKeys.ParentFieldSecurityProfileLogicalName, logicalName),
                        (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(metadataPath)),
                        (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                        (ArtifactPropertyKeys.AttributeLogicalName, attributeLogicalName),
                        (ArtifactPropertyKeys.CanRead, Text(permission.ElementLocal("CanRead"))),
                        (ArtifactPropertyKeys.CanCreate, Text(permission.ElementLocal("CanCreate"))),
                        (ArtifactPropertyKeys.CanUpdate, Text(permission.ElementLocal("CanUpdate"))),
                        (ArtifactPropertyKeys.CanReadUnmasked, Text(permission.ElementLocal("CanReadUnmasked"))),
                        (ArtifactPropertyKeys.SummaryJson, permissionSummaryJson),
                        (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(permissionSummaryJson))));
            }
        }
    }

    private void ParseConnectionRoles()
    {
        var metadataPath = Path.Combine(_root, "Other", "ConnectionRoles.xml");
        if (!File.Exists(metadataPath))
        {
            return;
        }

        var root = LoadRoot(metadataPath);
        var roles = root.ElementLocal("ConnectionRoles")
            ?.Elements()
            .Where(element => element.Name.LocalName.Equals("ConnectionRole", StringComparison.OrdinalIgnoreCase))
            .OrderBy(element => Text(element.ElementLocal("name")), StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];

        foreach (var role in roles)
        {
            var displayName = Text(role.ElementLocal("name"));
            var logicalName = NormalizeLogicalName(displayName);
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            var objectTypeMappings = role.ElementLocal("ConnectionRoleObjectTypeCodes")
                ?.Elements()
                .Where(element => element.Name.LocalName.Equals("ConnectionRoleObjectTypeCode", StringComparison.OrdinalIgnoreCase))
                .Select(element => Text(element.ElementLocal("associatedobjecttypecode")))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? [];
            var summaryJson = SerializeJson(new
            {
                logicalName,
                category = Text(role.ElementLocal("category")),
                objectTypeMappings
            });

            AddArtifact(
                ComponentFamily.ConnectionRole,
                logicalName!,
                displayName,
                metadataPath,
                CreateProperties(
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(metadataPath)),
                    (ArtifactPropertyKeys.Category, Text(role.ElementLocal("category"))),
                    (ArtifactPropertyKeys.Description, Text(role.ElementLocal("description"))),
                    (ArtifactPropertyKeys.IsCustomizable, NormalizeBoolean(Text(role.ElementLocal("IsCustomizable")))),
                    (ArtifactPropertyKeys.IntroducedVersion, Text(role.ElementLocal("IntroducedVersion"))),
                    (ArtifactPropertyKeys.ObjectTypeMappingsJson, SerializeJson(objectTypeMappings)),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
        }
    }
}
