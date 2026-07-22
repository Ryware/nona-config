using Nona.Application.Admin.ConfigReleases.Commands;
using Nona.Application.Admin.ConfigReleases.DTOs;
using Nona.Application.Admin.ConfigReleases.Validators;

namespace Nona.Application.Tests.ConfigReleases;

public class PublishConfigReleaseRequestValidatorTests
{
    private readonly PublishConfigReleaseRequestValidator _validator = new();

    [Test]
    public async Task AllowsNullAndEmptyEntryPayloads()
    {
        var nullPayload = await _validator.ValidateAsync(new PublishConfigReleaseRequest("1.2.3"));
        var emptyPayload = await _validator.ValidateAsync(new PublishConfigReleaseRequest("1.2.3", Entries: []));

        await Assert.That(nullPayload.IsValid).IsTrue();
        await Assert.That(emptyPayload.IsValid).IsTrue();
    }

    [Test]
    public async Task RejectsNullEntry()
    {
        var result = await ValidateAsync([null!]);

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Select(error => error.PropertyName)).Contains("Entries[0]");
    }

    [Test]
    public async Task RejectsInvalidKeys()
    {
        foreach (var key in new[]
                 {
                     "",
                     "   ",
                     "feature flag",
                     "feature\tflag",
                     "feature.flag\n",
                     "feature/value",
                     "feature@flag",
                     "Ångström",
                     "不存在",
                     "___"
                 })
        {
            var result = await ValidateAsync([Entry(key)]);

            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors.Select(error => error.PropertyName)).Contains("Entries[0].Key");
        }
    }

    [Test]
    public async Task RejectsNullValue()
    {
        var result = await ValidateAsync([Entry("feature.flag", value: null!)]);

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Select(error => error.PropertyName)).Contains("Entries[0].Value");
    }

    [Test]
    public async Task RejectsCaseInsensitiveDuplicateKeys()
    {
        var result = await ValidateAsync(
        [
            Entry("feature.flag"),
            Entry("FEATURE.FLAG")
        ]);

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Select(error => error.PropertyName)).Contains("Entries[1].Key");
    }

    [Test]
    public async Task RejectsInvalidScope()
    {
        var result = await ValidateAsync([Entry("feature.flag", scope: "internal")]);

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Select(error => error.PropertyName)).Contains("Entries[0].Scope");
    }

    [Test]
    public async Task RejectsUnknownContentType()
    {
        var result = await ValidateAsync([Entry("feature.flag", contentType: "xml")]);

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Select(error => error.PropertyName)).Contains("Entries[0].ContentType");
    }

    [Test]
    public async Task RejectsValuesThatDoNotMatchDeclaredContentType()
    {
        var invalidEntries = new[]
        {
            Entry("json.value", "{invalid", "json"),
            Entry("number.value", "abc", "number"),
            Entry("boolean.value", "yes", "boolean")
        };

        for (var index = 0; index < invalidEntries.Length; index++)
        {
            var result = await ValidateAsync([invalidEntries[index]]);

            await Assert.That(result.IsValid).IsFalse();
            await Assert.That(result.Errors.Select(error => error.PropertyName)).Contains("Entries[0].Value");
        }
    }

    [Test]
    public async Task AllowsContentTypeAliasesAndInference()
    {
        var result = await ValidateAsync(
        [
            Entry("declared.json", "{}", "application/json"),
            Entry("declared.text", "hello", "text/plain"),
            Entry("declared.number", "42", "integer"),
            Entry("declared.boolean", "true", "bool"),
            Entry("inferred.json", "[]", ""),
            Entry("inferred.text", "hello", " "),
            Entry("inferred.number", "42", null!),
            Entry("inferred.boolean", "false", "")
        ]);

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task AllowsSupportedKeySeparators()
    {
        var result = await ValidateAsync(
        [
            Entry("feature.enabled"),
            Entry("feature_flag"),
            Entry("feature-flag")
        ]);

        await Assert.That(result.IsValid).IsTrue();
    }

    private async Task<FluentValidation.Results.ValidationResult> ValidateAsync(
        IReadOnlyList<ConfigReleaseEntryDto> entries)
    {
        return await _validator.ValidateAsync(new PublishConfigReleaseRequest("1.2.3", Entries: entries));
    }

    private static ConfigReleaseEntryDto Entry(
        string key,
        string value = "value",
        string contentType = "text",
        string scope = "all")
    {
        return new ConfigReleaseEntryDto(key, value, contentType, scope);
    }
}
