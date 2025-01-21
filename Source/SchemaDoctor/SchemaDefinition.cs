using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Generation;

namespace SchemaDoctor;

public static class SchemaDefinition<T>
{
    // ReSharper disable once StaticMemberInGenericType
    static readonly SystemTextJsonSchemaGeneratorSettings Settings = new()
    {
        FlattenInheritanceHierarchy = true,
        SchemaType = SchemaType.JsonSchema,
        GenerateEnumMappingDescription = false,
        AllowReferencesWithProperties = false
    };

    // ReSharper disable once StaticMemberInGenericType
    public static JsonSchema JsonSchema { get; } = CreateSchema() ??
                                                   throw new InvalidOperationException(
                                                       "Failed to deserialize schema");

    public static string Schema => JsonSchema.ToJson();

    static JsonSchema CreateSchema()
    {
        // Generate JSON Schema for InputObject
        var jsonSchema = JsonSchema.FromType<T>(Settings);
        jsonSchema.Title = null;

        // Convert to JObject for manual post-processing
        var jObject = JObject.Parse(jsonSchema.ToJson());

        // Inline $ref definitions and simplify enum properties
        InlineDefinitionsAndSimplifyEnum(jObject);

        // Remove "$schema" and "definitions" 
        jObject.Remove("$schema");
        jObject.Remove("definitions");
        jObject.Remove("additionalProperties");

        // Process "properties" to lowercase names and remove "minLength"

        var asSchema = jObject.ToString(Formatting.None);

        return JsonSchema.FromJsonAsync(asSchema).GetAwaiter().GetResult() ??
               throw new InvalidOperationException("Failed to convert schema to PropertyDefinition");
    }


    static void InlineDefinitionsAndSimplifyEnum(JObject jObject)
    {
        if (jObject["definitions"] is not JObject definitions) return;

        foreach (var definition in definitions)
        {
            var definitionName = definition.Key;
            if (definition.Value is not JObject definitionValue) continue;

            if (definitionValue["enum"] != null && definitionValue["x-enumNames"] != null)
            {
                definitionValue.Remove("x-enumNames");

                if (definitionValue["enum"] is not JArray values) continue;
                definitionValue["enum"] = NormalizeToLowercase(values);
            }

            foreach (var refProperty in jObject.Descendants().Where(IsReferenceOf(definitionName))
                         .ToList())
            {
                ((JObject)refProperty).Replace(definitionValue);
            }
        }
    }

    static Func<JToken, bool> IsReferenceOf(string definitionName)
    {
        var refName = "#/definitions/" + definitionName;

        return x =>
        {
            if (x is not JObject prop) return false;
            var r = (string?)prop["$ref"];
            return r is not null && r == refName;
        };
    }

    /// <summary>
    /// Normalize all strings in the array to lowercase
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    static JArray NormalizeToLowercase(IEnumerable<JToken> input) =>
        new(input.Select(r =>
        {
            try
            {
                return ((string)r!).ToLower(CultureInfo.InvariantCulture);
            }
            catch
            {
                return r;
            }
        }));
}