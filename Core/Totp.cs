using System;
using System.Security.Cryptography;

namespace TwoFA.Core
{
    /// <summary>
    /// TOTP (基于时间的一次性密码) 算法实现 (符合 RFC 6238 规范)
    /// </summary>
    public static class Totp
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// 生成 TOTP 验证码（使用默认参数：6位、30秒、SHA1）
        /// </summary>
        public static string GenerateCode(byte[] secret, DateTime time)
        {
            return GenerateCode(secret, time, 6, 30, "SHA1");
        }

        /// <summary>
        /// 生成 TOTP 验证码
        /// </summary>
        /// <param name="secret">Base32 解码后的密钥字节数组</param>
        /// <param name="time">要计算验证码的时间(通常为 DateTime.UtcNow)</param>
        /// <param name="digits">验证码位数，支持 6、7、8 位</param>
        /// <param name="period">时间步长，单位为秒，通常为 30</param>
        /// <param name="algorithm">哈希算法，支持 SHA1、SHA256、SHA512</param>
        /// <returns>生成的数字验证码字符串</returns>
        public static string GenerateCode(byte[] secret, DateTime time, int digits, int period, string algorithm)
        {
            if (secret == null || secret.Length == 0)
            {
                return string.Empty;
            }

            long unixTimestamp = (long)(time.ToUniversalTime() - UnixEpoch).TotalSeconds;
            long counter = unixTimestamp / period;

            return GenerateCodeFromCounter(secret, counter, digits, algorithm);
        }

        /// <summary>
        /// 获取当前时间步长内剩余的有效秒数（默认 30 秒周期）
        /// </summary>
        public static int GetRemainingSeconds(DateTime time)
        {
            return GetRemainingSeconds(time, 30);
        }

        /// <summary>
        /// 获取当前时间步长内剩余的有效秒数
        /// </summary>
        /// <param name="time">当前时间</param>
        /// <param name="period">时间步长，单位为秒</param>
        /// <returns>剩余秒数</returns>
        public static int GetRemainingSeconds(DateTime time, int period)
        {
            long unixTimestamp = (long)(time.ToUniversalTime() - UnixEpoch).TotalSeconds;
            return period - (int)(unixTimestamp % period);
        }

        /// <summary>
        /// 从指定的计数器值生成 HOTP 验证码
        /// </summary>
        private static string GenerateCodeFromCounter(byte[] secret, long counter, int digits, string algorithm)
        {
            // 将 counter 转换为 8 字节大端（Big-Endian）字节数组
            byte[] counterBytes = BitConverter.GetBytes(counter);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(counterBytes);
            }

            byte[] hash;
            using (HMAC hmac = CreateHmac(algorithm, secret))
            {
                hash = hmac.ComputeHash(counterBytes);
            }

            // 动态截断 (Dynamic Truncation)
            int offset = hash[hash.Length - 1] & 0x0F;
            int binary = ((hash[offset] & 0x7F) << 24)
                       | ((hash[offset + 1] & 0xFF) << 16)
                       | ((hash[offset + 2] & 0xFF) << 8)
                       | (hash[offset + 3] & 0xFF);

            int mod = (int)Math.Pow(10, digits);
            int code = binary % mod;

            return code.ToString().PadLeft(digits, '0');
        }

        /// <summary>
        /// 根据算法名称创建对应的 HMAC 实例
        /// </summary>
        private static HMAC CreateHmac(string algorithm, byte[] secret)
        {
            string algUpper = (algorithm ?? "SHA1").ToUpperInvariant();
            switch (algUpper)
            {
                case "SHA256":
                    return new HMACSHA256(secret);
                case "SHA384":
                    // .NET 3.5 原生支持 HMACSHA384
                    return new HMACSHA384(secret);
                case "SHA512":
                    return new HMACSHA512(secret);
                case "SHA1":
                default:
                    return new HMACSHA1(secret);
            }
        }
    }
}
