using System;
using System.IO;
using System.Security.Cryptography;
using InkPulse.Worker.Infrastructure.Services.Security.Interfaces;
using InkPulse.Worker.Infrastructure.Services.Security.Models;
using Microsoft.Extensions.Options;

namespace InkPulse.Worker.Infrastructure.Services.Security.Implementations
{
    public class CryptographyService : ICryptographyService
    {
        private readonly string _key;
        private readonly string _iv;

        public CryptographyService(IOptions<AesSettings> aesSettingsOptions)
        {
            var settings = aesSettingsOptions.Value;
            _key = settings.Key;
            _iv = settings.Iv;
        }

        public string EncryptAes(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            using var aes = Aes.Create();
            aes.Key = Convert.FromBase64String(_key);
            aes.IV = Convert.FromBase64String(_iv);

            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using var msEncrypt = new MemoryStream();
            using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            {
                using (var swEncrypt = new StreamWriter(csEncrypt))
                {
                    swEncrypt.Write(plainText);
                }
            }
            return Convert.ToBase64String(msEncrypt.ToArray());
        }

        public string DecryptAes(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            try
            {
                using var aes = Aes.Create();
                aes.Key = Convert.FromBase64String(_key);
                aes.IV = Convert.FromBase64String(_iv);

                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using var msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText));
                using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                using var srDecrypt = new StreamReader(csDecrypt);
                return srDecrypt.ReadToEnd();
            }
            catch (Exception)
            {
                // Input không phải Base64 hợp lệ hoặc không mã hóa (ví dụ dữ liệu cũ plain text)
                // Fallback: trả về chuỗi gốc (không decrypt)
                return cipherText;
            }
        }
    }
}
