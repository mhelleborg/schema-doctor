﻿using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;

namespace SchemaDoctor.Microsoft.Extensions.AI;

/// <summary>
/// ChatResponse extensions for handling malformed input
/// </summary>
public static class CompletionExtensions
{
    /// <summary>
    /// Mapper that tries to fix hallucinations if present
    /// Will lean on the default mapper if possible, and fall back to mapping via the type schema
    /// </summary>
    /// <param name="completion">The completion to get the result from</param>
    /// <param name="parsed">The parsed result</param>
    /// <returns>True if the result can be parsed, false otherwise</returns>
    public static bool TryToGetResultWithTherapy<T>(this ChatResponse<T> completion,
        [NotNullWhen(true)] out T? parsed) where T : class
    {
        // If the default is OK, do nothing extra
        if (completion.TryGetResult(out parsed))
        {
            return true;
        }

        // Might be a hallucination
        var raw = completion.Text;
        if (string.IsNullOrWhiteSpace(raw))
        {
            parsed = default;
            return false;
        }

        return SchemaTherapist.TryMapToSchema(raw, out parsed);
    }
}