# PML 2 隐私政策

**最后更新日期：2026 年 9 月 19 日**

RYCB Studio（以下简称"我们"）尊重并保护用户的隐私。本政策详细说明我们如何收集、使用、存储和保护您的个人信息。请在使用 PML
2（Portal ME Frp Launcher 2，以下简称"本软件"）前仔细阅读。

---

## 重要提示

**注意：** 我们使用 **[Sentry](https://sentry.io/)**
进行错误跟踪和性能监控，以改善软件稳定性。若您想了解 [幻缘映射 ME Frp](https://www.mefrp.com) 的隐私政策，请前往
**[《ME Frp (幻缘映射) 隐私政策》](https://www.mefrp.com/policy?tab=privacy)**。

---

## 1. 信息收集范围

### 1.1 个人数据

- 注册/登录信息（如用户名、邮箱）

### 1.2 设备与使用数据

- 设备标识符（如 IMEI、MAC 地址）、IP 地址、操作系统版本
- 软件使用行为（如功能点击、会话时长、错误日志）
- **遥测数据（通过 Sentry 收集）**：当软件发生错误或崩溃时，我们可能收集以下信息：
    - 设备型号、操作系统版本
    - 崩溃堆栈和错误日志
    - 应用程序版本和运行时环境信息

[//]: # (    - IP 地址（用于地理位置统计）)

  这些数据存储在 **Sentry 位于欧盟 (EU) 的服务器**上。此数据收集旨在提升软件稳定性， **不包含**您的账号密码、隧道内容等敏感信息。

### 1.3 其他数据

- 用户主动提交的内容（如反馈、上传的文件）

[//]: # (- 第三方服务（如社交媒体账号、广告 SDK）提供的共享信息)

---

## 2. 信息用途

我们收集的信息将用于：

1. **提供核心功能服务**（如账号验证、数据同步、隧道管理）
2. **优化用户体验**（如故障修复、功能改进、界面优化）
3. **安全与合规**（如身份验证、防范欺诈、异常检测）
4. **统计分析**或匿名化处理后用于商业决策
5. **通过 Sentry 收集的遥测数据**专门用于 **错误监控与性能分析**，帮助我们定位并修复软件问题

---

## 3. 数据共享与披露

**我们不会出售用户数据**，但在以下情况下可能共享：

- 经用户明确同意
- 为履行法律义务或响应政府机构合法要求
- 与第三方服务商（如云服务、支付平台）合作必需时，且其需遵守同等隐私标准
- **特定第三方（Sentry）**：为实现错误监控，我们会与 Sentry.io (Functional Software, Inc.) 共享必要的设备信息和错误日志。Sentry
  作为数据处理者，其服务器位于欧盟，并遵守相应的数据保护法规。您可以在 [Sentry 隐私政策](https://sentry.io/privacy/) 中了解更多。

---

## 4. 数据存储与安全

### 4.1 存储位置与期限

- 我们自身的业务数据主要存储在境内服务器，保留期限为法律要求的最短期限
- 通过 Sentry 收集的遥测数据存储于 **欧盟 (EU) 地区的服务器**，其保留期遵循 Sentry 的标准数据保留政策

### 4.2 安全措施

- 采取加密、访问控制等措施保护数据
- 建立数据安全管理制度和操作规程
- 但请注意，互联网环境并非 100% 安全，我们无法保证绝对安全

---

## 5. 用户权利

您有权：

1. **访问、更正或删除个人信息**（部分数据因合规要求可能无法删除）
2. **撤回同意或限制数据处理**（可能影响部分功能）。您可以通过软件设置禁用遥测数据收集
3. **通过电子邮件提交请求**，我们将在 7 天内响应

**联系方式：** [im@rycb.tech](mailto:im@rycb.tech) | [rycbstudio@163.com](mailto:rycbstudio@163.com)

---

## 6. 第三方服务

本软件可能嵌入第三方服务，其隐私政策独立于我们，建议用户另行阅读。我们使用的第三方服务主要为开源库，以及用于错误监控的
**Sentry**（商业服务）。

### 6.1 使用的开源库

| 名称                                   | 协议           | 网站                                                                                                                           | 描述                                   |
|----------------------------------------|----------------|--------------------------------------------------------------------------------------------------------------------------------|----------------------------------------|
| .NET                                   | MIT            | [官网](https://dotnet.microsoft.com) [.NET 基金会](https://dotnetfoundation.org) [GitHub](https://github.com/microsoft/dotnet) | 软件的运行库                           |
| Avalonia UI                            | MIT            | [官网](https://avaloniaui.net) [GitHub](https://github.com/avaloniaui/avalonia)                                                | 软件的UI 框架                          |
| Downloader                             | MIT            | [GitHub](https://github.com/bezzad/Downloader)                                                                                 | 用于软件内多线程下载                   |
| FluentAvalonia                         | MIT            | [GitHub](https://github.com/amwx/FluentAvalonia)                                                                               | 软件的主题库                           |
| RestSharp                              | Apache 2.0     | [官网](https://restsharp.dev/) [GitHub](https://github.com/RestSharp/RestSharp)                                                | 用于进行网络通信、获取API信息的库      |
| Markdown.AIRender (Fork 至 FluentAvalonia.MarkdownRender) | MIT + 二次分发 | [GitHub 原仓库](https://github.com/AIDotNet/Markdown.AIRender)                                                                 | 用于显示 Markdown 内容（如公告等）的库 |
| Message.Avalonia                       | MIT            | [GitHub](https://github.com/xiyaowong/Message.Avalonia)                                                                        | 软件内用于显示消息的库                 |
| AvaloniaEdit                           | MIT            | [GitHub](https://github.com/AvaloniaUI/AvaloniaEdit)                                                                           | 用于显示配置文件语法高亮的库           |
| LiveCharts2                            | MIT            | [官网](https://livecharts.dev/) [GitHub](https://github.com/Live-Charts/LiveCharts2)                                           | 用于绘制折线图的库（例如流量统计）     |
| NPinyin                                | MIT            | [GitHub](https://github.com/WuTong1995/NPinyin)                                                                                | 用于汉字转拼音的库                     |
| YamlDotNet                             | MIT            | [GitHub](https://github.com/aaubry/YamlDotNet)                                                                                 | 用于读取 YAML 文件的库                 |
| Tomlyn                                 | BSD-2-Clause   | [GitHub](https://github.com/xoofx/Tomlyn)                                                                                      | 用于读取 TOML 文件的库                 |

### 6.2 自行开发库

| 名称                                    | 状态   | 描述                                                                                                                                                                                                                     |
|-----------------------------------------|--------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| MEFrpLauncherX.Core                     | 半开源 | 软件的核心库，含有网络请求等核心代码                                                                                                                                                                                     |
| MEFrpLauncherX.Fonts                    | 半开源 | 软件的字体库，含有软件的所有字体（[HarmonyOS Sans](https://rycb.mxj.pub/static/HarmonyOS_Sans_License.txt)、[JetBrains Mono](https://www.jetbrains.com/zh-cn/lp/mono/#license) 等）。**注意：** 以上列举字体均有其许可证 |
| RYCB.PML2.Extensions.MinecraftExtension | 半开源 | 软件的扩展，提供了对 Minecraft 的相关支持                                                                                                                                                          |
| RYCB.PML2.Mixin.TerminalHelper          | 半开源 | 软件的扩展，提供了对控制台的相关支持                                                                                                                                                               |
| RYCB.PML2.MEFrpCaptchaLib               | 闭源   | 软件人机验证库，用于进行无感人机验证。**注：** 本库的代码已经过混淆，禁止对本库的一切逆向操作                                                                                                                            |
| SecretLib                               | 闭源   | 软件插件核心库，用于解压、打包插件等核心操作。**注：** 本库的代码已经过混淆，禁止对本库的一切逆向操作。相关知识产权归我方所有                                                                                            |

### 6.3 错误监控服务

| 名称   | 类型     | 隐私政策                               | 描述                                                             |
|--------|----------|----------------------------------------|------------------------------------------------------------------|
| Sentry | 商业服务 | [隐私政策](https://sentry.io/privacy/) | 用于收集崩溃报告、错误日志和性能指标，帮助我们快速定位和修复问题 |

### 6.4 证书助手（26.4 新增）

「证书助手」是 PML 2 提供的**可选本地工具**，用于为您**自有域名**申请 SSL 证书，供创建 HTTPS 隧道使用。该功能与幻缘映射 ME Frp 的证书服务**无关**。

**在您本机处理、不会上传给我们：**

- 您填写的域名与附加域名（SAN）
- ACME 账户邮箱
- 申请到的证书（`fullchain.pem`）与私钥（`privkey.pem`）
- DNS 服务商 API Token / 密钥（如启用自动验证，加密存储于本机）

**涉及的第三方（仅限证书申请流程必需）：**

| 名称 | 类型 | 隐私政策 | 说明 |
|---|---|---|---|
| Let's Encrypt | 公益 CA | [隐私政策](https://letsencrypt.org/privacy/) | 接收域名与 ACME 账户邮箱以完成域名验证与签发；默认使用其 Staging 测试环境 |
| 您所选的 DNS 服务商 | 取决于用户选择 | 以该服务商政策为准 | 仅在您启用自动 DNS 验证时，由本机向其 API 提交或删除 TXT 记录 |
| lego（ACME 客户端） | 开源工具 | [仓库](https://github.com/go-acme/lego) | 按需下载至本机并运行，其产物与账户密钥均保存在本机目录 |

#### 6.4.1 DNS 账户保险箱（26.4 阶段 B）

为支持「一键自动申请」，证书助手可将 DNS 服务商凭据保存为**本机加密的 DNS 账户**：

- **存储位置与加密**：账户文件位于 `%AppData%/PML2/certs/dns_accounts.dat`，使用 AES-256 加密；
  加密密钥位于 `%LocalAppData%/PML2/keys/`，在 Windows 上由 DPAPI 绑定当前用户，
  复制文件到其他机器或用户下**无法解密**。
- **不外传**：Token / 密钥不会上传给 ME Frp、RYCB Studio 或任何第三方服务，
  也不会写入日志、崩溃报告或分析上报。
- **进程隔离**：申请证书时，凭据仅作为环境变量注入本机的 lego 子进程，
  子进程退出即失效；界面与日志中出现的凭据一律以掩码显示。
- **最小权限**：我们建议按各服务商的最小权限说明创建**专用 Token**（例如 Cloudflare 仅需
  `Zone → DNS → Edit`），请勿在软件中填写账号密码或全局 API Key。
- **随时删除**：您可在「设置 → DNS 账户」中随时删除任一账户；删除后已签发的证书仍可继续使用。

**我们不会**通过证书助手收集、上传或留存您的域名、邮箱、Token 或证书私钥；上述信息仅用于本机完成签发流程。相关日志经过脱敏处理，普通日志不包含 Token 与私钥路径。


---

## 7. Cookie 和类似技术

本软件不使用 Cookie 或类似的跟踪技术。

---

## 8. 未成年人保护

我们非常重视对未成年人个人信息的保护。若您是 18 岁以下的未成年人，在使用本软件前，应事先取得家长或法定监护人的书面同意。

---

## 9. 政策更新

我们可能修订本政策，更新版本将通过 [官网](https://www.rycb.tech/pml-2) 发布，继续使用即视为接受修改。

**重大变更时，我们会通过以下方式通知您：**

- 在软件内发布通知
- 通过电子邮件发送变更说明
- 在官方网站发布公告

---

## 10. 联系我们

如对隐私政策有疑问，或需要行使您的权利，请通过以下方式联系我们：

- **电子邮件：** [im@rycb.tech](mailto:im@rycb.tech) | [rycbstudio@163.com](mailto:rycbstudio@163.com)
- **官方网站：** [https://www.rycb.tech/](https://www.rycb.tech/)

**我们将在收到请求后 7 个工作日内予以回复。**

---

## 11. 管辖法律与争议解决

本隐私政策的解释与适用，以及因此产生的争议，均适用中华人民共和国大陆地区法律。若您和我们发生任何纠纷或争议，首先应友好协商解决；协商不成的，任何一方均有权将争议提交至我们所在地有管辖权的人民法院诉讼解决。

---


<div align="center">
Copyright © 2023-2026 RYCB Studio. All Rights Reserved.
</div>
