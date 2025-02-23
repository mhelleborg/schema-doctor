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
    public static Task<object?> InvokeWithTherapyAsync(
        this AIFunction function,
        IEnumerable<KeyValuePair<string, object?>>? arguments,
        CancellationToken cancellationToken = default) =>
        function.InvokeAsync(GetArguments(function, arguments), cancellationToken);

    private static IEnumerable<KeyValuePair<string, object?>>? GetArguments(AIFunction function,
        IEnumerable<KeyValuePair<string, object?>>? arguments)
    {
        if (arguments is null)
        {
            return null;
        }

        if (arguments is not IDictionary<string, object?> dict)
        {
            dict = arguments.ToDictionary();
        }

        if (function.TryGetArguments(dict, out var parsedArguments))
        {
            return parsedArguments;
        }

        return dict;
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

        if (toRemove is null)
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
    public static bool TryGetArguments(this AIFunction function, IEnumerable<KeyValuePair<string, object?>>? arguments,
        [NotNullWhen(true)] out List<KeyValuePair<string, object?>>? parsed)
    {
        if (arguments is null)
        {
            parsed = null;
            return false;
        }

        if (arguments is not IDictionary<string, object?> dict)
        {
            dict = arguments.ToDictionary();
        }

        parsed = [];
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

            if (dict?.TryGetValue(parameter.Name, out var value) == true)
            {
                if (value?.GetType() == parameter.ParameterType)
                {
                    parsed.Add(new KeyValuePair<string, object?>(parameter.Name, value));
                    continue;
                }

                if (value is null)
                {
                    parsed.Add(new KeyValuePair<string, object?>(parameter.Name, null));
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
                            parsed.Add(new KeyValuePair<string, object?>(parameter.Name, therapistParams[1]));
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    ok = false;
                    // Failed to map, skip this parameter
                }
            }
        }

        return ok;
    }
}