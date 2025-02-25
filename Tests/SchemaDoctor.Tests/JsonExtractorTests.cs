﻿using FluentAssertions;

namespace SchemaDoctor.Tests;

public class JsonExtractorTests
{
    [Fact]
    public void WhenJsonIsWrappedInTextResponseAndMarkdown()
    {
        var raw = """
                  John? Sure I know John!
                  I can even respond in json ({}) and markdown for you, like you asked!
                  ```
                  {
                      "name": "John",
                      "age": "31",
                      "city": "Baltimore",
                      "tags": ["guinea pig", "test subject"]
                  }
                  ```

                  He might have moved tho
                  """.ReplaceLineEndings("\n");

        var readOnlySpan = raw.AsSpan();
        var first = JsonExtractor.ExtractJsonDocument(readOnlySpan, out var remaining);

        first.Length.Should().Be(2);
        first.ToString().Should().Be("{}");

        var next = JsonExtractor.ExtractJsonDocument(remaining, out remaining);
        next.Length.Should().NotBe(0);

        var asString = next.ToString();

        asString.Should().BeEquivalentTo("""
                                         {
                                             "name": "John",
                                             "age": "31",
                                             "city": "Baltimore",
                                             "tags": ["guinea pig", "test subject"]
                                         }
                                         """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void WhenJsonIsArrayWrappedInTextResponseAndMarkdown()
    {
        var raw = """
                  John? Sure I know John!
                  I can even respond in json ({}) and markdown for you, like you asked!
                  ```
                  [
                    {
                      "name": "John",
                      "age": "30",
                      "city": "New York",
                      "tags": []
                    }
                  ]
                  ```

                  I think that's the right age at least. He might have aged since the last test.
                  """.ReplaceLineEndings("\n");

        var first = JsonExtractor.ExtractJsonDocument(raw.AsSpan(), out var remaining);


        first.Length.Should().Be(2);
        first.ToString().Should().Be("{}");

        var next = JsonExtractor.ExtractJsonDocument(remaining, out remaining);
        next.Length.Should().NotBe(0);

        var asString = next.ToString();

        asString.Should().BeEquivalentTo("""
                                         [
                                           {
                                             "name": "John",
                                             "age": "30",
                                             "city": "New York",
                                             "tags": []
                                           }
                                         ]
                                         """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void WhenTextContainsJsonCharacters()
    {
        var raw = """

                  This might mess with the parsing: {{7[{

                  {
                      "name": "John",
                      "age": "30",
                      "city": "New York"
                  }
                  """.ReplaceLineEndings("\n");

        var next = JsonExtractor.ExtractJsonDocument(raw, out _);
        next.Length.Should().NotBe(0);

        var asString = next.ToString();

        asString.Should().BeEquivalentTo("""
                                         {
                                             "name": "John",
                                             "age": "30",
                                             "city": "New York"
                                         }
                                         """.ReplaceLineEndings("\n"));
    }
}