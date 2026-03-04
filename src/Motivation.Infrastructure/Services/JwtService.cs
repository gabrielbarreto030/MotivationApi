using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Motivation.Application.Interfaces;
using Motivation.Domain.Entities;

namespace Motivation.Infrastructure.Services
{
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _config;

        public JwtService(IConfiguration config)
        {
            _config = config;
        }

        public string GenerateToken(User user)
        {
            var key = _config["Jwt:Key"] ?? "dev_secret_key_change_me";
            var issuer = _config["Jwt:Issuer"] ?? "motivation";
            var audience = _config["Jwt:Audience"] ?? "motivation";
            var expiresMinutes = int.TryParse(_config["Jwt:ExpiresMinutes"], out var m) ? m : 60;

            var header = new { alg = "HS256", typ = "JWT" };
            var now = DateTimeOffset.UtcNow;
            var payload = new
            {
                sub = user.Id.ToString(),
                email = user.Email,
                iss = issuer,
                aud = audience,
                iat = now.ToUnixTimeSeconds(),
                exp = now.AddMinutes(expiresMinutes).ToUnixTimeSeconds()
            };

            string Encode(object obj)
            {
                var json = JsonSerializer.Serialize(obj);
                var bytes = Encoding.UTF8.GetBytes(json);
                return Base64UrlEncode(bytes);
            }

            var headerEncoded = Encode(header);
            var payloadEncoded = Encode(payload);
            var toSign = headerEncoded + "." + payloadEncoded;
            var signature = ComputeHmacSha256(key, toSign);
            return toSign + "." + signature;
        }

        private static string ComputeHmacSha256(string key, string data)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            using var hmac = new HMACSHA256(keyBytes);
            var sig = hmac.ComputeHash(dataBytes);
            return Base64UrlEncode(sig);
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
