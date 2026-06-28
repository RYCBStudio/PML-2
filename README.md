# MEFrpLauncherX 开发指南

## 📖 项目简介

**MEFrpLauncherX**（简称: PML 2）是一个功能强大、跨平台的 ME Frp 图形化启动工具，基于 Avalonia UI 框架开发，支持 Windows、Linux 和 macOS 平台。

### 核心特性
- 🎨 现代化的 Fluent Design 用户界面
- 🌍 跨平台支持（Windows/Linux/macOS）
- 🔐 内置验证码识别系统
- 📊 实时流量监控和统计
- 🚀 快速启动和管理 Frp 代理
- 🌐 多语言支持（中文、英文、日文等）
- 🎯 智能端口扫描
- 📱 终端控制台集成
- 🔔 通知系统
- 🎨 主题和背景自定义

## 🏗️ 项目架构

### 解决方案结构

```
MEFrpLauncherX.sln
├── MEFrpLauncherX/                  # 主应用程序项目
│   ├── Views/                       # UI 视图层 (Avalonia AXAML)
│   ├── ViewModels/                  # 视图模型层 (MVVM 模式)
│   ├── Controls/                    # 自定义控件
│   ├── Console/                     # 终端控制台组件
│   ├── NetworkMonitoring/           # 网络监控模块
│   ├── Plugins/                     # 插件系统
│   ├── Styles/                      # 样式资源
│   └── Assets/                      # 静态资源文件
│
├── MEFrpLauncherX.Core/             # 核心类库
│   ├── MEFIntergrated/              # Frp 集成服务
│   ├── Services/                    # 业务服务层
│   ├── Models/                      # 数据模型
│   ├── Messaging/                   # 消息总线
│   ├── Storage/                     # 安全存储
│   ├── UrlProtocol/                 # URL 协议处理
│   ├── WindowServices/              # 窗口服务
│   └── Controls/                    # 核心控件
│
├── MarkdownAIRender/                # Markdown 渲染组件
├── MEFrpLauncherX.Fonts/            # 字体资源包
├── RYCB.PML2.Mixin.TerminalHelper/  # 终端辅助工具
├── RYCB.PML2.Extensions.MinecraftExtension/  # Minecraft 扩展
└── System.Device.Location/          # 地理位置服务
```

### 技术栈

#### 主要框架
- **.NET 10.0** (主应用) / **.NET 9.0/10.0** (Core 库)
- **Avalonia 11.3.11** - 跨平台 UI 框架
- **ReactiveUI** - MVVM 框架
- **FluentAvaloniaUI** - Fluent Design 实现

#### 核心依赖
- **LiveChartsCore** - 图表和数据可视化
- **MessageBox.Avalonia** - 现代化消息框
- **Message.Avalonia** - 消息通知系统
- **Notification.Avalonia** - 桌面通知
- **RestSharp** - HTTP 客户端
- **Sentry** - 错误监控和日志
- **Downloader** - 多线程下载器
- **Tomlyn/YamlDotNet** - TOML/YAML 解析

#### 专用库
- **RYCB.PML.MEFrpCaptchaLib** - 验证码识别
- **SecretLib** - 加密和安全存储
- **NPinyin.Core** - 拼音转换

## 🛠️ 开发环境搭建

### 前置要求

1. **.NET SDK**
   - .NET 10.0 SDK（主应用）

2. **IDE 推荐**
   - Visual Studio 2022 (v17.14+)
   - JetBrains Rider 2024.1+
   - VS Code + C# Dev Kit

3. **操作系统**
   - Windows 10/11 (推荐 Windows 11 25H2)
   - Linux (Ubuntu/Debian, Fedora, Alpine)
   - macOS 10.15+

### 克隆项目

```bash
git clone <repository-url>
cd MEFrpLauncherX
```

### 还原依赖

```bash
# 还原 NuGet 包
dotnet restore MEFrpLauncherX.sln
```

### 构建项目

```bash
# Debug 模式构建
dotnet build MEFrpLauncherX.sln -c Debug

# Release 模式构建
dotnet build MEFrpLauncherX.sln -c Release
```

### 运行应用

```bash
# 运行主应用
dotnet run --project MEFrpLauncherX/MEFrpLauncherX.csproj
```

### 发布应用

```bash
# Windows x64
dotnet publish MEFrpLauncherX/MEFrpLauncherX.csproj -c Release -r win-x64 --self-contained

# Linux x64
dotnet publish MEFrpLauncherX/MEFrpLauncherX.csproj -c Release -r linux-x64 --self-contained

# macOS x64
dotnet publish MEFrpLauncherX/MEFrpLauncherX.csproj -c Release -r osx-x64 --self-contained
```

## 📋 核心模块说明

### 1. MEFrpLauncherX (主应用)

#### Views 层
所有用户界面都在此目录下，使用 AXAML 定义：

- `MainWindow.axaml` - 主窗口
- `HomePage.axaml` - 首页
- `LoginPage.axaml` - 登录页面
- `CreateProxyPage.axaml` - 创建代理
- `ManageProxyPage.axaml` - 代理管理
- `SettingsPage.axaml` - 设置页面
- `ConfigEditor.axaml` - 配置文件编辑器
- `TerminalPage.axaml` - 终端页面

#### ViewModels 层
实现 MVVM 模式的视图模型：

- `MainWindowViewModel` - 主窗口 ViewModel
- `HomePageViewModel` - 首页逻辑
- `LoginViewModel` - 登录逻辑
- `UserProxyViewModel` - 代理管理逻辑
- `NodesContainerViewModel` - 节点容器逻辑
- `UpdatePageViewModel` - 更新页面逻辑

#### 特色功能模块

**Console (终端控制台)**
- `TerminalControl.axaml` - 终端模拟器
- `AnsiColoringTransformer` - ANSI 颜色解析

**NetworkMonitoring (网络监控)**
- `CrossPlatformNetworkMonitor` - 跨平台网络状态监控
- 实时流量统计和图表展示

**Controls (自定义控件)**
- `AnimatedProgressRing` - 动画进度环
- `RollingNumberTextBlock` - 滚动数字显示
- `TrafficStatusControl` - 流量状态控件
- `TunnelNodeControl` - 隧道节点控件
- `StarMapCanvas` - 星图绘制画布

### 2. MEFrpLauncherX.Core (核心库)

#### MEFIntergrated (Frp 集成)
- `DownloadHelper` - Frp 下载管理器
- `FrpConfigService` - Frp 配置生成和服务
- `MEFApiConverter` - API 响应转换
- `NetworkSpeedTester` - 网络速度测试

#### Services (业务服务)
- `CaptchaService` - 验证码识别服务
- `LogService` - 日志记录服务

#### Models (数据模型)
- `FrpModels` - Frp 相关的数据模型定义

#### Messaging (消息系统)
- `MessageBus` - 应用内消息总线

#### Storage (存储)
- `SecureStorage` - 敏感数据加密存储

#### UrlProtocol (URL 协议)
- `UrlProtocolHelper` - `mefrp://` 协议处理
- 支持深度链接启动代理

### 3. 配置系统

#### 配置文件位置
```
[应用目录]/Config/Settings.json
```

#### 配置结构
```json
{
  "Skin": "None",                 // 界面皮肤
  "Theme": "System",              // 主题 (Dark/Light/System)
  "HideInsteadOfClose": true,     // 点击关闭时最小化到托盘
  "ParallelDownload": true,       // 并行下载
  "ParallelCount": 16,            // 并行下载线程数
  "AutoStartup": false,           // 开机自启
  "AutoLaunch": false,            // 自动启动代理
  "CaptchaMode": "implicit",      // 验证码模式 (implicit/explicit)
  "DownloadSource": "TPCA",       // 下载源
  "UpdateSettings": {             // 更新设置
    "AutoCheck": true,
    "Channel": "Preview",
    "Method": "ds"
  },
  "BackgroundSettings": {         // 背景设置
    "LayerOpacity": 0.6,
    "BackgroundImage": "",
    "Stretch": "Uniform"
  }
}
```

### 4. 日志系统

#### 日志位置
```
[应用目录]/Logs/[日期].log        # 日常志
[应用目录]/Logs/Crash/crash_*.log # 崩溃日志
```

#### 日志级别
- `Info` - 信息
- `Warning` - 警告
- `Error` - 错误
- `Fatal` - 致命错误

#### 使用示例
```csharp
App.CurrentLogger?.Log("操作成功", module: EnumLogModule.Custom, customModuleName: "我的模块");
App.CurrentLogger?.Error(ex, type: EnumLogType.Error);
```

## 🎯 开发指南

### 添加新页面

1. **创建 View**
   ```bash
   # 在 Views/ 目录下创建新的 AXAML 文件
   MyNewPage.axaml
   MyNewPage.axaml.cs
   ```

2. **创建 ViewModel**
   ```csharp
   // ViewModels/MyNewPageViewModel.cs
   public class MyNewPageViewModel : ViewModelBase
   {
       // 实现业务逻辑
   }
   ```

3. **注册路由**
   ```csharp
   // 在 MainPageFrameViewModel 中添加导航逻辑
   ```

### 添加新控件

```xml
<!-- Controls/MyCustomControl.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- 控件定义 -->
</UserControl>
```

```csharp
// Controls/MyCustomControl.axaml.cs
public partial class MyCustomControl : UserControl
{
    public MyCustomControl()
    {
        InitializeComponent();
    }
}
```

### 使用消息总线

```csharp
// 发送消息
MessageBus.Instance.SendMessage(new MyMessage { Data = "Hello" });

// 接收消息
MessageBus.Instance.Register<MyMessage>(msg => {
    Console.WriteLine(msg.Data);
});
```

### 调用 Frp API

```csharp
// 使用 MEFApiConverter 进行 API 调用
var result = await MEFApiConverter.ConvertResponse<T>(response);
```

### 配置文件编辑

使用 `ConfigManager` 管理类配置：

```csharp
// 读取配置
var theme = ConfigManager.CurrentConfig.Theme;

// 修改配置
ConfigManager.UpdateConfig(config => {
    config.Theme = "Dark";
});

// 异步修改
await ConfigManager.UpdateConfigAsync(config => {
    config.ParallelCount = 32;
});
```

## 🧪 调试技巧

### 启用调试模式

在 `appsettings.Development.json` 或代码中设置：

```csharp
#if DEBUG
    options.Debug = true;
#endif
```

### 查看实时日志

日志文件位置：
```
[应用目录]/Logs/[当前日期].log
```

### Sentry 错误追踪

项目已集成 Sentry，崩溃报告会自动上传：
- DSN: `https://840a0a2c7a17031d7639b82c602312fc@o4511009461305344.ingest.de.sentry.io/4511009467924560`
- 生产环境自动启用
- Debug 模式下可查看详细日志

### Avalonia DevTools

Debug 模式下按 `F12` 打开 Avalonia 开发工具，可以：
- 检查 UI 元素树
- 查看和修改样式
- 调试绑定和数据上下文

## 📦 打包发布

### Windows

```bash
dotnet publish MEFrpLauncherX/MEFrpLauncherX.csproj \
  -c Release \
  -r win-x64 \
  --self-contained \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true
```

### Linux

```bash
# DEB 包
dotnet publish MEFrpLauncherX/MEFrpLauncherX.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained

# 使用 Packaging.Targets 创建 DEB/RPM
```

### macOS

```bash
dotnet publish MEFrpLauncherX/MEFrpLauncherX.csproj \
  -c Release \
  -r osx-x64 \
  --self-contained
```

## 🐛 常见问题

### Q: 编译时提示找不到 RYCB.PML.MEFrpCaptchaLib.dll

**A:** 这是一个外部依赖库，需要手动添加到项目根目录：
```
RYCB.PML.MEFrpCaptchaLib.dll
SecretLib.dll
```

### Q: Linux 下字体显示异常

**A:** 安装 Noto Sans CJK 字体：
```bash
# Ubuntu/Debian
sudo apt-get install fonts-noto-cjk

# Fedora
sudo dnf install google-noto-sans-cjk-fonts
```

### Q: 应用启动后闪退

**A:** 查看日志文件定位问题：
```
[应用目录]/Logs/crash_*.log
```

### Q: Frp 下载失败

**A:** 检查网络和下载源配置，可尝试切换下载源：
```json
{
  "DownloadSource": "TPCA"  // 或 "GitHub", "Gitee"
}
```

## 🤝 贡献指南

### 提交代码

1. Fork 项目
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

### 代码规范

- 遵循 C# 命名约定
- 使用 XML 注释文档化公共 API
- 保持 MVVM 架构清晰
- 添加必要的单元测试

### 报告 Bug

使用 Issue Tracker 报告 Bug，请提供：
- 重现步骤
- 预期行为
- 实际行为
- 环境信息（OS、.NET 版本等）
- 日志文件

## 📄 许可证

本项目采用 [查看 LICENSE.txt](LICENSE.txt)

## 📞 联系方式

- **官方网站**: https://rycb.mxj.pub/mefl
- **开发者**: RYCB Studio

## 🙏 致谢

感谢以下开源项目：

- [Avalonia UI](https://avaloniaui.net/)
- [FluentAvalonia](https://github.com/amwx/FluentAvalonia)
- [LiveCharts2](https://github.com/beto-rodriguez/LiveCharts2)
- [ReactiveUI](https://www.reactiveui.net/)
- [Sentry](https://sentry.io/)

---

**Happy Coding!** 🎉
