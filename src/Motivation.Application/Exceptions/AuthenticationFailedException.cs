using System;

namespace Motivation.Application.Exceptions
{
    public class AuthenticationFailedException : Exception
    {
        public AuthenticationFailedException(string message) : base(message) { }
    }
}
