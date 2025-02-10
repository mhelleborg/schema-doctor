namespace SchemaDoctor;

public static class JsonExtractor
{
    public static ReadOnlySpan<char> ExtractJsonDocument(ReadOnlySpan<char> text, out ReadOnlySpan<char> remainingText)
    {
        remainingText = ReadOnlySpan<char>.Empty;
        if (text.IsEmpty) return ReadOnlySpan<char>.Empty;

        var currentPosition = 0;
        while (currentPosition < text.Length)
        {
            // Find the next opening brace or bracket
            var start = text[currentPosition..].IndexOfAny('{', '[');
            if (start == -1) return ReadOnlySpan<char>.Empty;

            start += currentPosition;
            var stack = new Stack<char>();
            var inString = false;
            var escaped = false;
            var firstBracket = text[start];
            stack.Push(firstBracket);
            var valid = true;

            for (var i = start + 1; i < text.Length; i++)
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

                    case '}' when stack.Count > 0 && stack.Peek() == '{':
                    case ']' when stack.Count > 0 && stack.Peek() == '[':
                        stack.Pop();
                        if (stack.Count == 0)
                        {
                            remainingText = text[(i + 1)..];
                            return text.Slice(start, i - start + 1);
                        }

                        break;

                    case '}' when stack.Count > 0 && stack.Peek() != '{':
                    case ']' when stack.Count > 0 && stack.Peek() != '[':
                        // Mismatched brackets - invalidate this attempt
                        valid = false;
                        break;
                }

                if (!valid) break;
            }

            // If this attempt wasn't valid, move past this opening bracket and try again
            currentPosition = start + 1;
        }

        // No valid JSON document found
        return ReadOnlySpan<char>.Empty;
    }
}