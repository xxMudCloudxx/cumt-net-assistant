using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace CampusNetAssistant
{
    /// <summary>运营商类型枚举</summary>
    public enum OperatorType
    {
        Campus  = 0, // 校园网（无后缀）
        Telecom = 1, // 电信
        Unicom  = 2, // 联通
        CMCC    = 3  // 移动
    }

    /// <summary>登录结果</summary>
    public class LoginResult
    {
        public bool   Success { get; set; }
        public string Message { get; set; } = "";
    }

    /// <summary>校园网 eportal 认证核心</summary>
    public static class LoginService
    {
        private static readonly HttpClient Client = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private const string BaseUrl = "http://10.2.5.251:801/eportal/";

        // ── 已知的 Base64 错误消息 → 友好中文 ──
        private static readonly Dictionary<string, string> KnownErrors = new()
        {
            { "dXNlcmlkIGVycm9yMg==",          "账号或密码错误" },
            { "dXNlcmlkIGVycm9yMQ==",          "账号不存在，请切换运营商再尝试" },
            { "UmFkOkxpbWl0IFVzZXJzIEVycg==",  "登录超限，请在用户自助服务系统下线终端" }
        };

        /// <summary>根据运营商类型返回 URL 中的后缀</summary>
        private static string GetSuffix(OperatorType op) => op switch
        {
            OperatorType.Telecom => "%40telecom",
            OperatorType.Unicom  => "%40unicom",
            OperatorType.CMCC    => "%40cmcc",
            _                    => ""          // 校园网无后缀
        };

        /// <summary>发起校园网登录请求</summary>
        public static async Task<LoginResult> LoginAsync(string username, string password, OperatorType op)
        {
            try
            {
                string account = Uri.EscapeDataString(username) + GetSuffix(op);
                string url = $"{BaseUrl}?c=Portal&a=login&login_method=1" +
                             $"&user_account={account}" +
                             $"&user_password={Uri.EscapeDataString(password)}";

                string raw = await Client.GetStringAsync(url);

                // 服务器返回格式可能是 "(...)" 或 JSONP "cbXXX(...)"
                // 统一用正则提取花括号里的 JSON 内容
                var m = Regex.Match(raw, @"\(\s*(\{[\s\S]*\})\s*\)");
                string body = m.Success
                    ? m.Groups[1].Value
                    : (raw.Length > 2 ? raw[1..^1] : raw);

                // ── 判断成功 ──
                if (body.Contains("\"result\":\"1\""))
                    return new LoginResult { Success = true, Message = "校园网登录成功！" };

                if (body.Contains("\"ret_code\":\"2\""))
                    return new LoginResult { Success = true, Message = "您已登录校园网" };

                // ── 提取 msg 字段 ──
                var msgMatch = Regex.Match(body, "\"msg\"\\s*:\\s*\"([^\"]+)\"");
                if (msgMatch.Success)
                {
                    string msgVal = msgMatch.Groups[1].Value;

                    // 优先匹配写死的已知错误码
                    if (KnownErrors.TryGetValue(msgVal, out string? friendly))
                        return new LoginResult { Success = false, Message = friendly };

                    // 兜底：尝试 Base64 解码
                    try
                    {
                        byte[] bytes = Convert.FromBase64String(msgVal);
                        return new LoginResult { Success = false, Message = Encoding.UTF8.GetString(bytes) };
                    }
                    catch
                    {
                        return new LoginResult { Success = false, Message = msgVal };
                    }
                }

                return new LoginResult { Success = false, Message = "未知错误" };
            }
            catch (HttpRequestException)
            {
                return new LoginResult { Success = false, Message = "网络连接失败，请确保已连接校园网 (CUMT_Stu)" };
            }
            catch (TaskCanceledException)
            {
                return new LoginResult { Success = false, Message = "请求超时，认证服务器无响应" };
            }
            catch (Exception ex)
            {
                return new LoginResult { Success = false, Message = $"登录异常: {ex.Message}" };
            }
        }

        /// <summary>发起校园网注销请求</summary>
        public static async Task<LoginResult> LogoutAsync()
        {
            try
            {
                string url = $"{BaseUrl}?c=Portal&a=logout&login_method=1";
                await Client.GetStringAsync(url);
                return new LoginResult { Success = true, Message = "已断开校园网连接" };
            }
            catch (Exception ex)
            {
                return new LoginResult { Success = false, Message = $"断开失败: {ex.Message}" };
            }
        }
    }
}
