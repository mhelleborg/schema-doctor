using System.ComponentModel;
using AwesomeAssertions;
using SchemaDoctor.Microsoft.Extensions.AI;

namespace SchemaDoctor.Tests;

public class FunctionFactorySchemaTests
{
    public string ACancellableFunction(
        [Description("This should be a part of the json schema")]
        string zip,
        [Description("This should not")] CancellationToken cancellationToken = default) => zip;

    [Fact]
    public void WhenCreatingCancellableFunction()
    {
        var function = AIFunctionFactoryWithTherapy.CreateFunction(ACancellableFunction);

        var schema = function.JsonSchema.ToString();

        schema.Should().Contain("This should be a part of the json schema");
        schema.Should().NotContain("cancellationToken");
        schema.Should().NotContain("This should not");
    }
}