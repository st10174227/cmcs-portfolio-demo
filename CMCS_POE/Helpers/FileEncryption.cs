using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace CMCS_Part2.Helpers
{
    public static class FileEncryptionHelper
    {
        private static readonly byte[] Key;
        private static readonly byte[] IV;

        static FileEncryptionHelper()
        {
            var defaultKey = "A1B2C3D4E5F6G7H8I9J0K1L2M3N4O5P6"; 
            var defaultIV = "H8G7F6E5D4C3B2A1";              

            Key = Encoding.UTF8.GetBytes(defaultKey);
            IV = Encoding.UTF8.GetBytes(defaultIV);
        }

        public static void EncryptFile(string inputFile, string outputFile)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Key;
                aes.IV = IV;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (FileStream fsInput = new FileStream(inputFile, FileMode.Open, FileAccess.Read))
                using (FileStream fsEncrypted = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
                using (CryptoStream cs = new CryptoStream(fsEncrypted, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    fsInput.CopyTo(cs);
                }
            }
        }

        public static void DecryptFile(string inputFile, string outputFile)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Key;
                aes.IV = IV;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (FileStream fsEncrypted = new FileStream(inputFile, FileMode.Open, FileAccess.Read))
                using (FileStream fsDecrypted = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
                using (CryptoStream cs = new CryptoStream(fsEncrypted, aes.CreateDecryptor(), CryptoStreamMode.Read))
                {
                    cs.CopyTo(fsDecrypted);
                }
            }
        }
    }
}
//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].