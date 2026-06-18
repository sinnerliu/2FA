using System;
using System.Text;

namespace TwoFA.Core
{
    /// <summary>
    /// Base32 编解码实现（符合 RFC 4648 规范）
    /// </summary>
    public static class Base32
    {
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        /// <summary>
        /// 将 Base32 编码的字符串解码为字节数组
        /// </summary>
        /// <param name="input">Base32 编码字符串</param>
        /// <returns>解码后的字节数组</returns>
        public static byte[] Decode(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return new byte[0];
            }

            // 清理输入字符串：转换为大写，并移除非法字符（如空格、连字符等）
            input = input.ToUpperInvariant().Replace("-", "").Replace(" ", "");

            // 移除填充字符 '='
            input = input.TrimEnd('=');

            if (input.Length == 0)
            {
                return new byte[0];
            }

            int byteCount = input.Length * 5 / 8;
            byte[] returnArray = new byte[byteCount];

            byte curByte = 0;
            int bitsRemaining = 8;
            int arrayIndex = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                int value = Alphabet.IndexOf(c);

                if (value < 0)
                {
                    throw new ArgumentException(string.Format("输入字符串包含非法 Base32 字符: '{0}'", c));
                }

                if (bitsRemaining > 5)
                {
                    int mask = value << (bitsRemaining - 5);
                    curByte = (byte)(curByte | mask);
                    bitsRemaining -= 5;
                }
                else
                {
                    int mask = value >> (5 - bitsRemaining);
                    curByte = (byte)(curByte | mask);
                    if (arrayIndex < returnArray.Length)
                    {
                        returnArray[arrayIndex++] = curByte;
                    }

                    curByte = (byte)((value << (3 + bitsRemaining)) & 0xFF);
                    bitsRemaining = bitsRemaining + 8 - 5;
                }
            }

            // 如果最后还有剩余的字节且尚未写入数组，这里忽略，因为 byteCount 已经是截断计算的
            return returnArray;
        }

        /// <summary>
        /// 将字节数组编码为 Base32 字符串
        /// </summary>
        /// <param name="data">字节数组</param>
        /// <returns>Base32 编码后的字符串</returns>
        public static string Encode(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder result = new StringBuilder((data.Length * 8 + 4) / 5);

            int currentByte = 0;
            int digit = 0;
            int i = 0;

            while (i < data.Length)
            {
                int val = data[i];

                if (digit < 5)
                {
                    currentByte = (currentByte << 8) | val;
                    digit += 8;
                    i++;
                }

                while (digit >= 5)
                {
                    digit -= 5;
                    int index = (currentByte >> digit) & 0x1F;
                    result.Append(Alphabet[index]);
                }
            }

            if (digit > 0)
            {
                int index = (currentByte << (5 - digit)) & 0x1F;
                result.Append(Alphabet[index]);
            }

            // 添加填充 '=' 使长度符合 8 的倍数
            while (result.Length % 8 != 0)
            {
                result.Append('=');
            }

            return result.ToString();
        }
    }
}
