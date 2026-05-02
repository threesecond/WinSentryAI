using System;
using System.Security.Cryptography;
using System.Text;

namespace WinSentryAI.Services
{
    public static class EncryptionHelper
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WinSentryAI-Entropy-2026");

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            
            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] cipherBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(cipherBytes);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static bool TryDecrypt(string cipherText, out string? plainText)
        {
            plainText = null;
            if (string.IsNullOrEmpty(cipherText)) return true;
            
            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                byte[] plainBytes = ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.CurrentUser);
                plainText = Encoding.UTF8.GetString(plainBytes);
                return true;
            }
            catch (CryptographicException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string? Decrypt(string cipherText) =>
            TryDecrypt(cipherText, out var plainText) ? plainText : null;
    }
}
