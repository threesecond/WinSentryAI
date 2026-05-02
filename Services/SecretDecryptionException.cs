using System;

namespace WinSentryAI.Services
{
    public sealed class SecretDecryptionException : Exception
    {
        public SecretDecryptionException(string key)
            : base($"The saved secret '{key}' could not be decrypted with the current Windows user account.")
        {
            Key = key;
        }

        public string Key { get; }
    }
}
