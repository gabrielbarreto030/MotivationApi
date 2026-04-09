using FluentAssertions;
using Motivation.Application.Services;
using Xunit;

namespace Motivation.UnitTests.Application
{
    public class PasswordHasherTests
    {
        private readonly PasswordHasher _hasher = new();

        [Fact]
        public void Hash_ReturnsNonEmptyString()
        {
            var hash = _hasher.Hash("password");

            hash.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void Hash_DoesNotReturnPlainTextPassword()
        {
            var plain = "mysecret";

            var hash = _hasher.Hash(plain);

            hash.Should().NotBe(plain);
        }

        [Fact]
        public void Hash_IsDeterministic_SameInputProducesSameHash()
        {
            var password = "consistent";

            var hash1 = _hasher.Hash(password);
            var hash2 = _hasher.Hash(password);

            hash1.Should().Be(hash2);
        }

        [Fact]
        public void Hash_DifferentPasswords_ProduceDifferentHashes()
        {
            var hash1 = _hasher.Hash("password1");
            var hash2 = _hasher.Hash("password2");

            hash1.Should().NotBe(hash2);
        }

        [Fact]
        public void Hash_NullInput_ReturnsEmptyString()
        {
            var hash = _hasher.Hash(null!);

            hash.Should().Be(string.Empty);
        }

        [Fact]
        public void Hash_EmptyString_ReturnsSha256OfEmpty()
        {
            var hash = _hasher.Hash(string.Empty);

            // SHA256("") = e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
            hash.Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
        }

        [Fact]
        public void Verify_CorrectPassword_ReturnsTrue()
        {
            var password = "mypassword";
            var hash = _hasher.Hash(password);

            var result = _hasher.Verify(password, hash);

            result.Should().BeTrue();
        }

        [Fact]
        public void Verify_WrongPassword_ReturnsFalse()
        {
            var hash = _hasher.Hash("correct");

            var result = _hasher.Verify("incorrect", hash);

            result.Should().BeFalse();
        }

        [Fact]
        public void Verify_CaseSensitive_DifferentCaseReturnsFalse()
        {
            var hash = _hasher.Hash("Password");

            var result = _hasher.Verify("password", hash);

            result.Should().BeFalse();
        }

        [Fact]
        public void Verify_EmptyPasswordAgainstItsHash_ReturnsTrue()
        {
            var hash = _hasher.Hash(string.Empty);

            var result = _hasher.Verify(string.Empty, hash);

            result.Should().BeTrue();
        }
    }
}
