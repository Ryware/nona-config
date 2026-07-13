using Nona.Application.Admin.ConfigEntries.Commands;
using Nona.Application.Admin.ConfigEntries.Validators;

namespace Nona.Application.Tests.ConfigEntries;

public class UpsertConfigEntryRequestValidatorTests
{
    private readonly UpsertConfigEntryRequestValidator _validator = new();

    [Test]
    public async Task AllowsValidLogicalContentTypes()
    {
        var json = await _validator.ValidateAsync(new UpsertConfigEntryRequest("""{"enabled":true}""", "json", "all"));
        var number = await _validator.ValidateAsync(new UpsertConfigEntryRequest("42", "number", "all"));
        var boolean = await _validator.ValidateAsync(new UpsertConfigEntryRequest("true", "boolean", "all"));
        var text = await _validator.ValidateAsync(new UpsertConfigEntryRequest("hello", "text", "all"));

        await Assert.That(json.IsValid).IsTrue();
        await Assert.That(number.IsValid).IsTrue();
        await Assert.That(boolean.IsValid).IsTrue();
        await Assert.That(text.IsValid).IsTrue();
    }

    [Test]
    public async Task RejectsValuesThatDoNotMatchDeclaredContentType()
    {
        var json = await _validator.ValidateAsync(new UpsertConfigEntryRequest("{invalid", "json", "all"));
        var number = await _validator.ValidateAsync(new UpsertConfigEntryRequest("abc", "number", "all"));
        var boolean = await _validator.ValidateAsync(new UpsertConfigEntryRequest("yes", "boolean", "all"));

        await Assert.That(json.IsValid).IsFalse();
        await Assert.That(number.IsValid).IsFalse();
        await Assert.That(boolean.IsValid).IsFalse();
    }

    [Test]
    public async Task RejectsUnknownContentType()
    {
        var result = await _validator.ValidateAsync(new UpsertConfigEntryRequest("hello", "xml", "all"));

        await Assert.That(result.IsValid).IsFalse();
    }
}
