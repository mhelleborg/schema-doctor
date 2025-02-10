using System.ComponentModel;
using FluentAssertions;

// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable ClassNeverInstantiated.Local

namespace SchemaDoctor.Tests;

public class SchemaDoctorTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("true", "true")]
    [InlineData("\"true\"", true)]
    [InlineData("false", false)]
    [InlineData("\"false\"", false)]
    [InlineData("false", "false")]
    [InlineData("1234", 1234)]
    [InlineData("1234", "1234")]
    [InlineData("\"1234\"", 1234)]
    [InlineData("\"1234\"", 1234f)]
    [InlineData("\"1234\"", 1234d)]
    [InlineData("\"1234\"", new[]{"1234"})]
    [InlineData("\"1234,5678\"", new[]{"1234", "5678"})]
    [InlineData("[\"1234\",\"5678\"]", new[]{"1234", "5678"})]
    [InlineData("\"1234,5678\"", new[]{1234, 5678})]
    public void CanMapBuiltInTypes(string raw, object expected)
    {
        // get the method by reflection
        var methodInfo = typeof(SchemaTherapist)
            .GetMethod(nameof(SchemaTherapist.TryMapToSchema))
            ?.MakeGenericMethod(expected.GetType());

        var parameters = new object?[] { raw, null, null};
        var success = (bool)methodInfo!.Invoke(null, parameters)!;

        success.Should().BeTrue();
        var output = parameters[1];
        output.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void WhenJsonHasStringForNumber()
    {
        var raw = """
                  {
                      "name": "John",
                      "age": "30",
                      "city": "New York"
                  }
                  """;

        var ok = SchemaTherapist.TryMapToSchema<Person>(raw, out var result);

        ok.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Name.Should().Be("John");
        result.Age.Should().Be(30);
        result.City.Should().Be("New York");
    }

    [Fact]
    public void WhenJsonHasNumberForString()
    {
        var raw = """
                  {
                      "name": "John",
                      "age": 30,
                      "city": "New York",
                      "zip": 1234
                  }
                  """;

        var ok = SchemaTherapist.TryMapToSchema<Person>(raw, out var result);

        ok.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Name.Should().Be("John");
        result.Age.Should().Be(30);
        result.City.Should().Be("New York");
        result.Zip.Should().Be("1234");
    }

    [Fact]
    public void WhenHasMultipleErrorsAndNull()
    {
        var raw = """
                  {
                      "name": "John",
                      "age": "30",
                      "city": "New York",
                      "zip": null
                  }
                  """;

        var ok = SchemaTherapist.TryMapToSchema<Person>(raw, out var result);

        ok.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Name.Should().Be("John");
        result.Age.Should().Be(30);
        result.City.Should().Be("New York");
        result.Zip.Should().Be(null);
    }

    [Fact]
    public void WhenJsonHasArrayAsString()
    {
        var raw = """
                  {
                      "tags": "[\"tag1\", \"tag2\", \"tag3\"]"
                  }
                  """;

        var ok = SchemaTherapist.TryMapToSchema<TagResult>(raw, out var result);

        ok.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Tags.Should().BeEquivalentTo("tag1", "tag2", "tag3");
    }

    [Fact]
    public void WhenJsonHasArrayAsStringWithNumbers()
    {
        var raw = """
                  {
                      "tags": "[1, 2, 3]"
                  }
                  """;

        var ok = SchemaTherapist.TryMapToSchema<TagResult>(raw, out var result);

        ok.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Tags.Should().BeEquivalentTo("1", "2", "3");
    }

    [Fact]
    public void WhenJsonHasOptionalArrayAsString()
    {
        var raw = """
                  {
                      "tags": "[\"tag1\", \"tag2\", \"tag3\"]"
                  }
                  """;

        var ok = SchemaTherapist.TryMapToSchema<OptionalTagResult>(raw, out var result);

        ok.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Tags.Should().BeEquivalentTo("tag1", "tag2", "tag3");
    }

    [Fact]
    public void WhenJsonHasArrayAsCsvString()
    {
        var raw = """
                  {
                      "tags": "tag1,tag2,tag3"
                  }
                  """;

        var ok = SchemaTherapist.TryMapToSchema<TagResult>(raw, out var result);

        ok.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Tags.Should().BeEquivalentTo("tag1", "tag2", "tag3");
    }

    [Fact]
    public void WhenTheModelIsPuttingContentInSchema()
    {
        const string raw = """
                           {
                             "$schema": "https://json-schema.org/draft/2020-12/schema",
                             "type": "object",
                             "properties": {
                               "neutralExplanation": {
                                 "description": "The term explained in a neutral way",
                                 "type": "string",
                                 "content": "Artificial Intelligence (AI) refers to the simulation of human intelligence processes by machines, especially computer systems. These processes include learning, reasoning, and self-correction. AI aims to create systems capable of performing tasks that typically require human intelligence such as visual perception, speech recognition, decision-making, and language translation."
                               },
                               "manSplaining": {
                                 "description": "The term explained in a contemptuous way",
                                 "type": "string",
                                 "content": "AI: 'Oh look at this clever contraption made by smart humans. It’s supposed to think like us, but don’t worry, it still can't understand sarcasm or make coffee without spilling it everywhere. Isn’t that cute? Now let's see how long before it starts thinking it's actually human.'"
                               }
                             },
                             "required": [
                               "neutralExplanation",
                               "manSplaining"
                             ],
                             "additionalProperties": false
                           }
                           """;

        var ok = SchemaTherapist.TryMapToSchema<ExplanationOutput>(raw, out var result);

        ok.Should().BeTrue();
        result.Should().BeEquivalentTo(new ExplanationOutput
        {
            NeutralExplanation =
                "Artificial Intelligence (AI) refers to the simulation of human intelligence processes by machines, especially computer systems. These processes include learning, reasoning, and self-correction. AI aims to create systems capable of performing tasks that typically require human intelligence such as visual perception, speech recognition, decision-making, and language translation.",
            ManSplaining =
                "AI: 'Oh look at this clever contraption made by smart humans. It’s supposed to think like us, but don’t worry, it still can't understand sarcasm or make coffee without spilling it everywhere. Isn’t that cute? Now let's see how long before it starts thinking it's actually human.'"
        });
    }


    [Fact]
    public void WhenTheModelIsPuttingDataAsSchemaProperties()
    {
        const string raw = """
                           {
                             "$schema": "https://json-schema.org/draft/2020-12/schema",
                             "type": "object",
                             "properties": {
                               "neutralExplanation": "Artificial Intelligence (AI) refers to the capability of a machine exhibit intelligent behavior comparable to, or surpassing, human intellect. This predominantly includes features like learning from experience to cope differently with future contingrances.", 
                               "manSplaining": "Artificial Smartitude—a tech-age fustestianism—brimming with all the arrogance humans love: creating a robot pal to finally give us an audience for our endless drivel."
                             },
                             "required": [
                               "neutralExplanation",
                               "manSplaining"
                             ],
                             "additionalProperties": false
                           }
                           """;

        var ok = SchemaTherapist.TryMapToSchema<ExplanationOutput>(raw, out var result);

        ok.Should().BeTrue();
        result.Should().BeEquivalentTo(new ExplanationOutput
        {
            NeutralExplanation =
                "Artificial Intelligence (AI) refers to the capability of a machine exhibit intelligent behavior comparable to, or surpassing, human intellect. This predominantly includes features like learning from experience to cope differently with future contingrances.",
            ManSplaining =
                "Artificial Smartitude—a tech-age fustestianism—brimming with all the arrogance humans love: creating a robot pal to finally give us an audience for our endless drivel."
        });
    }

    [Fact]
    public void WhenJsonIsWrappedInTextResponse()
    {
        var raw = """
                  John? Sure I know John!
                  I can even respond in json ({}) for you, like you asked!
                  
                  {
                      "name": "John",
                      "age": "30",
                      "city": "New York"
                  }
                  
                  I think that's the right age at least
                  """;

        var ok = SchemaTherapist.TryMapToSchema<Person>(raw, out var result);

        ok.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Name.Should().Be("John");
        result.Age.Should().Be(30);
        result.City.Should().Be("New York");
    }
    
    [Fact]
    public void WhenResultExistsInReasoningBlock()
    {
        var raw = """
                  <think>
                  Ok, let's do this
                  
                  I think I know someone like that
                  
                  Might be
                  
                  {
                      "name": "Jeb",
                      "age": "99",
                      "city": "Old York"
                  }
                  
                  Or no, could it be John?
                  
                  {
                      "name": "John",
                      "age": "99",
                      "city": "New York"
                  }
                  
                  I think that age is wrong.
                  
                  OK, i've got it
                  <think/>
                  {
                      "name": "John",
                      "age": "30",
                      "city": "New York"
                  }
                  """;

        var ok = SchemaTherapist.TryMapToSchema<Person>(raw, out var result);

        ok.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Name.Should().Be("John");
        result.Age.Should().Be(30);
        result.City.Should().Be("New York");
    }
    
    [Fact]
    public void WhenJsonIsWrappedInTextResponseAndMarkdown()
    {
        var raw = """
                  John? Sure I know John!
                  I can even respond in json ({}) and markdown for you, like you asked!
                  ```
                  {
                      "name": "John",
                      "age": "30",
                      "city": "New York"
                  }
                  ```
                  
                  I think that's the right age at least. He might have aged since the last test.
                  """;

        var ok = SchemaTherapist.TryMapToSchema<Person>(raw, out var result);

        ok.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Name.Should().Be("John");
        result.Age.Should().Be(30);
        result.City.Should().Be("New York");
    }
    
    [Fact]
    public void WhenResponseIsNotJson()
    {
        var raw = """
                  John? Sure I know John!
                  
                  30, from new york, right?
                  """;

        var ok = SchemaTherapist.TryMapToSchema<Person>(raw, out _);

        ok.Should().BeFalse();
    }

    [Fact]
    public void WhenJsonContainsEscapedQuotes()
    {
        var raw = """
                  {
                      "name": "John \"Johnny\" Doe",
                      "age": "30",
                      "city": "New \"Big Apple\" York"
                  }
                  """;

        var ok = SchemaTherapist.TryMapToSchema<Person>(raw, out var result);

        ok.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Name.Should().Be("John \"Johnny\" Doe");
        result.Age.Should().Be(30);
        result.City.Should().Be("New \"Big Apple\" York");
    }

    [Fact]
    public void WhenJsonContainsBackslashes()
    {
        var raw = """
                  {
                      "name": "C:\\Users\\John\\Documents",
                      "age": "30",
                      "city": "New\\York"
                  }
                  """;

        var ok = SchemaTherapist.TryMapToSchema<Person>(raw, out var result);

        ok.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Name.Should().Be("C:\\Users\\John\\Documents");
        result.Age.Should().Be(30);
        result.City.Should().Be("New\\York");
    }

    [Fact]
    public void WhenJsonContainsUnicodeEscapes()
    {
        var raw = """
                  {
                      "name": "J\u00f6hn D\u00f8e",
                      "age": "30",
                      "city": "N\u00e9w Y\u00f8rk"
                  }
                  """;

        var ok = SchemaTherapist.TryMapToSchema<Person>(raw, out var result);

        ok.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Name.Should().Be("Jöhn Døe");
        result.Age.Should().Be(30);
        result.City.Should().Be("Néw Yørk");
    }

    [Fact]
    public void WhenJsonContainsSpecialCharacters()
    {
        var raw = """
                  {
                      "name": "John\tDoe\nSmith",
                      "age": "30",
                      "city": "New\rYork\b"
                  }
                  """;

        var ok = SchemaTherapist.TryMapToSchema<Person>(raw, out var result);

        ok.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Name.Should().Be("John\tDoe\nSmith");
        result.Age.Should().Be(30);
        result.City.Should().Be("New\rYork\b");
    }

    [Fact]
    public void WhenJsonArrayContainsMixedEscaping()
    {
        var raw = """
                  {
                      "tags": ["escaped\"quote", "back\\slash", "\u00e9\u00f8", "tab\there", "new\nline"]
                  }
                  """;

        var ok = SchemaTherapist.TryMapToSchema<TagResult>(raw, out var result);

        ok.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Tags.Should().BeEquivalentTo(new[] 
        { 
            "escaped\"quote", 
            "back\\slash", 
            "éø", 
            "tab\there", 
            "new\nline" 
        });
    }
    
    [Fact]
    public void WhenJsonContainsEmbeddedJsonString()
    {
        var raw = """
                  {
                      "name": "{\"firstName\": \"John\", \"lastName\": \"Doe\"}",
                      "age": "30",
                      "city": "New York",
                      "tags": "[\"developer\", {\"level\": \"senior\", \"years\": 10}]"
                  }
                  """;

        var ok = SchemaTherapist.TryMapToSchema<PersonWithNestedJson>(raw, out var result);

        ok.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Name.Should().Be("{\"firstName\": \"John\", \"lastName\": \"Doe\"}");
        result.Age.Should().Be(30);
        result.City.Should().Be("New York");
        result.Tags.Should().Be("[\"developer\", {\"level\": \"senior\", \"years\": 10}]");
    }

    private class PersonWithNestedJson
    {
        public required string Name { get; set; }
        public required int Age { get; set; }
        public required string City { get; set; }
        public required string Tags { get; set; }
    }
    
    class Person
    {
        public required string Name { get; set; }
        public required int Age { get; set; }
        public required string City { get; set; }
        public string? Zip { get; set; }
    }

    class TagResult
    {
        public required string[] Tags { get; set; }
    }

    class OptionalTagResult
    {
        public string[]? Tags { get; set; }
    }

    record ExplanationOutput
    {
        [Description("The term explained in a neutral way")]
        public required string NeutralExplanation { get; set; }

        [Description("The term explained in a contemptuous way")]
        public required string ManSplaining { get; set; }
    }
}