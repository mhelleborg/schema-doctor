using FluentAssertions;
using Microsoft.Extensions.AI;
using SchemaDoctor.Microsoft.Extensions.AI;

namespace SchemaDoctor.Tests;

public class ToolMappingTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("yes", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("no", false)]
    [InlineData("0", false)]
    public void CanMapStringToBool(string asString, bool expected)
    {
        var function = AIFunctionFactoryWithTherapy.CreateFunction(DoABooleanThing);

        var arguments = new Dictionary<string, object?>()
        {
            { "upperCase", asString }
        };

        var ok = function.TryGetArguments(new AIFunctionArguments(arguments), out var parsed);

        ok.Should().BeTrue();
        parsed.Should().HaveCount(1);
        parsed!.Single(it => it.Key.Equals("upperCase")).Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("2", 2)]
    [InlineData("-2", -2)]
    public void CanMapIntFromString(object asValue, int expected)
    {
        var function = AIFunctionFactoryWithTherapy.CreateFunction(DoAThing);

        var arguments = new Dictionary<string, object?>
        {
            { "howManyTimes", asValue }
        };

        var ok = function.TryGetArguments(new AIFunctionArguments(arguments),
            out var parsed);

        ok.Should().BeTrue();
        parsed.Should().HaveCount(1);
        parsed!.Single(it => it.Key.Equals("howManyTimes")).Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(1234, "1234")]
    public void CanMapStringFromInt(object asValue, string expected)
    {
        var function = AIFunctionFactoryWithTherapy.CreateFunction(DoAStringThing);

        var arguments = new Dictionary<string, object?>()
        {
            { "zip", asValue }
        };

        var ok = function.TryGetArguments(new AIFunctionArguments(arguments),
            out var parsed);

        ok.Should().BeTrue();
        parsed.Should().HaveCount(1);
        parsed!.Single(it => it.Key.Equals("zip")).Value.Should().Be(expected);
    }

    string DoAStringThing(string zip)
    {
        return zip;
    }

    string DoABooleanThing(bool upperCase)
    {
        return upperCase.ToString();
    }

    string DoAThing(int howManyTimes)
    {
        return howManyTimes.ToString();
    }
}