using System.Diagnostics;
using System.Net.NetworkInformation;

namespace CampusNetAssistant
{
    /// <summary>网络适配器管理：列出适配器 / 提权禁用或启用</summary>
    public static class AdapterHelper
    {
        /// <summary>获取所有物理网络适配器名称（排除回环和虚拟适配器）</summary>
        public static List<string> GetAllAdapters()
        {
            var list = new List<string>();
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                // 跳过回环
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                // 跳过常见虚拟适配器
                string desc = nic.Description;
                if (desc.Contains("Virtual",  StringComparison.OrdinalIgnoreCase) ||
                    desc.Contains("VMware",   StringComparison.OrdinalIgnoreCase) ||
                    desc.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase) ||
                    desc.Contains("Hyper-V",  StringComparison.OrdinalIgnoreCase) ||
                    desc.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase))
                    continue;

                list.Add(nic.Name);
            }
            return list;
        }

        /// <summary>
        /// 禁用或启用指定名称的网络适配器。
        /// 需要管理员权限，会以 runas 方式弹出 UAC 提权弹窗。
        /// </summary>
        /// <param name="adapterName">适配器名称（如"以太网"）</param>
        /// <param name="enable">true=启用 false=禁用</param>
        /// <returns>操作是否成功</returns>
        public static bool SetAdapterState(string adapterName, bool enable)
        {
            try
            {
                string action = enable ? "enable" : "disable";
                var psi = new ProcessStartInfo
                {
                    FileName        = "netsh",
                    Arguments       = $"interface set interface \"{adapterName}\" {action}",
                    Verb            = "runas",        // 提权
                    UseShellExecute = true,
                    WindowStyle     = ProcessWindowStyle.Hidden,
                    CreateNoWindow  = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                if (!process.WaitForExit(5000))
                    return false; // UAC 超时或用户未响应

                return process.ExitCode == 0;
            }
            catch
            {
                // 用户拒绝 UAC 或其他错误
                return false;
            }
        }

        /// <summary>检查指定适配器当前是否处于启用状态（OperationalStatus）</summary>
        public static bool IsAdapterEnabled(string adapterName)
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.Name == adapterName)
                    return nic.OperationalStatus == OperationalStatus.Up;
            }
            return false;
        }

        /// <summary>
        /// 检查指定适配器的管理状态是否为启用（通过 netsh 查询真实管理状态）。
        /// 与 IsAdapterEnabled 不同，此方法区分"管理员禁用"和"物理断开"。
        /// </summary>
        public static bool IsAdapterAdminEnabled(string adapterName)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "netsh",
                    Arguments              = $"interface show interface name=\"{adapterName}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process == null) return true;

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(3000);

                // 逐行查找管理状态行
                foreach (var line in output.Split('\n'))
                {
                    var trimmed = line.Trim();
                    // English: "Administrative state:   Enabled / Disabled"
                    // Chinese: "管理状态:               已启用 / 已禁用"
                    if (trimmed.StartsWith("Admin", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("管理"))
                    {
                        // 如果该行包含 Disabled / 已禁用，则适配器为禁用状态
                        if (trimmed.Contains("Disabled", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.Contains("已禁用"))
                            return false;
                        return true;
                    }
                }
                return true; // 无法判断时默认为启用
            }
            catch
            {
                return true; // 出错时默认为启用
            }
        }
    }
}
