using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;

namespace SchemaDoctor.Microsoft.Extensions.AI;

public static class CompletionExtensions
{
    /// <summary>
    /// Mapper that tries to fix hallucinations if present
    /// Will lean on the default mapper if possible, and fall back to hacky parsing
    /// </summary>
    /// <param name="completion"></param>
    /// <param name="parsed"></param>
    /// <param name="error"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static bool TryToGetResultWithTherapy<T>(this ChatCompletion<T> completion,
        [NotNullWhen(true)] out T? parsed,
        [NotNullWhen(false)] out string? error) where T : class
    {
        // If the default is OK, do nothing extra
        if (completion.TryGetResult(out parsed))
        {
            error = null;
            return true;
        }

        // Might be a hallucination
        var raw = completion.Message.Text;
        if (raw is null)
        {
            parsed = default;
            error = "No text in response";
            return false;
        }

        return SchemaTherapist.TryMapToSchema(raw, out parsed, out error);
    }
}