using System;
using System.Security.Cryptography;
using System.Text;

namespace TwoFA.Core
{
    /// <summary>
    /// 提供基于 Windows DPAPI (数据保护 API) 的安全加密与解密工具
    /// 确保保存在本地磁盘上的敏感数据（如密钥）不会被其他机器或用户轻易读取
    /// </summary>
    public static class CryptoHelper
    {
        // 额外的可选熵（Salt），增加破解难度
        private static readonly byte[] Entropy = new byte[] { 0x2A, 0x09, 0x1B, 0x4F, 0x7E, 0x33, 0x6A, 0x0C };

        /// <summary>
        /// 使用 Windows DPAPI 加密字符串
        /// </summary>
        /// <param name="plainText">明文字符串</param>
        /// <returns>加密后的 Base64 字符串</returns>
        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return string.Empty;
            }

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = ProtectedData.Protect(
                    plainBytes, 
                    Entropy, 
                    DataProtectionScope.CurrentUser
                );
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                // 如果在某些环境下 DPAPI 不可用，作为最后的兜底策略，使用简单的 XOR 混淆（但警告这不是真正的加密）
                // 实际生产中，.NET WinForms 在 Windows 桌面环境上 DPAPI 是 100% 可用的
                throw new CryptographicException("数据加密失败，Windows DPAPI 服务可能未启动", ex);
            }
        }

        /// <summary>
        /// 使用 Windows DPAPI 解密字符串
        /// </summary>
        /// <param name="encryptedText">Base64 格式的加密字符串</param>
        /// <returns>解密后的明文字符串</returns>
        public static string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
            {
                return string.Empty;
            }

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                byte[] plainBytes = ProtectedData.Unprotect(
                    encryptedBytes, 
                    Entropy, 
                    DataProtectionScope.CurrentUser
                );
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                throw new CryptographicException("数据解密失败，数据可能损坏或是在另一台计算机上尝试读取", ex);
            }
        }
    }
}
