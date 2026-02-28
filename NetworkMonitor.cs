using System.Net.NetworkInformation;

namespace CampusNetAssistant
{
    /// <summary>
    /// 网络守护模块：
    /// 1. 监听网卡状态变化（插拔网线 / Wi-Fi 重连）→ 自动登录
    /// 2. 心跳 Ping 外网（5 分钟一次）→ 不通则自动登录
    /// 3. 连续 3 次失败后熔断，仅允许手动登录
    /// </summary>
    public class NetworkMonitor : IDisposable
    {
        private System.Threading.Timer? _heartbeatTimer;
        private CancellationTokenSource? _debounceCts;
        private volatile int  _consecutiveFailures;
        private bool _disposed;
        private bool _running;

        private const int MaxFailures        = 3;
        private const int HeartbeatIntervalMs = 5 * 60 * 1000; // 5 分钟

        /// <summary>状态变更通知（用于 UI 显示与气泡提示）</summary>
        public event Action<string>? StatusChanged;

        /// <summary>需要重新登录时触发</summary>
        public event Func<Task>? ReloginRequested;

        /// <summary>是否已熔断（连续失败 >= 3）</summary>
        public bool IsFrozen => _consecutiveFailures >= MaxFailures;

        /// <summary>启动网络守护</summary>
        public void Start()
        {
            if (_running) return;
            _running = true;

            NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
            _heartbeatTimer = new System.Threading.Timer(
                HeartbeatCallback, null, HeartbeatIntervalMs, HeartbeatIntervalMs);
        }

        /// <summary>停止网络守护</summary>
        public void Stop()
        {
            _running = false;
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
            _heartbeatTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>登录成功后重置熔断计数</summary>
        public void ResetFailures() => Interlocked.Exchange(ref _consecutiveFailures, 0);

        /// <summary>登录失败后递增计数</summary>
        public void RecordFailure()
        {
            Interlocked.Increment(ref _consecutiveFailures);
            if (IsFrozen)
                StatusChanged?.Invoke($"连续 {MaxFailures} 次登录失败，已暂停自动重连（请手动登录）");
        }

        // ── 网卡状态变化 ──
        private async void OnNetworkChanged(object? sender, NetworkAvailabilityEventArgs e)
        {
            try
            {
                if (!e.IsAvailable || IsFrozen || !_running) return;

                // 取消之前的延迟登录，避免快速网络切换时并发重登
                _debounceCts?.Cancel();
                _debounceCts = new CancellationTokenSource();
                var token = _debounceCts.Token;

                StatusChanged?.Invoke("检测到网络重新连接，3 秒后自动登录…");
                await Task.Delay(3000, token);

                if (ReloginRequested != null)
                    await ReloginRequested.Invoke();
            }
            catch (OperationCanceledException) { /* debounce 取消，忽略 */ }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"网络变化处理异常: {ex.Message}");
            }
        }

        // ── 心跳检测 ──
        private async void HeartbeatCallback(object? state)
        {
            try
            {
                if (IsFrozen || !_running) return;

                bool online = await PingExternalAsync();
                if (!online)
                {
                    StatusChanged?.Invoke("心跳检测：外网不通，尝试重新登录…");
                    if (ReloginRequested != null)
                        await ReloginRequested.Invoke();
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"心跳检测异常: {ex.Message}");
            }
        }

        /// <summary>Ping 114.114.114.114 判断外网是否可达</summary>
        private static async Task<bool> PingExternalAsync()
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("114.114.114.114", 3000);
                return reply.Status == IPStatus.Success;
            }
            catch { return false; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _heartbeatTimer?.Dispose();
            _debounceCts?.Dispose();
        }
    }
}
