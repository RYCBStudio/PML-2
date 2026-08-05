# MEFrpLauncherX (PML 2)

> [!NOTE]
> 我们正在提高本存储库的代码质量。开发者在开发本软件时对 .NET 和 Avalonia 并不是很熟悉。请见谅。

**MEFrpLauncherX**（简称 PML 2）是一个功能强大的跨平台 ME Frp 图形化启动工具，基于 Avalonia UI 开发，支持 Windows、Linux 和 macOS。

## 核心特性

- 现代化 Fluent Design 用户界面
- 跨平台支持（Windows / Linux / macOS）
- 内置验证码识别系统
- 实时流量监控与统计
- 快速启动与管理 Frp 代理
- 智能端口扫描
- 终端控制台集成
- 通知系统
- 主题与背景自定义

## 许可证说明

本项目的**开源代码部分**采用 [MIT License](LICENSE)。

以下组件为**专有闭源/混淆库**，不提供源代码，不受本项目 MIT 许可证约束：

- `RYCB.PML2.MEFrpCaptchaLib`（验证码识别）
- `SecretLib`（安全存储）

详细说明请查看 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

## 技术栈

- **.NET 10.0**
- **Avalonia 11.x** + FluentAvaloniaUI
- **ReactiveUI**
- LiveChartsCore、RestSharp、Sentry、Downloader、Tomlyn、YamlDotNet 等

## 项目结构

```
MEFrpLauncherX.sln
├── MEFrpLauncherX/                  # 主应用程序
│   ├── Views/                       # UI 视图 (AXAML)
│   ├── ViewModels/                  # 视图模型 (MVVM)
│   ├── Controls/                    # 自定义控件
│   ├── Console/                     # 终端控制台
│   ├── NetworkMonitoring/           # 网络监控
│   ├── Plugins/                     # 插件系统
│   └── Assets/                      # 静态资源
├── MEFrpLauncherX.Core/             # 核心类库
│   ├── MEFIntergrated/              # Frp 集成
│   ├── Services/                    # 业务服务
│   ├── Models/                      # 数据模型
│   ├── Messaging/                   # 消息总线
│   ├── Storage/                     # 安全存储
│   └── ...
├── MarkdownAIRender/                # Markdown 渲染
├── MEFrpLauncherX.Fonts/            # 字体资源
├── RYCB.PML2.Mixin.TerminalHelper/  # 终端辅助
└── RYCB.PML2.Extensions.MinecraftExtension/  # Minecraft 扩展
```

## 开发环境要求

- .NET 10.0 SDK
- 推荐 IDE：Visual Studio 2022 (17.14+)、JetBrains Rider 或 VS Code + C# Dev Kit
- 操作系统：Windows 10/11、Linux（Ubuntu/Debian、Fedora 等）、macOS 10.15+

## 构建与运行

### 1. 克隆仓库

```bash
git clone https://github.com/RYCBStudio/PML-2.git
cd PML-2
```

### 2. 处理专有依赖（重要）

以下库为闭源组件，**不会随仓库提供完整源码**：

- `RYCB.PML.MEFrpCaptchaLib.dll`
- `SecretLib.dll`

请将对应二进制文件放置到正确位置后才能完整编译（具体路径请参考项目内 `.csproj` 引用）。缺少这些库时，验证码识别与部分安全存储功能将无法使用。

### 3. 还原依赖并构建

```bash
dotnet restore MEFrpLauncherX.sln
dotnet build MEFrpLauncherX.sln -c Release
```

### 4. 运行

```bash
dotnet run --project MEFrpLauncherX/MEFrpLauncherX.csproj
```

### 5. 发布示例

```bash
# Windows x64
dotnet publish MEFrpLauncherX/MEFrpLauncherX.csproj -c Release -r win-x64 --self-contained

# Linux x64
dotnet publish MEFrpLauncherX/MEFrpLauncherX.csproj -c Release -r linux-x64 --self-contained

# macOS x64
dotnet publish MEFrpLauncherX/MEFrpLauncherX.csproj -c Release -r osx-x64 --self-contained
```

## 配置与日志

- 配置文件位置：`[应用目录]/Config/Settings.json`
- 日志位置：
  - 日常日志：`[应用目录]/Logs/[日期].log`
  - 崩溃日志：`[应用目录]/Logs/Crash/crash_*.log`

## 常见问题

**Q: 编译时提示找不到 RYCB.PML.MEFrpCaptchaLib 或 SecretLib？**  
A: 这些是专有闭源库，需要自行获取并放置到项目引用路径。详见上方「处理专有依赖」。

**Q: Linux 下中文字体显示异常？**  
A: 安装 Noto Sans CJK 字体：

```bash
# Ubuntu/Debian
sudo apt-get install fonts-noto-cjk

# Fedora
sudo dnf install google-noto-sans-cjk-fonts
```

**Q: 应用启动后闪退？**  
A: 查看崩溃日志 `[应用目录]/Logs/Crash/crash_*.log`。

## 贡献

欢迎提交 Issue 和 Pull Request。

1. Fork 本仓库
2. 创建功能分支 (`git checkout -b feature/YourFeature`)
3. 提交更改 (`git commit -m 'Add some feature'`)
4. 推送到分支 (`git push origin feature/YourFeature`)
5. 开启 Pull Request

请尽量保持 MVVM 架构清晰，并为公共 API 添加必要注释。

## 联系与致谢

- 官方网站：https://www.rycb.tech/pml-2/
- 开发者：RYCB Studio

感谢以下开源项目：

- [Avalonia UI](https://avaloniaui.net/)
- [FluentAvalonia](https://github.com/amwx/FluentAvalonia)
- [LiveCharts2](https://github.com/beto-rodriguez/LiveCharts2)
- [ReactiveUI](https://www.reactiveui.net/)
- [Sentry](https://sentry.io/)

---

**Happy Coding!**