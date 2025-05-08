using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace SchemaDoctor.Microsoft.Extensions.AI;

/// <summary>
/// Extensions for <see cref="AIFunction"/> to help mitigate hallucinations
/// </summary>
public static class FunctionExtensions
{
    private static bool? _schemasIncludeCts;

    static string Cancel(CancellationToken cancellationToken)
    {
        return "unused";
    }

    private static JsonElement SchemaWithCancellationToken { get; } = AIFunctionFactory.Create(Cancel).JsonSchema;

    private static bool SchemasIncludeCts =>
        _schemasIncludeCts ??= SchemaWithCancellationToken.ToString().Contains("cancellationToken");

    /// <summary>
    /// Wraps the function in a <see cref="DoctoredFunction"/>
    /// This will then try to fix schema issues both for the defined input schema,
    /// and tries to repair hallucinations when the function is invoked
    /// </summary>
    /// <param name="function"></param>
    /// <returns></returns>
    public static AIFunction WithTherapy(this AIFunction function) =>
        function is DoctoredFunction ? function : new DoctoredFunction(function);

    /// <summary>
    /// Extension method that tries to mitigate hallucinations before passing the arguments to the function.
    /// It does this by looking at the type schema, and tries to map each argument to the correct schema.
    /// </summary>
    /// <param name="function"></param>
    /// <param name="arguments"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static ValueTask<object?> InvokeWithTherapyAsync(
        this AIFunction function,
        AIFunctionArguments? arguments,
        CancellationToken cancellationToken = default) =>
        function.InvokeAsync(GetArguments(function, arguments), cancellationToken);

    private static AIFunctionArguments? GetArguments(AIFunction function,
        AIFunctionArguments? arguments)
    {
        if (arguments is null || arguments.Count == 0) // Nothing to do
        {
            return arguments;
        }

        if (function.TryGetArguments(arguments, out var parsedArguments))
        {
            return parsedArguments;
        }

        // If parsing fails, return the original arguments
        return arguments;
    }

    internal static JsonElement GetSchema(this AIFunction function)
    {
        if (!SchemasIncludeCts)
        {
            return function.JsonSchema;
        }

        var toRemove = function.UnderlyingMethod?.GetParameters()
            .Where(it => it.ParameterType == typeof(CancellationToken))
            .Select(it => it.Name)
            .OfType<string>().ToHashSet();

        if (toRemove is null || toRemove.Count == 0)
        {
            return function.JsonSchema;
        }

        var updatedSchema = function.JsonSchema.Without(toRemove);
        return updatedSchema;
    }

    /// <summary>
    /// Remove the specified properties from the schema and return the updated schema
    /// </summary>
    /// <param name="jsonSchema"></param>
    /// <param name="fields"></param>
    /// <returns></returns>
    private static JsonElement Without(this JsonElement jsonSchema, HashSet<string> fields)
    {
        // If the element isn't an object, return it as is
        if (jsonSchema.ValueKind != JsonValueKind.Object)
        {
            return jsonSchema;
        }

        var node = JsonNode.Parse(jsonSchema.ToString());
        if (node is null)
        {
            return jsonSchema;
        }

        // Get the properties object
        if (node is JsonObject obj && obj.TryGetPropertyValue("properties", out var propsNode) &&
            propsNode is JsonObject propsObj)
        {
            foreach (var name in fields)
            {
                propsObj.Remove(name);
            }
        }

        return JsonDocument.Parse(node.ToJsonString()).RootElement;
    }

    /// <summary>
    /// Tries to parse the arguments to the correct types based on the schema
    /// </summary>
    /// <param name="function">The applicable function</param>
    /// <param name="arguments">The arguments to parse</param>
    /// <param name="parsed">True if it was able to parse the arguments</param>
    /// <returns></returns>
    public static bool TryGetArguments(this AIFunction function, AIFunctionArguments arguments,
        [NotNullWhen(true)] out AIFunctionArguments? parsed)
    {
        parsed = null;
        if (arguments.Count == 0) // Nothing to do
        {
            parsed = arguments;
            return true;
        }

        var parsedArguments = new Dictionary<string, object?>();
        var ok = true;


        var parameters = function.UnderlyingMethod?.GetParameters();

        if (parameters is null)
        {
            return false;
        }

        foreach (var parameter in parameters)
        {
            if (parameter.Name is null)
            {
                continue;
            }

            if (arguments.TryGetValue(parameter.Name, out var value))
            {
                if (value?.GetType() == parameter.ParameterType)
                {
                    parsedArguments.Add(parameter.Name, value);
                    continue;
                }

                if (value is null)
                {
                    parsedArguments.Add(parameter.Name, null);
                    continue;
                }

                // Try to fix incorrect type using schema mapping
                try
                {
                    var json = JsonSerializer.Serialize(value, function.JsonSerializerOptions);

                    // Use reflection to call TryMapToSchema with the correct type
                    var methodInfo = typeof(SchemaTherapist)
                        .GetMethod(nameof(SchemaTherapist.TryMapToSchema))
                        ?.MakeGenericMethod(parameter.ParameterType);

                    if (methodInfo != null)
                    {
                        var therapistParams = new object?[] { json, null, function.JsonSerializerOptions };
                        var success = (bool)methodInfo.Invoke(null, therapistParams)!;

                        if (success)
                        {
                            parsedArguments.Add(parameter.Name, therapistParams[1]);
                        }
                        else
                        {
                            // If mapping fails, add the original value? Or mark as not ok?
                            // For now, let's add the original value and mark as not fully ok
                            parsedArguments.Add(parameter.Name, value);
                            ok = false;
                        }
                    }
                    else
                    {
                         parsedArguments.Add(parameter.Name, value); // Add original if reflection fails
                         ok = false;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    ok = false;
                    // Failed to map, add the original value
                    parsedArguments.Add(parameter.Name, value);
                }
            }
            // Handle parameters not present in the input dictionary?
            // Currently, they are just skipped. This seems reasonable.
        }

        parsed = new AIFunctionArguments(parsedArguments)
        {
            Services = arguments.Services,
            Context = arguments.Context
        };


        return ok;
    }
}
