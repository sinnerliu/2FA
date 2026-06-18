using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using TwoFA.Models;

namespace TwoFA.Data
{
    /// <summary>
    /// 数据持久化管理类，支持账户数据保存、配置读取、自动备份等
    /// </summary>
    public class DataStore
    {
        private const string AppRegistryKey = "2FA_Authenticator";
        private const string DataFileName = "data.2fa";
        private const string SettingsFileName = "settings.json";

        public List<TotpAccount> Accounts { get; private set; }
        public AppSettings Settings { get; private set; }

        private readonly JavaScriptSerializer _serializer;
        private string _appDir;
        private string _dataDir;

        public DataStore()
        {
            Accounts = new List<TotpAccount>();
            Settings = new AppSettings();
            _serializer = new JavaScriptSerializer();
            _appDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // 数据目录设定为 exe 同级的 data/
            _dataDir = Path.Combine(_appDir, "data");
            EnsureDataDirectoryExists();
        }

        private void EnsureDataDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_dataDir))
                {
                    Directory.CreateDirectory(_dataDir);
                }
            }
            catch { }
        }

        /// <summary>
        /// 获取数据文件的实际绝对路径
        /// </summary>
        public string GetDataFilePath()
        {
            EnsureDataDirectoryExists();
            if (!string.IsNullOrEmpty(Settings.DataFilePath))
            {
                try
                {
                    // 如果是相对路径，转换为绝对路径
                    return Path.GetFullPath(Path.Combine(_dataDir, Settings.DataFilePath));
                }
                catch
                {
                    // 解析失败时，回退到默认
                }
            }
            return Path.Combine(_dataDir, DataFileName);
        }

        /// <summary>
        /// 获取配置文件的实际绝对路径
        /// </summary>
        private string GetSettingsFilePath()
        {
            EnsureDataDirectoryExists();
            return Path.Combine(_dataDir, SettingsFileName);
        }

        /// <summary>
        /// 初始化加载所有数据与设置
        /// </summary>
        public void LoadAll()
        {
            LoadSettings();
            LoadAccounts();
        }

        /// <summary>
        /// 加载应用配置
        /// </summary>
        private void LoadSettings()
        {
            string path = GetSettingsFilePath();
            if (!File.Exists(path))
            {
                Settings = new AppSettings();
                return;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                Settings = _serializer.Deserialize<AppSettings>(json);
                if (Settings == null)
                {
                    Settings = new AppSettings();
                }
            }
            catch
            {
                Settings = new AppSettings();
            }
        }

        /// <summary>
        /// 保存应用配置
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                string path = GetSettingsFilePath();
                string json = _serializer.Serialize(Settings);
                File.WriteAllText(path, json, Encoding.UTF8);

                // 更新开机自启动设置
                SetAutoStart(Settings.AutoStart);
            }
            catch (Exception ex)
            {
                throw new IOException("无法保存设置文件: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// 加载账户数据
        /// </summary>
        private void LoadAccounts()
        {
            string path = GetDataFilePath();
            if (!File.Exists(path))
            {
                Accounts = new List<TotpAccount>();
                return;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                var list = _serializer.Deserialize<List<TotpAccount>>(json);
                if (list != null)
                {
                    // 排序
                    list.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
                    Accounts = list;
                }
                else
                {
                    Accounts = new List<TotpAccount>();
                }
            }
            catch
            {
                Accounts = new List<TotpAccount>();
                // 如果主文件加载失败，尝试从最近的备份文件恢复
                TryRestoreFromBackup();
            }
        }

        /// <summary>
        /// 保存账户数据（带备份机制）
        /// </summary>
        public void SaveAccounts()
        {
            string path = GetDataFilePath();

            try
            {
                // 先备份原有的数据文件
                BackupDataFile(path);

                // 序列化
                string json = _serializer.Serialize(Accounts);
                File.WriteAllText(path, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new IOException("保存数据失败: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// 备份数据文件（使用时间戳命名，并滚动清理多余备份）
        /// </summary>
        private void BackupDataFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            try
            {
                EnsureDataDirectoryExists();
                // 1. 拷贝当前数据为时间戳备份文件
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFileName = string.Format("data_{0}.bak", timestamp);
                string backupFilePath = Path.Combine(_dataDir, backupFileName);

                File.Copy(filePath, backupFilePath, true);

                // 2. 滚动删除旧备份（仅保留最新的 5 个版本）
                string[] bakFiles = Directory.GetFiles(_dataDir, "data_*.bak");
                if (bakFiles != null && bakFiles.Length > 5)
                {
                    List<string> bakList = new List<string>(bakFiles);
                    bakList.Sort(StringComparer.OrdinalIgnoreCase); // 时间戳正序（早期的在前）

                    int deleteCount = bakList.Count - 5;
                    for (int i = 0; i < deleteCount; i++)
                    {
                        try
                        {
                            File.Delete(bakList[i]);
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 尝试从备份文件还原数据（按时间戳由新到旧依次尝试）
        /// </summary>
        private bool TryRestoreFromBackup()
        {
            try
            {
                EnsureDataDirectoryExists();
                string[] bakFiles = Directory.GetFiles(_dataDir, "data_*.bak");
                if (bakFiles == null || bakFiles.Length == 0)
                {
                    return false;
                }

                // 排序并将最新的排在最前
                List<string> bakList = new List<string>(bakFiles);
                bakList.Sort(StringComparer.OrdinalIgnoreCase);
                bakList.Reverse();

                string baseFieldPath = GetDataFilePath();

                foreach (string bakPath in bakList)
                {
                    try
                    {
                        string json = File.ReadAllText(bakPath, Encoding.UTF8);
                        var list = _serializer.Deserialize<List<TotpAccount>>(json);
                        if (list != null)
                        {
                            Accounts = list;
                            // 成功恢复，覆写损坏的主文件
                            File.Copy(bakPath, baseFieldPath, true);
                            return true;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// 设置/取消开机自启动
        /// </summary>
        private void SetAutoStart(bool autoStart)
        {
            try
            {
                using (RegistryKey runKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (runKey == null) return;

                    string appPath = System.Windows.Forms.Application.ExecutablePath;
                    if (autoStart)
                    {
                        runKey.SetValue(AppRegistryKey, string.Format("\"{0}\" --minimized", appPath));
                    }
                    else
                    {
                        if (runKey.GetValue(AppRegistryKey) != null)
                        {
                            runKey.DeleteValue(AppRegistryKey);
                        }
                    }
                }
            }
            catch
            {
                // 忽略注册表操作失败（可能是由于权限受限）
            }
        }
    }
}
