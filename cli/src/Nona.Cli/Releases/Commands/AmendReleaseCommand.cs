using Microsoft.Kiota.Abstractions;
using Nona.Cli.Generated.Models;

namespace Nona.Cli.Releases.Commands;

internal sealed record AmendReleaseCommand(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment,
    string SourceVersion,
    IReadOnlyList<string> SetValues,
    IReadOnlyList<string> DeleteKeys,
    string? FromFile,
    bool Editor);

internal sealed class AmendReleaseCommandHandler(
    Func<HttpClient>? httpClientFactory = null,
    IReleaseEntryEditor? editor = null,
    Func<bool>? isInteractive = null)
{
    private readonly IReleaseEntryEditor _editor = editor ?? new ReleaseEntryEditor();
    private readonly Func<bool> _isInteractive =
        isInteractive ?? (() => !Console.IsInputRedirected);

    public async Task<int> HandleAsync(AmendReleaseCommand command, CancellationToken ct)
    {
        var hasDirectEdits = command.SetValues.Count > 0 || command.DeleteKeys.Count > 0;
        var editModeCount =
            (hasDirectEdits ? 1 : 0) +
            (command.FromFile is not null ? 1 : 0) +
            (command.Editor ? 1 : 0);
        if (editModeCount > 1)
        {
            return ValidationError(
                "Choose exactly one amend edit mode: --set/--delete, --from-file, or --editor.");
        }

        var useEditor = command.Editor || editModeCount == 0;
        if (editModeCount == 0 && !_isInteractive())
        {
            return ValidationError(
                "Amend requires --set/--delete, --from-file, or --editor when input is not interactive.");
        }

        if (!ReleaseVersions.TryParseExact(command.SourceVersion, out var sourceVersion))
            return ValidationError("Source version must use major.minor.patch format.");

        using var api = NonaClientFactory.Create(command.Connection, httpClientFactory);
        var releases = api.Admin.Projects[command.Project]
            .Environments[command.Environment].Releases;

        var existingReleases = await releases.GetAsync(cancellationToken: ct) ?? [];
        var source = await releases[command.SourceVersion].GetAsync(cancellationToken: ct);
        if (source?.Entries is null)
        {
            throw new InvalidOperationException(
                $"Release {command.SourceVersion} did not return its entries and cannot be amended.");
        }

        if (!ReleaseVersions.TryGetNextPatch(
                sourceVersion,
                existingReleases.Select(release => release.Version),
                out var targetVersion))
        {
            return ValidationError(
                $"Release line {sourceVersion.Line} cannot be amended because its patch " +
                "number is already at the supported maximum.");
        }

        List<ConfigReleaseEntryDto> editedEntries;
        try
        {
            var copiedEntries = ReleaseEntryEditing.Clone(source.Entries);
            if (hasDirectEdits)
            {
                editedEntries = ReleaseEntryEditing.ApplyDirectEdits(
                    copiedEntries,
                    command.SetValues,
                    command.DeleteKeys);
            }
            else if (command.FromFile is not null)
            {
                editedEntries = await ReleaseEntryEditing.ReadFileAsync(command.FromFile, ct);
            }
            else if (useEditor)
            {
                editedEntries = await _editor.EditAsync(copiedEntries, ct);
            }
            else
            {
                throw new InvalidOperationException("No amend edit mode was selected.");
            }
        }
        catch (ReleaseEditException exception)
        {
            return ValidationError(exception.Message);
        }

        ConfigReleaseDetailsDto? release;
        try
        {
            release = await releases.PostAsync(
                new PublishConfigReleaseRequest
                {
                    Version = targetVersion.ToString(),
                    MakeActive = false,
                    Entries = editedEntries
                },
                cancellationToken: ct);
        }
        catch (ApiException exception) when (exception.ResponseStatusCode is 400 or 409 or 422)
        {
            var error = CliExceptionHandler.Describe(exception);
            Console.Error.WriteLine(error.Message);
            return error.ExitCode;
        }

        Console.WriteLine(
            $"Published amended release {release?.Version ?? targetVersion.ToString()} " +
            $"from {command.SourceVersion}.");
        return CliExitCodes.Success;
    }

    private static int ValidationError(string message)
    {
        Console.Error.WriteLine($"Error: {message}");
        return CliExitCodes.ValidationError;
    }
}
