namespace Nona.Application.Common.Interfaces;

public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a password using BCrypt with a work factor of 12.
    /// </summary>
    /// <param name="password">The plain text password to hash.</param>
    /// <returns>A tuple containing the hashed password and an empty salt (BCrypt manages salt internally).</returns>
    (string hash, string salt) HashPassword(string password);

    /// <summary>
    /// Verifies a password against a stored hash.
    /// </summary>
    /// <param name="password">The plain text password to verify.</param>
    /// <param name="storedHash">The stored password hash.</param>
    /// <returns>True if the password matches the hash, false otherwise.</returns>
    bool VerifyPassword(string password, string storedHash);
}
