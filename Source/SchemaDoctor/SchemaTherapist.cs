using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using NJsonSchema;

namespace SchemaDoctor;

/// <summary>
/// This takes LLM responses with hallucinations and tries to map them to a schema
/// Errors can be for example that parts of the document has been represented as a string instead of a number,
/// or that an array has been represented as a string, or that the model tries to return the values inside a schema
/// definition
/// </summary>
public static class SchemaTherapist
{
    static readonly JsonSerializerOptions CaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Tries to map LLM responses (potentially with hallucinations) to a type
    /// It will first try to read it with no mapping, and only fall back to the schema mapping if that fails
    /// </summary>
    /// <param name="raw"></param>
    /// <param name="parsed"></param>
    /// <param name="serializerOptions"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static bool TryMapToSchema<T>(string raw, [NotNullWhen(true)] out T? parsed,
        [NotNullWhen(false)] JsonSerializerOptions? serializerOptions = null)
    {
        try
        {
            if (JsonSerializer.Deserialize<T>(raw, serializerOptions ?? CaseInsensitive) is { } result)
            {
                parsed = result;
                return true;
            }
        }
        catch
        {
            // Broken, but we can try to fix it
        }

        var candidates = GetCandidates(raw);

        // Checking the last candidate first, if the LLM is doing chain of thought
        foreach (var candidate in Enumerable.Reverse(candidates))
        {
            try
            {
                var jsonNode = JsonNode.Parse(candidate);
                if (jsonNode is null)
                {
                    continue;
                }

                var fixedJson = FixNode(jsonNode, SchemaDefinition<T>.JsonSchema);

                parsed = JsonSerializer.Deserialize<T>(fixedJson.ToJsonString(), serializerOptions ?? CaseInsensitive);
                return parsed is not null;
            }
            catch
            {
                // Continue or return false if no more candidates
            }
        }

        parsed = default;
        return false;
    }

    private static List<string> GetCandidates(string raw)
    {
        var found = new List<string>
        {
            raw
        };
        var remaining = raw.AsSpan();
        while (remaining.Length > 0)
        {
            var json = JsonExtractor.ExtractJsonDocument(remaining, out remaining);

            if (json.Length > 0)
            {
                var item = json.ToString();
                if(item.Equals(raw)) continue;
                found.Add(item);
                if (remaining.Length == 0) break;
            }
            else break;
        }

        return found;
    }

    private static JsonNode FixNode(JsonNode node, JsonSchema schema)
    {
        var type = schema.Type;

        if (type.HasFlag(JsonObjectType.Object))
        {
            return ToObject(node, schema);
        }

        if (type.HasFlag(JsonObjectType.Array))
        {
            return ToArray(node, schema);
        }

        return ToValue(node, schema);
    }

    private static JsonNode ToValue(JsonNode node, JsonSchema schema)
    {
        var type = schema.Type;

        if (type.HasFlag(JsonObjectType.Number) || type.HasFlag(JsonObjectType.Integer))
        {
            return ToNumber(node);
        }

        if (type.HasFlag(JsonObjectType.Boolean))
        {
            return ToBoolean(node);
        }

        if (type.HasFlag(JsonObjectType.String))
        {
            return AsString(node);
        }

        return node.DeepClone();
    }

    private static JsonNode AsString(JsonNode node)
    {
        switch (node)
        {
            case JsonValue jsonValue when jsonValue.GetValueKind() == JsonValueKind.String:
                return node.DeepClone();
            // bool
            case JsonValue jsonValue when jsonValue.GetValueKind() == JsonValueKind.True:
                return JsonValue.Create("true");
            case JsonValue jsonValue when jsonValue.GetValueKind() == JsonValueKind.False:
                return JsonValue.Create("false");

            // number
            case JsonValue jsonValue when jsonValue.GetValueKind() == JsonValueKind.Number:
                return JsonValue.Create(jsonValue.AsValue().ToString());

            default:
                return node.DeepClone();
        }
    }

    private static JsonNode ToBoolean(JsonNode node)
    {
        if (node is JsonValue jsonValue && jsonValue.GetValueKind() == JsonValueKind.String &&
            jsonValue.AsValue().TryGetValue(out string? asString))
        {
            switch (asString.ToLower())
            {
                case "true":
                case "yes":
                case "1":
                    return JsonValue.Create(true);
                case "false":
                case "no":
                case "0":
                    return JsonValue.Create(false);
            }

            if (bool.TryParse(asString, out var boolean))
            {
                return JsonValue.Create(boolean);
            }
        }

        return node.DeepClone();
    }

    private static JsonNode ToNumber(JsonNode node)
    {
        if (node is JsonValue jsonValue && jsonValue.GetValueKind() == JsonValueKind.String &&
            jsonValue.AsValue().TryGetValue(out string? asString))
        {
            if (decimal.TryParse(asString, out var number))
            {
                return JsonValue.Create(number);
            }
        }

        return node.DeepClone();
    }

    private static JsonArray ToArray(JsonNode node, JsonSchema schema)
    {
        if (node is JsonValue jsonValue && jsonValue.GetValueKind() == JsonValueKind.String &&
            jsonValue.AsValue().TryGetValue(out string? asString))
        {
            if (asString.StartsWith('[') && asString.EndsWith(']'))
            {
                node = JsonNode.Parse(asString) ?? throw new JsonException("Failed to parse string as JSON");
            }
        }

        if (node is not JsonArray jsonArray)
        {
            jsonArray = CoerceToArray(node);
        }

        var itemSchema = schema.Item ?? throw new JsonException("Expected items schema");


        var fixedArray = new JsonArray();
        foreach (var item in jsonArray)
        {
            if(item is null) continue;
            
            fixedArray.Add(FixNode(item, itemSchema));
        }

        return fixedArray;
    }

    private static JsonArray CoerceToArray(JsonNode value)
    {
        switch (value.GetValueKind())
        {
            case JsonValueKind.String when value.AsValue().TryGetValue(out string? asString):
                if (asString.Contains(','))
                {
                    var split = asString.Split(',');
                    var array = new JsonArray();
                    foreach (var s in split)
                    {
                        array.Add(JsonValue.Create(s));
                    }

                    return array;
                }

                return [value.DeepClone()];


            case JsonValueKind.Object:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                // Wrap it
                return [value.DeepClone()];
            case JsonValueKind.Undefined:
            case JsonValueKind.Null:
                // Empty
                return [];
            default:
                return [];
        }
    }

    private static JsonObject ToObject(JsonNode node, JsonSchema schema)
    {
        if (node is JsonValue jsonValue && jsonValue.GetValueKind() == JsonValueKind.String &&
            jsonValue.AsValue().TryGetValue(out string? asString))
        {
            node = JsonNode.Parse(asString) ?? throw new JsonException("Failed to parse string as JSON");
        }

        if (node is not JsonObject jsonObject) throw new JsonException("Expected object");

        // Fix any properties
        var fixedObject = new JsonObject();

        var propertyMap = schema.Properties
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value,
                StringComparer.OrdinalIgnoreCase
            );

        // Normal object processing
        foreach (var (key, value) in jsonObject)
        {
            if (propertyMap.TryGetValue(key, out var property) && value is not null)
            {
                fixedObject.Add(key, FixNode(value, property));
            }
        }

        // Check if we're dealing with a schema definition instead of the actual data
        // Phi-4 has a tendency to put data in the properties section
        if (jsonObject.ContainsKey("$schema") && jsonObject.TryGetPropertyValue("properties", out var properties) &&
            properties is not null)
        {
            foreach (var (key, value) in properties.AsObject())
            {
                if (!propertyMap.TryGetValue(key, out var propertySchema)) continue; // Not part of the schema

                if (value is JsonObject property)
                {
                    if (property.TryGetPropertyValue("content", out var contentNode) && contentNode is not null)
                    {
                        fixedObject.Add(key, FixNode(contentNode, propertySchema));
                    }
                }
                else if (value is JsonValue valueNode)
                {
                    fixedObject.Add(key, FixNode(valueNode, propertySchema));
                }
            }
        }


        return fixedObject;
    }
}