# CUMT校园网助手 (CampusNetAssistant)

🎓 专为中国矿业大学（CUMT）师生打造的轻量级校园网自动登录工具。

## ✨ 核心特性

- **一键登录/断开**：支持保存账号密码，一键快速连接校园网。
- **多运营商支持**：支持选择校园网、中国电信、中国联通、中国移动。
- **开机自启与自动登录**：随系统启动并在后台自动完成网络认证，无感上网。
- **网卡快捷管理**：支持一键禁用/启用选定的网络适配器（需管理员权限）。
- **系统托盘运行**：界面美观，关闭后静默在系统托盘运行，不占用任务栏空间。
- **密码加密存储**：本地保存的密码经过加密处理，保障账号安全。

## 🎯 运行环境

- Windows 10 / Windows 11
- .NET 9.0 桌面运行时 (桌面版)

## 🚀 快速使用

1. 在右侧 **Releases** 中下载最新版的 `CampusNetAssistant.zip`。
2. 解压缩到一个固定目录（例如 `D:\Programs\CampusNetAssistant`）。
3. 双击运行 `CampusNetAssistant.exe`。
4. 在主界面中输入你的 **学号** 和 **密码**，并选择对应的 **运营商**。
5. （可选）勾选 **开机自启** 和 **自动登录**，实现完全免打扰的自动联网。
6. 点击 **保存并登录**。

## 💡 托盘菜单操作

程序运行后，会在任务栏右下角的系统托盘处显示一个香蕉 🍌 图标。
右键点击该图标，可以弹出快捷菜单：

- **🏠 打开主面板**：重新显示配置和状态界面
- **🚀 立即登录** / **⛔ 断开校园网**：快捷执行网络连接和断开操作
- **🔌 禁用/启用以太网**：快速重启网卡解决部分网络卡顿问题
- **❌ 退出**：彻底关闭本程序

## 🛠️ 本地编译构建

你需要安装 [.NET 9.0 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0)。

```powershell
# 克隆仓库
git clone https://github.com/你的用户名/CampusNetAssistant.git
cd CampusNetAssistant

# 编译运行
dotnet run

# 发布为独立绿色版本
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o out
```

## ✅ 本地 Release 预发布测试（推荐）

为避免“发布后下载才发现崩溃”，可在本地运行一键预发布检查脚本：

```powershell
# 使用与 CI 一致的发布参数打包，并做两轮冒烟测试（发布目录 + zip 解压后）
powershell -ExecutionPolicy Bypass -File .\scripts\local-release-test.ps1 -Version 1.0.4
```

脚本会自动完成：

- Release 编译
- 与 GitHub Actions 相同参数的 `dotnet publish`
- 启动 `CampusNetAssistant.exe` 冒烟检查（是否启动即崩溃）
- 打 zip 后再解压，并再次启动冒烟检查（模拟用户下载解压后的场景）

通过后再打 tag 发布，可显著降低线上回滚概率。

## 📝 许可证

MIT License
