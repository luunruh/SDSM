using System.Security.Cryptography;
using System.Text.Json;

namespace Services
{
    // Single-admin credential store: one PBKDF2-hashed password in a
    // JSON file. SetupRequired is true until the first password is set.
    public class AuthService
    {
        public record Credentials(string Username, string Salt, string Hash, int Iterations);

        private const int Pbkdf2Iterations = 100_000;

        private readonly string _file;
        private Credentials? _credentials;

        public AuthService(string file)
        {
            _file = file;
            _credentials = File.Exists(file)
                ? JsonSerializer.Deserialize<Credentials>(File.ReadAllText(file))
                : null;
        }

        public bool SetupRequired => _credentials == null;

        public bool Verify(string username, string password)
        {
            Credentials? credentials = _credentials;
            if (credentials == null || !string.Equals(username, credentials.Username, StringComparison.Ordinal))
            {
                return false;
            }
            byte[] expected = Convert.FromBase64String(credentials.Hash);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(credentials.Salt),
                credentials.Iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        public void SetPassword(string username, string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, 32);
            var credentials = new Credentials(username,
                Convert.ToBase64String(salt), Convert.ToBase64String(hash), Pbkdf2Iterations);
            File.WriteAllText(_file, JsonSerializer.Serialize(credentials));
            _credentials = credentials;
        }
    }
}
