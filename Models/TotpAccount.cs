using System;
using System.Web.Script.Serialization;
using TwoFA.Core;

namespace TwoFA.Models
{
    /// <summary>
    /// 2FA 账户实体
    /// </summary>
    public class TotpAccount
    {
        public string Id { get; set; }
        public string Issuer { get; set; }
        public string Account { get; set; }
        
        // 保存在磁盘上的加密密钥 (Base64格式)
        public string EncryptedSecret { get; set; }
        
        public string Remark { get; set; }
        public int Digits { get; set; }
        public int Period { get; set; }
        public string Algorithm { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public TotpAccount()
        {
            Id = Guid.NewGuid().ToString();
            Digits = 6;
            Period = 30;
            Algorithm = "SHA1";
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        private string _plainSecret;

        /// <summary>
        /// 内存明文密钥（不序列化保存到 JSON 中）
        /// </summary>
        [ScriptIgnore]
        public string PlainSecret
        {
            get
            {
                if (string.IsNullOrEmpty(_plainSecret) && !string.IsNullOrEmpty(EncryptedSecret))
                {
                    try
                    {
                        _plainSecret = CryptoHelper.Decrypt(EncryptedSecret);
                    }
                    catch
                    {
                        // 如果在另一台电脑上读取，解密会失败
                        _plainSecret = string.Empty;
                    }
                }
                return _plainSecret;
            }
            set
            {
                _plainSecret = value;
                if (!string.IsNullOrEmpty(value))
                {
                    EncryptedSecret = CryptoHelper.Encrypt(value);
                }
                else
                {
                    EncryptedSecret = string.Empty;
                }
            }
        }
    }
}
