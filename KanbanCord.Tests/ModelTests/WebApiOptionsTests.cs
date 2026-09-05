using System.ComponentModel.DataAnnotations;
using KanbanCord.Core.Options;

namespace KanbanCord.Tests.ModelTests;

public sealed class WebApiOptionsTests
{
    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    public void ApiKey_RejectsMissingOrShortSecrets(string apiKey)
    {
        var options = new WebApiOptions { ApiKey = apiKey };
        var validation = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(
            options,
            new ValidationContext(options),
            validation,
            validateAllProperties: true);

        Assert.False(valid);
        Assert.NotEmpty(validation);
    }

    [Fact]
    public void ApiKey_AcceptsALongServiceSecret()
    {
        var options = new WebApiOptions { ApiKey = new string('x', 32) };

        Assert.True(Validator.TryValidateObject(
            options,
            new ValidationContext(options),
            validationResults: null,
            validateAllProperties: true));
    }
}
