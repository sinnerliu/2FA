using System;
using System.Collections.Generic;
using System.Text;

namespace TwoFA.Core
{
    /// <summary>
    /// 解析和生成符合 Google Authenticator 规范的 otpauth://totp/ URI
    /// </summary>
    public class OtpAuthUri
    {
        public string Type { get; set; } // 目前仅支持 "totp"
        public string Label { get; set; }
        public string Issuer { get; set; }
        public string Account { get; set; }
        public string Secret { get; set; }
        public int Digits { get; set; }
        public int Period { get; set; }
        public string Algorithm { get; set; }

        public OtpAuthUri()
        {
            Type = "totp";
            Digits = 6;
            Period = 30;
            Algorithm = "SHA1";
        }

        /// <summary>
        /// 解析 otpauth URI 字符串
        /// </summary>
        public static OtpAuthUri Parse(string uriString)
        {
            if (string.IsNullOrEmpty(uriString))
            {
                throw new ArgumentNullException("uriString");
            }

            // 基础格式校验
            if (!uriString.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException("无效的 URI scheme，必须以 otpauth:// 开头");
            }

            Uri uri;
            try
            {
                uri = new Uri(uriString);
            }
            catch (Exception ex)
            {
                throw new FormatException("无法解析 URI 格式", ex);
            }

            // 检查类型是否为 totp
            string type = uri.Host;
            if (!string.Equals(type, "totp", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("仅支持 totp 类型的 2FA 密钥");
            }

            OtpAuthUri result = new OtpAuthUri();
            result.Type = "totp";

            // 解析 Label (通常在 Path 中，格式为 /Issuer:Account 或 /Account)
            // Path 会带有开头的斜杠 '/'
            string path = Uri.UnescapeDataString(uri.AbsolutePath);
            if (path.StartsWith("/"))
            {
                path = path.Substring(1);
            }

            result.Label = path;
            int colonIndex = path.IndexOf(':');
            if (colonIndex >= 0)
            {
                result.Issuer = path.Substring(0, colonIndex).Trim();
                result.Account = path.Substring(colonIndex + 1).Trim();
            }
            else
            {
                result.Account = path.Trim();
            }

            // 解析 Query 参数
            string query = uri.Query;
            if (query.StartsWith("?"))
            {
                query = query.Substring(1);
            }

            Dictionary<string, string> parameters = ParseQueryString(query);

            if (parameters.ContainsKey("secret"))
            {
                result.Secret = parameters["secret"];
            }
            else
            {
                throw new FormatException("URI 中未包含必需的 secret 参数");
            }

            if (parameters.ContainsKey("issuer"))
            {
                result.Issuer = parameters["issuer"];
            }

            if (parameters.ContainsKey("digits"))
            {
                int digits;
                if (int.TryParse(parameters["digits"], out digits))
                {
                    result.Digits = digits;
                }
            }

            if (parameters.ContainsKey("period"))
            {
                int period;
                if (int.TryParse(parameters["period"], out period))
                {
                    result.Period = period;
                }
            }

            if (parameters.ContainsKey("algorithm"))
            {
                result.Algorithm = parameters["algorithm"].ToUpperInvariant();
            }

            // 如果 Label 包含 Issuer 而参数中也有，以参数中的为准（或者作为互补）
            if (string.IsNullOrEmpty(result.Issuer) && !string.IsNullOrEmpty(result.Label))
            {
                // 再次尝试提取
                int idx = result.Label.IndexOf(':');
                if (idx > 0)
                {
                    result.Issuer = result.Label.Substring(0, idx).Trim();
                }
            }

            return result;
        }

        /// <summary>
        /// 生成 otpauth URI 字符串
        /// </summary>
        public string ToUriString()
        {
            if (string.IsNullOrEmpty(Secret))
            {
                throw new InvalidOperationException("未设置 Secret 密钥，无法生成 URI");
            }

            // Label 格式为 Issuer:Account，如果没有 Issuer，则只用 Account
            string labelPart;
            if (!string.IsNullOrEmpty(Issuer) && !string.IsNullOrEmpty(Account))
            {
                labelPart = string.Format("{0}:{1}", Issuer, Account);
            }
            else
            {
                labelPart = Account ?? string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("otpauth://totp/{0}?", Uri.EscapeDataString(labelPart));
            sb.AppendFormat("secret={0}", Uri.EscapeDataString(Secret));

            if (!string.IsNullOrEmpty(Issuer))
            {
                sb.AppendFormat("&issuer={0}", Uri.EscapeDataString(Issuer));
            }

            if (Digits != 6)
            {
                sb.AppendFormat("&digits={0}", Digits);
            }

            if (Period != 30)
            {
                sb.AppendFormat("&period={0}", Period);
            }

            if (!string.Equals(Algorithm, "SHA1", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(Algorithm))
            {
                sb.AppendFormat("&algorithm={0}", Algorithm.ToUpperInvariant());
            }

            return sb.ToString();
        }

        private static Dictionary<string, string> ParseQueryString(string query)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query))
            {
                return dict;
            }

            string[] pairs = query.Split('&');
            foreach (string pair in pairs)
            {
                if (string.IsNullOrEmpty(pair))
                {
                    continue;
                }

                string[] kv = pair.Split('=');
                string key = Uri.UnescapeDataString(kv[0]);
                string val = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;

                dict[key] = val;
            }

            return dict;
        }
    }
}
