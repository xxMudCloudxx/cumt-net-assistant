using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace CampusNetAssistant
{
    /// <summary>用户配置数据模型</summary>
    public class AppConfig
    {
        public string StudentId         { get; set; } = "";
        public string EncryptedPassword { get; set; } = "";
        public int    OperatorIndex     { get; set; } = 0;   // 0=校园网 1=电信 2=联通 3=移动
        public string SelectedAdapter   { get; set; } = "";
        public bool   AutoStart         { get; set; } = false;
        public bool   AutoLogin         { get; set; } = false;
    }

    /// <summary>配置管理器：JSON 持久化 + DPAPI 密码加密 + 注册表自启</summary>
    public static class ConfigManager
    {
        private static readonly string ConfigDir  = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

        private const string AppName = "CampusNetAssistant";
        private const string RegKey  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        // ────────────────── 配置读写 ──────────────────

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch { /* 配置文件损坏则返回默认 */ }
            return new AppConfig();
        }

        public static void Save(AppConfig config)
        {
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }

        // ────────────────── DPAPI 密码加解密 ──────────────────

        /// <summary>使用 Windows DPAPI 加密密码（绑定当前用户）</summary>
        public static string EncryptPassword(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return "";
            byte[] data      = Encoding.UTF8.GetBytes(plainText);
            byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        /// <summary>使用 Windows DPAPI 解密密码</summary>
        public static string DecryptPassword(string cipher)
        {
            if (string.IsNullOrEmpty(cipher)) return "";
            try
            {
                byte[] data      = Convert.FromBase64String(cipher);
                byte[] decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch { return ""; }
        }

        // ────────────────── 开机自启 ──────────────────

        /// <summary>设置或取消开机自启动</summary>
        public static void SetAutoStart(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKey, true);
            if (key == null) return;

            if (enable)
                key.SetValue(AppName, $"\"{Application.ExecutablePath}\"");
            else
                key.DeleteValue(AppName, false);
        }

        /// <summary>查询当前是否已设置自启动</summary>
        public static bool IsAutoStartEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKey, false);
            return key?.GetValue(AppName) != null;
        }
    }
}
