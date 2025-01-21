using Microsoft.Extensions.AI;

namespace SchemaDoctor.Microsoft.Extensions.AI;

/// <summary>
/// Middleware to mitigate hallucinations and remove garbage from the function definition
/// </summary>
/// <param name="function"></param>
public class DoctoredFunction(AIFunction function) : AIFunction
{
    protected override Task<object?> InvokeCoreAsync(IEnumerable<KeyValuePair<string, object?>> arguments,
        CancellationToken cancellationToken)
    {
        return function.InvokeWithTherapyAsync(arguments, cancellationToken);
    }


    public override AIFunctionMetadata Metadata { get; } = function.Metadata.WithoutCancellationTokenParameters();
}