# SchemaDoctor

<img src="logo.png" alt="SchemaDoctor Logo"  width="200px;">

A .NET helper library to fix schema issues when working with Large Language Models (LLMs). It helps handle common hallucination problems by attempting to map potentially incorrect responses to your expected schema.
In Addition it is able to pick out json responses returned after chain of thought or reasoning tokens, as long as it matches a given json schema.

## Installation

```shell
dotnet add package SchemaDoctor
```

For Microsoft.Extensions.AI integration:
```shell
dotnet add package SchemaDoctor.Microsoft.Extensions.AI
```

## Key Features

- Fixes common LLM response issues like incorrect data types and malformed JSON
- Maps string representations to proper numeric/boolean values
- Handles arrays that are incorrectly returned as strings
- Supports case-insensitive property mapping
- Integrates with Microsoft.Extensions.AI, both for `AIFunction` and structured responses via `ChatCompletion<T>`.

## Basic Usage

```csharp
public class MyResponse
{
    public int Number { get; set; }
    public bool Flag { get; set; }
    public string[] Tags { get; set; }
}

// Try to map potentially malformed JSON to your schema
if (SchemaTherapist.TryMapToSchema<MyResponse>(llmResponse, out var parsed))
{
    // Successfully mapped to schema
    Console.WriteLine($"Number: {parsed.Number}");
}
else
{
    Console.WriteLine($"Failed to parse response");
}
```

## Microsoft.Extensions.AI Integration

Schema Doctor provides extensions for Microsoft.Extensions.AI to make function calling more robust:

```csharp
// Create a function with hallucination therapy
var function = AIFunctionFactoryWithTherapy.CreateFunction(
    (int number, string text) => $"Received {number} and {text}",
    name: "example",
    description: "An example function"
);

// Wrap a function with hallucination therapy
AIFunction function = ...
var wrappedFunction = function.WithTherapy();

public static AIFunction WithTherapy(this AIFunction function)

// Use the completion extension to handle potential hallucinations
if (completion.TryToGetResultWithTherapy(out var result))
{
    Console.WriteLine($"Got result: {result}");
}
```

## What It Fixes

- String representations of numbers: `"42"` → `42`
- String representations of booleans: `"true"` → `true`
- String arrays represented as single strings: `"[1,2,3]"` → `[1,2,3]`
- Comma-separated strings to arrays: `"red,green,blue"` → `["red","green","blue"]`
- Case-insensitive property mapping
- Schema definition responses instead of actual data
- Malformed JSON structures

## Contributing

Issues and pull requests are welcome on GitHub at https://github.com/mhelleborg/schema-doctor

## License

This project is available under the MIT License.