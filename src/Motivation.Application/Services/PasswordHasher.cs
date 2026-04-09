using System.Security.Cryptography;
using System.Text;
using Motivation.Application.Interfaces;

namespace Motivation.Application.Services
{
    // Simple SHA256 hasher for demo purposes. In production use a stronger algorithm (BCrypt/PBKDF2).
    public class PasswordHasher : IPasswordHasher
    {
        public string Hash(string password)
        {
            if (password == null) return string.Empty;
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder();
            foreach (var b in hash)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        public bool Verify(string password, string hash)
        {
            return Hash(password) == hash;
        }
    }
}
