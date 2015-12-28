using System;

namespace Vault.Core.Exceptions
{
    public class VaultException : Exception
    {
        public VaultException()
        {
        }

        public VaultException(string message) : base(message)
        {
        }

        public VaultException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
