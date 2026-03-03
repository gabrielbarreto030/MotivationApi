using System;

namespace Motivation.Application.Exceptions
{
    public class EmailAlreadyInUseException : Exception
    {
        public EmailAlreadyInUseException(string email)
            : base($"Email '{email}' is already registered.")
        {
        }
    }
}