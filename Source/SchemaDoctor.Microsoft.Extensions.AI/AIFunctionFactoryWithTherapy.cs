using System.Text.Json;
using Microsoft.Extensions.AI;

namespace SchemaDoctor.Microsoft.Extensions.AI;

public static class AIFunctionFactoryWithTherapy
{
    /// <summary>
    /// Creates a function with hallucination "therapy".
    /// Makes the function more robust against hallucinations by trying to map the arguments to the defined schema.
    /// It also filters out unwanted parts of the function parameter definition, such as cancellation token parameters.
    /// </summary>
    /// <param name="method">The method to be represented via the created <see cref="AIFunction"/>.</param>
    /// <param name="name">The name to use for the <see cref="AIFunction"/>.</param>
    /// <param name="description">The description to use for the <see cref="AIFunction"/>.</param>
    /// <param name="serializerOptions">The <see cref="JsonSerializerOptions"/> used to marshal function parameters and any return value.</param>
    /// <returns>The created <see cref="AIFunction"/> for invoking <paramref name="method"/>.</returns>
    public static AIFunction CreateFunction(
        Delegate method,
        string? name = null,
        string? description = null,
        JsonSerializerOptions? serializerOptions = null
    ) => AIFunctionFactory.Create(method, name, description, serializerOptions).WithTherapy();
}