namespace Nona.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    /// <inheritdoc />
    public (string hash, string salt) HashPassword(string password)
    {
        // BCrypt includes salt automatically in the hash
        var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: WorkFactor);
        // Return empty salt since BCrypt manages it internally
        return (hash, string.Empty);
    }

    /// <inheritdoc />
    public bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash))
            return false;

        // BCrypt.Verify handles the comparison securely
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, storedHash);
        }
        catch
        {
            // Invalid hash format or other BCrypt errors
            return false;
        }
    }
}
