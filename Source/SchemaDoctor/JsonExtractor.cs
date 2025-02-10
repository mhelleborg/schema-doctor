namespace SchemaDoctor;

public static class JsonExtractor
{
    public static ReadOnlySpan<char> ExtractJsonDocument(ReadOnlySpan<char> text, out ReadOnlySpan<char> remainingText)
    {
        remainingText = ReadOnlySpan<char>.Empty;
        if (text.IsEmpty) return ReadOnlySpan<char>.Empty;

        // Find the first opening brace or bracket
        var start = text.IndexOfAny('{', '[');
        if (start == -1) return ReadOnlySpan<char>.Empty;

        // Track nesting level and string context
        var nestingLevel = 0;
        var inString = false;
        var escaped = false;
        var openChar = text[start];
        var closeChar = openChar == '{' ? '}' : ']';

        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    break;

                case '{':
                case '[':
                    if (c == openChar || nestingLevel > 0)
                    {
                        nestingLevel++;
                    }

                    break;

                case '}':
                case ']':
                    if ((c == closeChar && nestingLevel == 1) || (nestingLevel > 1 &&
                                                                  ((c == '}' && text[i - 1] != '{') ||
                                                                   (c == ']' && text[i - 1] != '['))))
                    {
                        nestingLevel--;
                        if (nestingLevel == 0)
                        {
                            // Match
                            remainingText = text[(i + 1)..];
                            return text.Slice(start, i - start + 1);
                        }
                    }

                    break;
            }
        }

        // No valid JSON document found
        return ReadOnlySpan<char>.Empty;
    }
}