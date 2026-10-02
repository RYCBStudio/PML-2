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
- **Avalonia 12.1.3** + FluentAvaloniaUI
- **ReactiveUI**
- LiveChartsCore、RestSharp、Sentry、Downloader、Tomlyn、YamlDotNet 等

## 项目结构

```
MEFrpLauncherX.sln
├── MEFrpLauncherX/                  # 主应用程序
│   ├── Views/                       # UI 视图 (AXAML)
│   ├── ViewModels/                  # 视图模型 (MVVM)
│   ├── Controls/                    # 自定义控件
│   ├── Behaviours/                  # 附加属性行为（动画 / 过渡等）
│   ├── Styles/                      # 全局样式与控件模板 (AXAML)
│   ├── Styling/                     # 代码构建的样式与过渡（可运行时切换）
│   ├── Services/                    # 应用级服务
│   ├── Console/                     # 终端控制台
│   ├── NetworkMonitoring/           # 网络监控
│   ├── Plugins/                     # 插件系统
│   ├── Tools/                       # 内置工具（证书助手等）
│   └── Assets/                      # 静态资源
├── MEFrpLauncherX.Core/             # 核心类库
│   ├── MEFIntergrated/              # Frp 集成
│   ├── Services/                    # 业务服务
│   ├── Models/                      # 数据模型
│   ├── Messaging/                   # 消息总线
│   ├── Storage/                     # 安全存储
│   └── ...
├── FluentAvalonia.MarkdownRender/   # Markdown 渲染（Fork 自 Markdown.AIRender）
├── MEFrpLauncherX.Fonts/            # 字体资源
├── RYCB.PML2.Mixin.TerminalHelper/  # 终端辅助
└── RYCB.PML2.Extensions.MinecraftExtension/  # Minecraft 扩展
```

## Markdown 渲染组件（FluentAvalonia.MarkdownRender）

`FluentAvalonia.MarkdownRender` 是本仓库自行维护的 Avalonia Markdown 渲染控件，**Fork 自开源项目 [AIDotNet/Markdown.AIRender](https://github.com/AIDotNet/Markdown.AIRender)**（原包名 `Markdown.AIRender` / 原工程名 `MarkdownAIRender`）。

在此基础上，本仓库做了如下修改：

- 工程、包名与命名空间重命名为 `FluentAvalonia.MarkdownRender`
- 组件 XML 命名空间由 `https://github.com/AIDotNet/Markdown.AIRender` 变更为 `https://github.com/RYCBStudio/FluentAvalonia.MarkdownRender`
- **新增 GFM 表格（Table）渲染支持**：表头行、列对齐（`:--` / `:-:` / `--:`）、单元格内联样式、列合并（`ColumnSpan`），表格过宽时自动横向滚动
- 修复链接下划线动画等若干渲染问题

原项目版权与许可（MIT）归原作者所有，具体见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) 与 [PRIVACY_POLICY.md](PRIVACY_POLICY.md)。

> 说明：仓库中的目录名、`x:Name` 等历史标识符命名可能仍保留 `MarkdownRender` 字样，属于内部约定，不影响使用。

在 XAML 中使用：

```xml
<Window xmlns:mdRender="https://github.com/RYCBStudio/FluentAvalonia.MarkdownRender">
    <mdRender:MarkdownRender Value="{Binding MarkdownText}" />
</Window>
```

## 界面流畅度与诊断

渲染与动画相关的设置集中在 **设置 → 外观 / 高级**：

| 设置项 | 配置文件 | 生效时机 |
|---|---|---|
| **动画程度**（关闭 / 精简 / 标准） | `Config/Settings.json`（`AnimationLevel`） | **即时生效**，同时影响页面入场动画与页签切换过渡 |
| **渲染模式**（自动 / Vulkan / OpenGL / 软件） | `Config/Render.json`（`RenderingMode`） | 需重启 |
| **显存分配**（128 / 256 / 512 / 1024 MB） | `Config/Render.json`（`GpuMemoryLimitMb`） | 需重启 |
| **低延迟渲染** | `Config/Render.json`（`LowLatencyRendering`） | 需重启 |

> 渲染相关设置存放在**独立的 `Config/Render.json`**：它们必须在 Avalonia 启动前读取，而 `Settings.json` 的加载时机更晚。
>
> Windows 默认渲染模式列表把 **AngleEgl 排在 Vulkan 之前** —— 只有 AngleEgl 的成功分支会注册合成模式（`LowLatencyDxgiSwapChain` / `WinUIComposition` / `DirectComposition`），若 Vulkan 优先成功则合成永不注册，将退回 60Hz 的睡眠式渲染循环。详见 `MEFrpLauncherX/Program.cs` 的 `BuildWin32Options`。

需要测量帧率时，用环境变量开启渲染诊断叠加层（**仅 Debug 构建**，默认关闭）：

```bash
# Windows (PowerShell)
$env:PML2_RENDER_DIAG = "1"; dotnet run --project MEFrpLauncherX/MEFrpLauncherX.csproj

# 只显示其中几项：fps / layout / render / dirty（dirty 会引入额外合成层，仅排查脏矩形时用）
$env:PML2_RENDER_DIAG = "fps,render"
```

窗口左上角会显示 **FPS 曲线 + 布局耗时图 + 渲染耗时图**：FPS 持续低于刷新率说明帧率不足；布局耗时出现尖峰说明瓶颈在布局（不要对 `Width`/`Height`/`Margin` 做动画）；渲染耗时尖峰说明瓶颈在绘制（阴影、模糊、大图缩放）。

> 帧率上限由显示器刷新率决定（垂直同步），无法超过屏幕刷新率。

进一步的优化记录见 [docs/performance_optimization_summary.md](docs/performance_optimization_summary.md)。

## 自检工具

`tools/` 下的免安装探针脚本（各自独立工程，不参与主解决方案构建）：

| 工具 | 用法 | 作用 |
|---|---|---|
| `XamlSmoke` | `dotnet run --project tools/XamlSmoke` | 运行时加载全部 XAML 资源与页面，捕获「编译通过、运行期 XAML 加载失败」 |
| `TabTransitionVerify` | `dotnet run --project tools/TabTransitionVerify` | 在 headless 环境真实播放标签页过渡，断言不抛异常 |
| `P1Verify` | `dotnet run --project tools/P1Verify` | 一次性编译全部项目（含不在主 sln 内的 CrashDisplayer / Splash） |
| `P4Verify` | `dotnet run --project tools/P4Verify` | 逐条校验 `TrimmerRoots.xml` 的裁剪条目是否真实命中 |
| `P3UiRegression` | `dotnet run --project tools/P3UiRegression` | 用 Avalonia Headless 真实创建窗口并驱动 UI 的回归测试 |

> 这些工具都以**反射方式加载主程序已编译的产物**（主程序是自包含可执行文件，无法被直接引用），因此请先构建主项目。若主程序正在运行导致 `bin` 被占用，`TabTransitionVerify` 会自动改用较新的 `obj` 产物，也可用环境变量 `PML2_VERIFY_APPDIR` 显式指定产物目录。

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

**Q: 编译时提示找不到 RYCB.PML2.MEFrpCaptchaLib 或 SecretLib？**  
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
- [Markdown.AIRender](https://github.com/AIDotNet/Markdown.AIRender)（本项目 `FluentAvalonia.MarkdownRender` 的上游原仓库）
- [LiveCharts2](https://github.com/beto-rodriguez/LiveCharts2)
- [ReactiveUI](https://www.reactiveui.net/)
- [Sentry](https://sentry.io/)

---

**Happy Coding!**