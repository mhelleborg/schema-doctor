using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace SchemaDoctor.Microsoft.Extensions.AI;

/// <summary>
/// Middleware to mitigate hallucinations and remove garbage from the function definition
/// </summary>
/// <param name="function"></param>
public class DoctoredFunction(AIFunction function) : AIFunction
{
    /// <inheritdoc />
    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await function.InvokeWithTherapyAsync(arguments, cancellationToken).ConfigureAwait(false);
    }


    /// <inheritdoc />
    public override JsonElement JsonSchema { get; } = function.GetSchema();

    /// <inheritdoc />
    public override MethodInfo? UnderlyingMethod => function.UnderlyingMethod;

    /// <inheritdoc />
    public override JsonSerializerOptions JsonSerializerOptions => function.JsonSerializerOptions;

    /// <inheritdoc />
    public override string Name => function.Name;

    /// <inheritdoc />
    public override string Description => function.Description;

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, object?> AdditionalProperties => function.AdditionalProperties;
}
