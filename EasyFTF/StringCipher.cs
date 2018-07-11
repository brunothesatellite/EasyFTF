using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System.Security.Cryptography;
using System.IO;

namespace EasyFTF
{
    /// <summary>
    /// Class to perform simple and portable string cyphering / decyphering
    /// </summary>
    public static class StringCipher
    {
        // This constant string is used as a "salt" value for the PasswordDeriveBytes function calls.
        // This size of the IV (in bytes) must = (keysize / 8).  Default keysize is 256, so the IV must be
        // 32 bytes long.  Using a 16 character string here gives us 32 bytes when converted to a byte array.
        private static readonly byte[] initVectorBytes = Encoding.ASCII.GetBytes("tu89geji340t89u2");

        // la passphrase de MGM
        private const string passwordMGM = "kL$ùµ$y+{$5bH<mK67wkQw[=Zjlk778{";
        private const string keywordMGM = "m9(-`é^^S)>£7nqb";

        // This constant is used to determine the keysize of the encryption algorithm.
        private const int keysize = 256;

        /// <summary>
        /// Performs string encryption with padding
        /// </summary>
        /// <param name="plainText">plain text</param>
        /// <returns>encrypted text</returns>
        public static string CustomEncrypt(string plainText)
        {
            // On insère la clé devant le texte à encrypter
            return Encrypt(keywordMGM + plainText, passwordMGM);
        }

        /// <summary>
        /// Performs string encryption without padding
        /// </summary>
        /// <param name="plainText">plain text</param>
        /// <returns>encrypted text</returns>
        public static string CustomEncryptNoPadding(string plainText)
        {
            if (plainText.Length < passwordMGM.Length)
                return "";
            else
                return Encrypt(plainText, passwordMGM);
        }

        /// <summary>
        /// Performs string encryption
        /// </summary>
        /// <param name="plainText">plain text</param>
        /// <param name="passPhrase">pass phrase to authenticate encrypted string</param>
        /// <returns>encrypted string</returns>
        public static string Encrypt(string plainText, string passPhrase)
        {
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase, null);
            {
                byte[] keyBytes = password.GetBytes(keysize / 8);
                using (RijndaelManaged symmetricKey = new RijndaelManaged())
                {
                    symmetricKey.Mode = CipherMode.CBC;
                    using (ICryptoTransform encryptor = symmetricKey.CreateEncryptor(keyBytes, initVectorBytes))
                    {
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                            {
                                cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                                cryptoStream.FlushFinalBlock();
                                byte[] cipherTextBytes = memoryStream.ToArray();
                                return Convert.ToBase64String(cipherTextBytes);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Performs string decryption without padding
        /// </summary>
        /// <param name="cipherText">encrypted text</param>
        /// <param name="res">decrypted text</param>
        /// <returns>true if decryption succeeded</returns>
        public static bool CustomDecryptNoPadding(string cipherText, ref string res)
        {
            try
            {
                if (cipherText.Length < passwordMGM.Length)
                {
                    res = "";
                    return false;
                }

                res = Decrypt(cipherText, passwordMGM);
                return true;
            }
            catch (Exception)
            {
                res = "";
                return false;
            }
        }

        /// <summary>
        /// Performs string decryption with padding
        /// </summary>
        /// <param name="cipherText">encrypted text</param>
        /// <param name="res">decrypted text</param>
        /// <returns>true if decryption succeeded</returns>
        public static bool CustomDecrypt(string cipherText, ref string res)
        {
            try
            {
                res = Decrypt(cipherText, passwordMGM);
                //  Si tout va bien, le début du texte décodé contient le passwordMGM
                if (res.StartsWith(keywordMGM))
                {
                    // On vire le password
                    res = res.Substring(keywordMGM.Length);
                    return true;
                }
                else
                {
                    res = "";
                    return false;
                }
            }
            catch (Exception)
            {
                res = "";
                return false;
            }
        }

        /// <summary>
        /// Performs string decryption
        /// </summary>
        /// <param name="cipherText">encrypted text</param>
        /// <param name="passPhrase">pass phrase to authenticate decrypted text</param>
        /// <returns>decrypted text</returns>
        public static string Decrypt(string cipherText, string passPhrase)
        {
            byte[] cipherTextBytes = Convert.FromBase64String(cipherText);
            PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase, null);
            {
                byte[] keyBytes = password.GetBytes(keysize / 8);
                using (RijndaelManaged symmetricKey = new RijndaelManaged())
                {
                    symmetricKey.Mode = CipherMode.CBC;
                    using (ICryptoTransform decryptor = symmetricKey.CreateDecryptor(keyBytes, initVectorBytes))
                    {
                        using (MemoryStream memoryStream = new MemoryStream(cipherTextBytes))
                        {
                            using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                            {
                                byte[] plainTextBytes = new byte[cipherTextBytes.Length];
                                int decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
                                return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount);
                            }
                        }
                    }
                }
            }
        }

    }

}