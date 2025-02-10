using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace SchemaDoctor.Microsoft.Extensions.AI;

public static class FunctionExtensions
{
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

    internal static AIFunctionMetadata WithoutCancellationTokenParameters(this AIFunctionMetadata metadata)
    {
        var cancellationTokenParameters = metadata.Parameters.Where(it => it.IsCancellationToken()).ToList();
        if (cancellationTokenParameters.Count != 0)
        {
            return new AIFunctionMetadata(metadata.Name)
            {
                Description = metadata.Description,
                Parameters = metadata.Parameters.Except(cancellationTokenParameters).ToList(),
                ReturnParameter = metadata.ReturnParameter,
                JsonSerializerOptions = metadata.JsonSerializerOptions,
                AdditionalProperties = metadata.AdditionalProperties
            };
        }

        return metadata;
    }

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

        foreach (var parameterMetadata in function.Metadata.Parameters)
        {
            if (dict?.TryGetValue(parameterMetadata.Name, out var value) == true)
            {
                if (parameterMetadata.ParameterType is null || value?.GetType() == parameterMetadata.ParameterType)
                {
                    parsed.Add(new KeyValuePair<string, object?>(parameterMetadata.Name, value));
                    continue;
                }

                if (value is null)
                {
                    parsed.Add(new KeyValuePair<string, object?>(parameterMetadata.Name, null));
                    continue;
                }

                // Try to fix incorrect type using schema mapping
                try
                {
                    var json = JsonSerializer.Serialize(value, function.Metadata.JsonSerializerOptions);

                    // Use reflection to call TryMapToSchema with the correct type
                    var methodInfo = typeof(SchemaTherapist)
                        .GetMethod(nameof(SchemaTherapist.TryMapToSchema))
                        ?.MakeGenericMethod(parameterMetadata.ParameterType);

                    if (methodInfo != null)
                    {
                        var parameters = new object?[] { json, null, function.Metadata.JsonSerializerOptions };
                        var success = (bool)methodInfo.Invoke(null, parameters)!;

                        if (success)
                        {
                            parsed.Add(new KeyValuePair<string, object?>(parameterMetadata.Name, parameters[1]));
                        }
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine(e);
                    ok = false;
                    // Failed to map, skip this parameter
                }
            }
        }

        return ok;
    }


    static bool IsCancellationToken(this AIFunctionParameterMetadata parameter)
    {
        return parameter.ParameterType == typeof(CancellationToken);
    }
}