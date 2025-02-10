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

        // Stack to track opening braces/brackets
        var stack = new Stack<char>();
        var inString = false;
        var escaped = false;

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
                    stack.Push(c);
                    break;

                case '}':
                case ']':
                    if (stack.Count == 0) continue;
                
                    var openBracket = stack.Peek();
                    if ((openBracket == '{' && c == '}') || (openBracket == '[' && c == ']'))
                    {
                        stack.Pop();
                        if (stack.Count == 0)
                        {
                            remainingText = text[(i + 1)..];
                            return text.Slice(start, i - start + 1);
                        }
                    }
                    break;
            }
        }

        // No valid JSON document found
        return ReadOnlySpan<char>.Empty;
    }}