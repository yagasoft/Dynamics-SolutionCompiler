using DataverseSolutionCompiler.Domain.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DataverseSolutionCompiler.Readers.Code;

public sealed partial class CodeFirstSdkRegistrationReader
{
    private const string DefaultPluginTypeKind = "plugin";
    private const string CustomWorkflowActivityPluginTypeKind = "customWorkflowActivity";

    private static string ResolvePluginTypeKind(
        IReadOnlyList<SyntaxNode> projectSyntaxRoots,
        string logicalName,
        string? friendlyName,
        string? description,
        string? workflowActivityGroupName)
    {
        if (string.IsNullOrWhiteSpace(logicalName)
            || string.IsNullOrWhiteSpace(friendlyName)
            || string.IsNullOrWhiteSpace(description)
            || string.IsNullOrWhiteSpace(workflowActivityGroupName))
        {
            return DefaultPluginTypeKind;
        }

        var declaredTypes = DiscoverDeclaredTypes(projectSyntaxRoots);
        if (TryGetDeclaredTypeMetadata(declaredTypes, logicalName, out var declaredType)
            && declaredType.IsCodeActivityDerived)
        {
            return CustomWorkflowActivityPluginTypeKind;
        }

        return DefaultPluginTypeKind;
    }

    private static IReadOnlyList<ImperativePluginStepDefinition> ReadImperativeStepDefinitions(
        IReadOnlyList<SyntaxNode> projectSyntaxRoots,
        SyntaxNode registrationRoot,
        string sourceFile,
        IReadOnlyDictionary<string, ExpressionSyntax> assemblyProperties,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        if (!assemblyProperties.TryGetValue("Steps", out var stepsExpression)
            || stepsExpression is not InvocationExpressionSyntax invocation)
        {
            return [];
        }

        if (invocation.ArgumentList.Arguments.Count != 0)
        {
            diagnostics.Add(CreateUnsupportedPatternDiagnostic(
                "code-first-registration-imperative-steps-arguments-unsupported",
                "Imperative DBM-style step registration currently supports only zero-argument helper methods.",
                sourceFile,
                invocation.GetLocation()));
            return [];
        }

        var methodName = GetInvocationName(invocation);
        if (string.IsNullOrWhiteSpace(methodName))
        {
            diagnostics.Add(CreateUnsupportedPatternDiagnostic(
                "code-first-registration-imperative-method-unresolved",
                "Could not resolve the zero-argument helper method used for imperative DBM-style step registration.",
                sourceFile,
                invocation.GetLocation()));
            return [];
        }

        var method = registrationRoot
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Identifier.ValueText, methodName, StringComparison.Ordinal)
                && candidate.ParameterList.Parameters.Count == 0);
        if (method?.Body is null)
        {
            diagnostics.Add(CreateUnsupportedPatternDiagnostic(
                "code-first-registration-imperative-method-body-required",
                $"Imperative DBM-style step helper '{methodName}' must be a zero-argument method with a block body.",
                sourceFile,
                invocation.GetLocation()));
            return [];
        }

        return ParseImperativeMethod(projectSyntaxRoots, sourceFile, method, diagnostics);
    }

    private static IReadOnlyList<ImperativePluginStepDefinition> ParseImperativeMethod(
        IReadOnlyList<SyntaxNode> projectSyntaxRoots,
        string sourceFile,
        MethodDeclarationSyntax method,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        var declaredTypes = DiscoverDeclaredTypes(projectSyntaxRoots);
        var enumValues = DiscoverEnumValues(projectSyntaxRoots);
        var globalStrings = DiscoverGlobalStringConstants(projectSyntaxRoots);
        var globalInts = DiscoverGlobalIntConstants(projectSyntaxRoots, enumValues);
        var strings = new Dictionary<string, string?>(globalStrings, StringComparer.Ordinal);
        var ints = new Dictionary<string, int>(globalInts, StringComparer.Ordinal);
        var messages = new Dictionary<string, ImperativeMessageContext>(StringComparer.Ordinal);
        var entities = new Dictionary<string, ImperativeEntityBuffer>(StringComparer.Ordinal);
        var stepBuffers = new List<ImperativeEntityBuffer>();
        var imageBuffers = new List<ImperativeEntityBuffer>();
        var context = new ImperativeEvaluationContext(strings, ints, messages, enumValues, declaredTypes, new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal));

        foreach (var statement in method.Body!.Statements)
        {
            switch (statement)
            {
                case LocalDeclarationStatementSyntax localDeclaration:
                    ParseImperativeLocalDeclaration(localDeclaration, sourceFile, context, entities, stepBuffers, imageBuffers, diagnostics);
                    break;
                case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }:
                    ParseImperativeAssignment(assignment, sourceFile, context, entities, diagnostics);
                    break;
            }
        }

        var resolvedStepBuffers = entities.Values
            .Where(entity => entity.Kind == ImperativeEntityKind.Step)
            .OrderBy(entity => entity.VariableName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var resolvedImageBuffers = entities.Values
            .Where(entity => entity.Kind == ImperativeEntityKind.Image)
            .OrderBy(entity => entity.VariableName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (resolvedStepBuffers.Length == 0)
        {
            diagnostics.Add(CreateUnsupportedPatternDiagnostic(
                "code-first-registration-imperative-step-none-supported",
                $"Imperative DBM-style step helper '{method.Identifier.ValueText}' did not contain a supported sdkmessageprocessingstep payload.",
                sourceFile,
                method.Identifier.GetLocation()));
            return [];
        }

        if (resolvedImageBuffers.Length > 0 && resolvedStepBuffers.Length > 1)
        {
            diagnostics.Add(CreateUnsupportedPatternDiagnostic(
                "code-first-registration-imperative-image-parent-ambiguous",
                $"Imperative DBM-style step helper '{method.Identifier.ValueText}' produced multiple step payloads; image-to-step binding is only supported when the helper emits one step.",
                sourceFile,
                method.Identifier.GetLocation()));
            resolvedImageBuffers = [];
        }

        var results = new List<ImperativePluginStepDefinition>();
        foreach (var stepBuffer in resolvedStepBuffers)
        {
            var messageContext = !string.IsNullOrWhiteSpace(stepBuffer.MessageContextName)
                && messages.TryGetValue(stepBuffer.MessageContextName, out var resolvedMessageContext)
                    ? resolvedMessageContext
                    : null;

            results.Add(new ImperativePluginStepDefinition(
                stepBuffer.Name,
                messageContext?.HandlerPluginTypeName,
                messageContext?.MessageName,
                NormalizeLogicalName(messageContext?.PrimaryEntity),
                stepBuffer.Stage.HasValue ? stepBuffer.Stage.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
                stepBuffer.Mode.HasValue ? stepBuffer.Mode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
                (stepBuffer.Rank ?? 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                (stepBuffer.SupportedDeployment ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
                NormalizeAttributeList(stepBuffer.FilteringAttributes),
                stepBuffer.Description,
                resolvedImageBuffers.Select(ToImperativeImageDefinition).ToArray()));
        }

        return results;
    }

    private static void ParseImperativeLocalDeclaration(
        LocalDeclarationStatementSyntax declaration,
        string sourceFile,
        ImperativeEvaluationContext context,
        IDictionary<string, ImperativeEntityBuffer> entities,
        ICollection<ImperativeEntityBuffer> stepBuffers,
        ICollection<ImperativeEntityBuffer> imageBuffers,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        foreach (var variable in declaration.Declaration.Variables)
        {
            if (variable.Initializer?.Value is not { } initializer)
            {
                continue;
            }

            var variableName = variable.Identifier.ValueText;
            if (TryCreateImperativeEntityBuffer(initializer, context, out var entityBuffer))
            {
                entityBuffer = entityBuffer with { VariableName = variableName };
                entities[variableName] = entityBuffer;
                if (entityBuffer.Kind == ImperativeEntityKind.Step)
                {
                    stepBuffers.Add(entityBuffer);
                }
                else
                {
                    imageBuffers.Add(entityBuffer);
                }

                continue;
            }

            if (TryEvaluateImperativeMessageContext(initializer, context, out var messageContext))
            {
                context.Messages[variableName] = messageContext!;
                continue;
            }

            if (TryEvaluateStringExpression(initializer, context, out var stringValue))
            {
                context.Strings[variableName] = stringValue;
                continue;
            }

            if (TryEvaluateIntExpression(initializer, context, out var intValue))
            {
                context.Ints[variableName] = intValue;
                continue;
            }

            if (initializer is InvocationExpressionSyntax invocation
                && string.Equals(GetInvocationName(invocation), "GetMessage", StringComparison.Ordinal))
            {
                diagnostics.Add(CreateUnsupportedPatternDiagnostic(
                    "code-first-registration-imperative-message-context-unsupported",
                    "Imperative DBM-style GetMessage(...) calls must use constant string arguments for primary entity, message name, and handler type name.",
                    sourceFile,
                    initializer.GetLocation()));
            }
        }
    }

    private static void ParseImperativeAssignment(
        AssignmentExpressionSyntax assignment,
        string sourceFile,
        ImperativeEvaluationContext context,
        IDictionary<string, ImperativeEntityBuffer> entities,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        if (assignment.Left is IdentifierNameSyntax identifier)
        {
            if (TryEvaluateImperativeMessageContext(assignment.Right, context, out var messageContext))
            {
                context.Messages[identifier.Identifier.ValueText] = messageContext!;
                return;
            }

            if (TryEvaluateStringExpression(assignment.Right, context, out var stringValue))
            {
                context.Strings[identifier.Identifier.ValueText] = stringValue;
                return;
            }

            if (TryEvaluateIntExpression(assignment.Right, context, out var intValue))
            {
                context.Ints[identifier.Identifier.ValueText] = intValue;
            }

            return;
        }

        if (!TryGetEntityAssignmentTarget(assignment.Left, context, out var variableName, out var propertyName)
            || !entities.TryGetValue(variableName, out var entity))
        {
            return;
        }

        entity = ApplyImperativeEntityAssignment(entity, propertyName, assignment.Right, sourceFile, context, diagnostics);
        entities[variableName] = entity;
    }

    private static ImperativeEntityBuffer ApplyImperativeEntityAssignment(
        ImperativeEntityBuffer entity,
        string propertyName,
        ExpressionSyntax expression,
        string sourceFile,
        ImperativeEvaluationContext context,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        switch (propertyName)
        {
            case "name":
                return entity with
                {
                    Name = ReadImperativeStringProperty("name", expression, sourceFile, context, diagnostics)
                };
            case "description":
                return entity with
                {
                    Description = ReadImperativeStringProperty("description", expression, sourceFile, context, diagnostics)
                };
            case "stage":
                return entity with
                {
                    Stage = ReadImperativeIntProperty("stage", expression, sourceFile, context, diagnostics)
                };
            case "mode":
                return entity with
                {
                    Mode = ReadImperativeIntProperty("mode", expression, sourceFile, context, diagnostics)
                };
            case "rank":
                return entity with
                {
                    Rank = ReadImperativeIntProperty("rank", expression, sourceFile, context, diagnostics)
                };
            case "supporteddeployment":
                return entity with
                {
                    SupportedDeployment = ReadImperativeIntProperty("supporteddeployment", expression, sourceFile, context, diagnostics)
                };
            case "filteringattributes":
                return entity with
                {
                    FilteringAttributes = ReadImperativeStringProperty("filteringattributes", expression, sourceFile, context, diagnostics)
                };
            case "entityalias":
                return entity with
                {
                    EntityAlias = NormalizeLogicalName(ReadImperativeStringProperty("entityalias", expression, sourceFile, context, diagnostics))
                };
            case "imagetype":
                return entity with
                {
                    ImageType = ReadImperativeIntProperty("imagetype", expression, sourceFile, context, diagnostics)
                };
            case "messagepropertyname":
                return entity with
                {
                    MessagePropertyName = ReadImperativeStringProperty("messagepropertyname", expression, sourceFile, context, diagnostics)
                };
            case "attributes":
                return entity with
                {
                    SelectedAttributes = ReadImperativeStringProperty("attributes", expression, sourceFile, context, diagnostics)
                };
            case "sdkmessageid":
            case "sdkmessagefilterid":
            case "eventhandler":
                if (TryResolveMessageContextReference(expression, out var messageContextName))
                {
                    return entity with { MessageContextName = messageContextName };
                }

                diagnostics.Add(CreateUnsupportedPatternDiagnostic(
                    "code-first-registration-imperative-reference-unsupported",
                    $"Imperative DBM-style '{propertyName}' assignments must reference GetMessage(...) results through EntityReference values.",
                    sourceFile,
                    expression.GetLocation()));
                return entity;
            default:
                return entity;
        }
    }

    private static string? ReadImperativeStringProperty(
        string propertyName,
        ExpressionSyntax expression,
        string sourceFile,
        ImperativeEvaluationContext context,
        ICollection<CompilerDiagnostic> diagnostics) =>
        TryEvaluateStringExpression(expression, context, out var value)
            ? value
            : ReportUnsupportedScalar<string?>(
                diagnostics,
                "code-first-registration-imperative-string-unsupported",
                $"Imperative DBM-style '{propertyName}' assignments must use supported constant, identifier, or ternary string expressions.",
                sourceFile,
                expression.GetLocation(),
                null);

    private static int? ReadImperativeIntProperty(
        string propertyName,
        ExpressionSyntax expression,
        string sourceFile,
        ImperativeEvaluationContext context,
        ICollection<CompilerDiagnostic> diagnostics) =>
        TryEvaluateIntExpression(expression, context, out var value)
            ? value
            : ReportUnsupportedScalar<int?>(
                diagnostics,
                "code-first-registration-imperative-int-unsupported",
                $"Imperative DBM-style '{propertyName}' assignments must use supported literals, identifiers, enum casts, or OptionSetValue(...) values.",
                sourceFile,
                expression.GetLocation(),
                null);

    private static bool TryCreateImperativeEntityBuffer(
        ExpressionSyntax expression,
        ImperativeEvaluationContext context,
        out ImperativeEntityBuffer entity)
    {
        entity = default!;

        if (expression is BinaryExpressionSyntax binary
            && binary.IsKind(SyntaxKind.CoalesceExpression))
        {
            expression = binary.Right;
        }

        if (expression is not ObjectCreationExpressionSyntax objectCreation
            || !string.Equals(GetTypeName(objectCreation.Type), "Entity", StringComparison.Ordinal)
            || objectCreation.ArgumentList?.Arguments.Count != 1
            || !TryEvaluateStringExpression(objectCreation.ArgumentList.Arguments[0].Expression, context, out var logicalName))
        {
            return false;
        }

        var normalizedLogicalName = NormalizeLogicalName(logicalName);
        if (string.Equals(normalizedLogicalName, "sdkmessageprocessingstep", StringComparison.OrdinalIgnoreCase))
        {
            entity = new ImperativeEntityBuffer(ImperativeEntityKind.Step);
            return true;
        }

        if (string.Equals(normalizedLogicalName, "sdkmessageprocessingstepimage", StringComparison.OrdinalIgnoreCase))
        {
            entity = new ImperativeEntityBuffer(ImperativeEntityKind.Image);
            return true;
        }

        return false;
    }

    private static bool TryEvaluateImperativeMessageContext(
        ExpressionSyntax expression,
        ImperativeEvaluationContext context,
        out ImperativeMessageContext? messageContext)
    {
        messageContext = null;
        if (expression is not InvocationExpressionSyntax invocation
            || !string.Equals(GetInvocationName(invocation), "GetMessage", StringComparison.Ordinal)
            || invocation.ArgumentList.Arguments.Count < 3)
        {
            return false;
        }

        var meaningfulArguments = invocation.ArgumentList.Arguments
            .TakeLast(3)
            .ToArray();
        if (meaningfulArguments.Length != 3
            || !TryEvaluateStringExpression(meaningfulArguments[0].Expression, context, out var primaryEntity)
            || !TryEvaluateStringExpression(meaningfulArguments[1].Expression, context, out var messageName)
            || !TryEvaluateStringExpression(meaningfulArguments[2].Expression, context, out var handlerPluginTypeName))
        {
            return false;
        }

        messageContext = new ImperativeMessageContext(
            messageName,
            primaryEntity,
            handlerPluginTypeName);
        return true;
    }

    private static bool TryResolveMessageContextReference(ExpressionSyntax expression, out string? messageContextName)
    {
        messageContextName = null;
        if (expression is not ObjectCreationExpressionSyntax objectCreation
            || !string.Equals(GetTypeName(objectCreation.Type), "EntityReference", StringComparison.Ordinal)
            || objectCreation.ArgumentList?.Arguments.Count != 2)
        {
            return false;
        }

        if (objectCreation.ArgumentList.Arguments[1].Expression is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Expression is IdentifierNameSyntax identifier)
        {
            messageContextName = identifier.Identifier.ValueText;
            return true;
        }

        return false;
    }

    private static bool TryGetEntityAssignmentTarget(
        ExpressionSyntax expression,
        ImperativeEvaluationContext context,
        out string variableName,
        out string propertyName)
    {
        variableName = string.Empty;
        propertyName = string.Empty;

        if (expression is not ElementAccessExpressionSyntax elementAccess
            || elementAccess.Expression is not IdentifierNameSyntax identifier
            || elementAccess.ArgumentList.Arguments.Count != 1
            || !TryEvaluateStringExpression(elementAccess.ArgumentList.Arguments[0].Expression, context, out var key))
        {
            return false;
        }

        variableName = identifier.Identifier.ValueText;
        propertyName = NormalizeLogicalName(key) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(variableName) && !string.IsNullOrWhiteSpace(propertyName);
    }

    private static bool TryEvaluateStringExpression(
        ExpressionSyntax expression,
        ImperativeEvaluationContext context,
        out string? value)
    {
        if (TryEvaluateStringExpression(expression, out value))
        {
            return true;
        }

        switch (expression)
        {
            case ParenthesizedExpressionSyntax parenthesized:
                return TryEvaluateStringExpression(parenthesized.Expression, context, out value);
            case IdentifierNameSyntax identifier when context.Strings.TryGetValue(identifier.Identifier.ValueText, out var mappedValue):
                value = mappedValue;
                return !string.IsNullOrWhiteSpace(value);
            case IdentifierNameSyntax identifier when context.ParameterBindings.TryGetValue(identifier.Identifier.ValueText, out var parameterBinding):
                return TryEvaluateStringExpression(parameterBinding, context with
                {
                    ParameterBindings = new Dictionary<string, ExpressionSyntax>(context.ParameterBindings, StringComparer.Ordinal)
                }, out value);
            case MemberAccessExpressionSyntax memberAccess
                when context.Strings.TryGetValue(memberAccess.ToString(), out var memberValue)
                     || context.Strings.TryGetValue(memberAccess.Name.Identifier.ValueText, out memberValue):
                value = memberValue;
                return !string.IsNullOrWhiteSpace(value);
            case MemberAccessExpressionSyntax memberAccess
                when memberAccess.Expression is IdentifierNameSyntax identifier
                     && context.Messages.TryGetValue(identifier.Identifier.ValueText, out var messageContext):
                value = memberAccess.Name.Identifier.ValueText switch
                {
                    "MessageName" => messageContext.MessageName,
                    "PrimaryEntity" => messageContext.PrimaryEntity,
                    "HandlerPluginTypeName" => messageContext.HandlerPluginTypeName,
                    _ => null
                };
                return !string.IsNullOrWhiteSpace(value);
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression):
                if (TryEvaluateStringExpression(binary.Left, context, out var leftString)
                    && TryEvaluateStringExpression(binary.Right, context, out var rightString))
                {
                    value = $"{leftString}{rightString}";
                    return true;
                }

                break;
            case InterpolatedStringExpressionSyntax interpolated when TryEvaluateInterpolatedString(interpolated, context, out value):
                return true;
            case ConditionalExpressionSyntax conditional
                when TryEvaluateBooleanExpression(conditional.Condition, context, out var condition):
                return TryEvaluateStringExpression(condition ? conditional.WhenTrue : conditional.WhenFalse, context, out value);
            case SwitchExpressionSyntax switchExpression
                when TryEvaluateSwitchStringExpression(switchExpression, context, out value):
                return true;
            default:
                value = null;
                return false;
        }

        value = null;
        return false;
    }

    private static bool TryEvaluateBooleanExpression(
        ExpressionSyntax expression,
        ImperativeEvaluationContext context,
        out bool value)
    {
        switch (expression)
        {
            case ParenthesizedExpressionSyntax parenthesized:
                return TryEvaluateBooleanExpression(parenthesized.Expression, context, out value);
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.TrueLiteralExpression):
                value = true;
                return true;
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.FalseLiteralExpression):
                value = false;
                return true;
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.EqualsExpression):
                if (TryEvaluateStringExpression(binary.Left, context, out var leftString)
                    && TryEvaluateStringExpression(binary.Right, context, out var rightString))
                {
                    value = string.Equals(leftString, rightString, StringComparison.Ordinal);
                    return true;
                }

                if (TryEvaluateIntExpression(binary.Left, context, out var leftInt)
                    && TryEvaluateIntExpression(binary.Right, context, out var rightInt))
                {
                    value = leftInt == rightInt;
                    return true;
                }

                break;
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.NotEqualsExpression):
                if (TryEvaluateBooleanExpression(
                        SyntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression, binary.Left, binary.Right),
                        context,
                        out var equalsValue))
                {
                    value = !equalsValue;
                    return true;
                }

                break;
        }

        value = default;
        return false;
    }

    private static bool TryEvaluateIntExpression(
        ExpressionSyntax expression,
        ImperativeEvaluationContext context,
        out int value)
    {
        if (TryEvaluateIntExpression(expression, out value))
        {
            return true;
        }

        switch (expression)
        {
            case ParenthesizedExpressionSyntax parenthesized:
                return TryEvaluateIntExpression(parenthesized.Expression, context, out value);
            case IdentifierNameSyntax identifier when context.Ints.TryGetValue(identifier.Identifier.ValueText, out var mappedValue):
                value = mappedValue;
                return true;
            case IdentifierNameSyntax identifier when context.ParameterBindings.TryGetValue(identifier.Identifier.ValueText, out var parameterBinding):
                return TryEvaluateIntExpression(parameterBinding, context, out value);
            case CastExpressionSyntax cast
                when string.Equals(cast.Type.ToString(), "int", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(cast.Type.ToString(), "Int32", StringComparison.OrdinalIgnoreCase):
                return TryEvaluateIntExpression(cast.Expression, context, out value);
            case ObjectCreationExpressionSyntax objectCreation
                when string.Equals(GetTypeName(objectCreation.Type), "OptionSetValue", StringComparison.Ordinal)
                     && objectCreation.ArgumentList?.Arguments.Count == 1:
                return TryEvaluateIntExpression(objectCreation.ArgumentList.Arguments[0].Expression, context, out value);
            case MemberAccessExpressionSyntax memberAccess:
                if (TryResolveEnumValue(memberAccess, context.EnumValues, out value))
                {
                    return true;
                }

                break;
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression):
                if (TryEvaluateIntExpression(binary.Left, context, out var leftInt)
                    && TryEvaluateIntExpression(binary.Right, context, out var rightInt))
                {
                    value = leftInt + rightInt;
                    return true;
                }

                break;
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.SubtractExpression):
                if (TryEvaluateIntExpression(binary.Left, context, out var subtractedLeft)
                    && TryEvaluateIntExpression(binary.Right, context, out var subtractedRight))
                {
                    value = subtractedLeft - subtractedRight;
                    return true;
                }

                break;
            case ConditionalExpressionSyntax conditional
                when TryEvaluateBooleanExpression(conditional.Condition, context, out var condition):
                return TryEvaluateIntExpression(condition ? conditional.WhenTrue : conditional.WhenFalse, context, out value);
            case SwitchExpressionSyntax switchExpression
                when TryEvaluateSwitchIntExpression(switchExpression, context, out value):
                return true;
        }

        value = default;
        return false;
    }

    private static bool TryResolveEnumValue(
        MemberAccessExpressionSyntax expression,
        IReadOnlyDictionary<string, int> enumValues,
        out int value)
    {
        var fullName = expression.ToString();
        if (enumValues.TryGetValue(fullName, out value))
        {
            return true;
        }

        return enumValues.TryGetValue(expression.Name.Identifier.ValueText, out value);
    }

    private static string? GetInvocationName(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            _ => null
        };

    private static Dictionary<string, DeclaredTypeMetadata> DiscoverDeclaredTypes(IEnumerable<SyntaxNode> roots)
    {
        var result = new Dictionary<string, DeclaredTypeMetadata>(StringComparer.Ordinal);
        foreach (var declaration in roots.SelectMany(root => root.DescendantNodes().OfType<ClassDeclarationSyntax>()))
        {
            var fullName = BuildDeclaredTypeName(declaration);
            if (string.IsNullOrWhiteSpace(fullName))
            {
                continue;
            }

            result[fullName] = new DeclaredTypeMetadata(
                fullName,
                declaration.Identifier.ValueText,
                declaration.BaseList?.Types.Any(type => IsCodeActivityType(type.Type)) == true);
        }

        return result;
    }

    private static bool TryGetDeclaredTypeMetadata(
        IReadOnlyDictionary<string, DeclaredTypeMetadata> declaredTypes,
        string logicalName,
        out DeclaredTypeMetadata metadata)
    {
        if (declaredTypes.TryGetValue(logicalName, out var exactMetadata)
            && exactMetadata is not null)
        {
            metadata = exactMetadata;
            return true;
        }

        var simpleName = logicalName.Split('.').LastOrDefault();
        if (!string.IsNullOrWhiteSpace(simpleName))
        {
            var matchedMetadata = declaredTypes.Values.FirstOrDefault(candidate =>
                string.Equals(candidate.SimpleName, simpleName, StringComparison.Ordinal));
            if (matchedMetadata is not null)
            {
                metadata = matchedMetadata;
                return true;
            }
        }

        metadata = new DeclaredTypeMetadata(string.Empty, string.Empty, false);
        return false;
    }

    private static bool IsCodeActivityType(TypeSyntax typeSyntax) =>
        GetTypeName(typeSyntax) switch
        {
            "CodeActivity" => true,
            "CodeActivity<" => true,
            _ => typeSyntax is GenericNameSyntax genericName
                 && string.Equals(genericName.Identifier.ValueText, "CodeActivity", StringComparison.Ordinal)
        };

    private static string BuildDeclaredTypeName(ClassDeclarationSyntax declaration)
    {
        var parts = new Stack<string>();
        parts.Push(declaration.Identifier.ValueText);

        for (SyntaxNode? current = declaration.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case NamespaceDeclarationSyntax namespaceDeclaration:
                    parts.Push(namespaceDeclaration.Name.ToString());
                    break;
                case FileScopedNamespaceDeclarationSyntax fileScopedNamespace:
                    parts.Push(fileScopedNamespace.Name.ToString());
                    break;
                case ClassDeclarationSyntax parentClass:
                    parts.Push(parentClass.Identifier.ValueText);
                    break;
            }
        }

        return string.Join(".", parts);
    }

    private static Dictionary<string, string?> DiscoverGlobalStringConstants(IEnumerable<SyntaxNode> roots)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        var emptyContext = new ImperativeEvaluationContext(
            result,
            new Dictionary<string, int>(StringComparer.Ordinal),
            new Dictionary<string, ImperativeMessageContext>(StringComparer.Ordinal),
            new Dictionary<string, int>(StringComparer.Ordinal),
            new Dictionary<string, DeclaredTypeMetadata>(StringComparer.Ordinal),
            new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal));
        foreach (var field in roots.SelectMany(root => root.DescendantNodes().OfType<FieldDeclarationSyntax>()))
        {
            if (!field.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.ConstKeyword))
                && !(field.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.StaticKeyword))
                     && field.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword))))
            {
                continue;
            }

            foreach (var variable in field.Declaration.Variables)
            {
                if (variable.Initializer?.Value is { } initializer
                    && TryEvaluateStringExpression(initializer, emptyContext, out var value))
                {
                    result[variable.Identifier.ValueText] = value;
                }
            }
        }

        return result;
    }

    private static Dictionary<string, int> DiscoverGlobalIntConstants(
        IEnumerable<SyntaxNode> roots,
        IReadOnlyDictionary<string, int> enumValues)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        var emptyContext = new ImperativeEvaluationContext(
            new Dictionary<string, string?>(StringComparer.Ordinal),
            result,
            new Dictionary<string, ImperativeMessageContext>(StringComparer.Ordinal),
            enumValues,
            new Dictionary<string, DeclaredTypeMetadata>(StringComparer.Ordinal),
            new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal));
        foreach (var field in roots.SelectMany(root => root.DescendantNodes().OfType<FieldDeclarationSyntax>()))
        {
            if (!field.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.ConstKeyword))
                && !(field.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.StaticKeyword))
                     && field.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword))))
            {
                continue;
            }

            foreach (var variable in field.Declaration.Variables)
            {
                if (variable.Initializer?.Value is { } initializer
                    && TryEvaluateIntExpression(initializer, emptyContext, out var value))
                {
                    result[variable.Identifier.ValueText] = value;
                }
            }
        }

        return result;
    }

    private static Dictionary<string, int> DiscoverEnumValues(IEnumerable<SyntaxNode> roots)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var enumDeclaration in roots.SelectMany(root => root.DescendantNodes().OfType<EnumDeclarationSyntax>()))
        {
            var enumName = enumDeclaration.Identifier.ValueText;
            var currentValue = -1;
            foreach (var member in enumDeclaration.Members)
            {
                if (member.EqualsValue is not null
                    && member.EqualsValue.Value is LiteralExpressionSyntax literal
                    && literal.Token.Value is int literalValue)
                {
                    currentValue = literalValue;
                }
                else
                {
                    currentValue++;
                }

                result[$"{enumName}.{member.Identifier.ValueText}"] = currentValue;
                result.TryAdd(member.Identifier.ValueText, currentValue);
            }
        }

        return result;
    }

    private static ImperativePluginStepImageDefinition ToImperativeImageDefinition(ImperativeEntityBuffer buffer) =>
        new(
            buffer.Name,
            buffer.EntityAlias,
            buffer.ImageType.HasValue ? buffer.ImageType.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
            buffer.MessagePropertyName,
            NormalizeAttributeList(buffer.SelectedAttributes),
            buffer.Description);

    private sealed record ImperativeEvaluationContext(
        Dictionary<string, string?> Strings,
        Dictionary<string, int> Ints,
        Dictionary<string, ImperativeMessageContext> Messages,
        IReadOnlyDictionary<string, int> EnumValues,
        IReadOnlyDictionary<string, DeclaredTypeMetadata> DeclaredTypes,
        Dictionary<string, ExpressionSyntax> ParameterBindings);

    private sealed record DeclaredTypeMetadata(string FullName, string SimpleName, bool IsCodeActivityDerived);

    private sealed record ImperativeMessageContext(
        string? MessageName,
        string? PrimaryEntity,
        string? HandlerPluginTypeName);

    private enum ImperativeEntityKind
    {
        Step,
        Image
    }

    private sealed record ImperativeEntityBuffer(ImperativeEntityKind Kind)
    {
        public string? VariableName { get; init; }
        public string? Name { get; init; }
        public string? Description { get; init; }
        public int? Stage { get; init; }
        public int? Mode { get; init; }
        public int? Rank { get; init; }
        public int? SupportedDeployment { get; init; }
        public string? FilteringAttributes { get; init; }
        public string? EntityAlias { get; init; }
        public int? ImageType { get; init; }
        public string? MessagePropertyName { get; init; }
        public string? SelectedAttributes { get; init; }
        public string? MessageContextName { get; init; }
    }

    private sealed record ImperativePluginStepDefinition(
        string? Name,
        string? HandlerPluginTypeName,
        string? MessageName,
        string? PrimaryEntity,
        string? Stage,
        string? Mode,
        string? Rank,
        string? SupportedDeployment,
        string? FilteringAttributes,
        string? Description,
        IReadOnlyList<ImperativePluginStepImageDefinition> Images);

    private sealed record ImperativePluginStepImageDefinition(
        string? Name,
        string? EntityAlias,
        string? ImageType,
        string? MessagePropertyName,
        string? SelectedAttributes,
        string? Description);

    private static bool TryEvaluateInterpolatedString(
        InterpolatedStringExpressionSyntax expression,
        ImperativeEvaluationContext context,
        out string? value)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var content in expression.Contents)
        {
            switch (content)
            {
                case InterpolatedStringTextSyntax text:
                    builder.Append(text.TextToken.ValueText);
                    break;
                case InterpolationSyntax interpolation:
                    if (TryEvaluateStringExpression(interpolation.Expression, context, out var stringValue))
                    {
                        builder.Append(stringValue);
                        break;
                    }

                    if (TryEvaluateIntExpression(interpolation.Expression, context, out var intValue))
                    {
                        builder.Append(intValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        break;
                    }

                    value = null;
                    return false;
                default:
                    value = null;
                    return false;
            }
        }

        value = builder.ToString();
        return true;
    }

    private static bool TryEvaluateSwitchStringExpression(
        SwitchExpressionSyntax switchExpression,
        ImperativeEvaluationContext context,
        out string? value)
    {
        value = null;
        int? governingInt = null;
        if (!TryEvaluateStringExpression(switchExpression.GoverningExpression, context, out var governingString)
            && !TryEvaluateIntExpression(switchExpression.GoverningExpression, context, out var evaluatedInt))
        {
            return false;
        }

        if (TryEvaluateIntExpression(switchExpression.GoverningExpression, context, out evaluatedInt))
        {
            governingInt = evaluatedInt;
        }

        foreach (var arm in switchExpression.Arms)
        {
            if (!SwitchArmMatches(arm.Pattern, governingString, governingInt, context))
            {
                continue;
            }

            return TryEvaluateStringExpression(arm.Expression, context, out value);
        }

        return false;
    }

    private static bool TryEvaluateSwitchIntExpression(
        SwitchExpressionSyntax switchExpression,
        ImperativeEvaluationContext context,
        out int value)
    {
        value = default;
        int? governingInt = null;
        if (!TryEvaluateStringExpression(switchExpression.GoverningExpression, context, out var governingString)
            && !TryEvaluateIntExpression(switchExpression.GoverningExpression, context, out var evaluatedInt))
        {
            return false;
        }

        if (TryEvaluateIntExpression(switchExpression.GoverningExpression, context, out evaluatedInt))
        {
            governingInt = evaluatedInt;
        }

        foreach (var arm in switchExpression.Arms)
        {
            if (!SwitchArmMatches(arm.Pattern, governingString, governingInt, context))
            {
                continue;
            }

            return TryEvaluateIntExpression(arm.Expression, context, out value);
        }

        return false;
    }

    private static bool SwitchArmMatches(
        PatternSyntax pattern,
        string? governingString,
        int? governingInt,
        ImperativeEvaluationContext context)
    {
        switch (pattern)
        {
            case DiscardPatternSyntax:
                return true;
            case ConstantPatternSyntax constantPattern:
                if (TryEvaluateStringExpression(constantPattern.Expression, context, out var armString))
                {
                    return string.Equals(governingString, armString, StringComparison.Ordinal);
                }

                if (TryEvaluateIntExpression(constantPattern.Expression, context, out var armInt))
                {
                    return governingInt.HasValue && governingInt.Value == armInt;
                }

                return false;
            default:
                return false;
        }
    }
}
