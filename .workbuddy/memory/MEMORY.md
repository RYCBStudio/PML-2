# 项目长期约定（MEFrpLauncherX / PML-2）

## FluentAvalonia.MarkdownRender 已独立

- **2026-11-07 起改为 NuGet 包引用**：`FluentAvalonia.MarkdownRender` 12.1.3.8（版本在 PML-2 的
  `Directory.Packages.props`），git submodule、`.gitmodules` 与工作目录均已删除，克隆**不再需要**
  `--recursive`。2026-10-02 ~ 11-07 期间曾是 submodule。
- 同时以 NuGet 包发布：`FluentAvalonia.MarkdownRender`，仓库
  https://github.com/RYCBStudio/FluentAvalonia.MarkdownRenderer（仓库名带 **er**，
  PackageId 与 XAML 命名空间 URI 仍是 `FluentAvalonia.MarkdownRender`，**不可改**）。
- 版本号四段式：**前三位对齐所适配的 Avalonia 版本，末位为组件修订号**（如 12.1.3.1 = 适配 Avalonia 12.1.3）。
- 子仓自带 `Directory.Packages.props`（CPM），依赖版本与 PML-2 解耦；改依赖去子仓改。

## 组件仓为双项目（2026-10-10 起）

- `FluentAvalonia.MarkdownRender/`（原包，依赖 FluentAvaloniaUI，Alert 用其 InfoBar）
- `MarkdownRenderX.Avalonia/`（新包，**无 FluentAvalonia 依赖**，命名空间
  `MarkdownRenderX.Avalonia.*`，XAML URI `https://github.com/RYCBStudio/MarkdownRenderX.Avalonia`，
  Alert 为自绘 `MdAlertBox`，强调色用自带键 `MdAccentTextFillColor*`）
- 两版是**独立副本**，不是共享源码：改 FAMR 的代码后必须手工镜像到 MarkdownRenderX，否则行为漂移。
- 一次性 tag 发两个包（CI 用 tag 覆盖两项目版本）；回归用 `dotnet run --project tools/MrxSmoke -c Release`（无头，断言 Alert 配色与资源键）。
- PML-2 的 sln 已移除该项目条目（改 NuGet 后），`MEFrpLauncherX/TrimmerRoots.xml` 里保留
  `FluentAvalonia.MarkdownRender` 与 `MarkdownRenderX.Avalonia` 两个程序集（XAML 走 `avares://`
  运行时加载，不产生静态类型引用，错误 fullname 会被 linker 静默忽略）。

## 扩展点与 KaTeX 公式包（2026-10-31 起）

- 核心包提供 `Extensibility/IMarkdownNodeRenderer` + `MarkdownRendererRegistry`（静态注册表），
  在 `ConvertBlock` / `ConvertInline` 之前查询；第三方渲染器异常被吞掉并回退内置逻辑。
- `MarkdownRenderX.Avalonia.KaTeX` = 核心包 + `CSharpMath.Avalonia 1.0.0-pre.2`（唯一面向 Avalonia 12 的
  原生排版库，预发布版，依赖 SkiaSharp 3.119.4），内嵌 Latin Modern Math（GUST 许可），**不走 WebView**。
  用法：`KaTeXExtension.Register()` + 引入两个 `Index.axaml`。
- `MarkdownRenderX.Avalonia.Emoji`（12.1.3.5 起）= 核心包 + 41KB 内置短码表（gemoji 1913 条，
  GitMoji 全被覆盖），`:beer:` → 🍺。`EmojiExtension.Register()` 即可，**无需 StyleInclude**。
- 接口有**带默认实现**的 `CreateInline`（返回 `Run`，文本可选中），`TryRenderInline` 优先于 `TryRender`；
  因此新增扩展时优先实现 `CreateInline`，旧渲染器不需改动。只作用于 `LiteralInline`，代码块天然免疫。
- **FAMR 自 12.1.3.6 起是 MRX 的副包**：`FluentAvalonia.MarkdownRender.csproj` 引用 MRX，
  两处渲染分派查询 MRX 的 `MarkdownRendererRegistry`（`TryRender` / `TryRenderInline` 已 public）。
  KaTeX / Emoji 注册一次即对两版同时生效。FAMR 的命名空间、XAML URI、样式与 FAInfoBar Alert 保持不变，
  **代码仍是完整副本**（未瘦成薄壳），公共逻辑改动仍需手工镜像。
- `MathView.TextColor` 是构造时写入的本地值，样式 Setter 压不住 → 必须
  `Bind(TextColorProperty, new DynamicResourceExtension(...))`；跨 Styles 树的资源不可见，
  故 KaTeX 包自带 `MdMathTextColor` 主题键，不依赖核心包的 `Grey9`。

## Mermaid / 扩展语法 / 安装提示（2026-11-07 起，12.1.3.7~8）

- `MarkdownRenderX.Avalonia.Mermaid` = 核心包 + `Mermaider 0.15.1`（MIT，纯 .NET 布局，
  传递依赖 Sugiyama）。`MermaidExtension.Register()` 即可，无 StyleInclude；拦截 info=mermaid
  的 `FencedCodeBlock`，输出 `SvgImage`。**不走 WebView**。注册一次对 MRX / FAMR 两版生效。
- **Mermaider 的 SVG 用 CSS 变量 `var(--fg)` / 嵌套 `color-mix()` / rem，Svg.Skia 全不认**，
  未展开时甘特图背景会变黑。故扩展包自带 `SvgCssFlattener`（递归展开为十六进制 +
  删 `<style>` 块）。纯字符串求值即可，别引 CSS 引擎。
- 核心 `CreateEmphasisInline` 原只按 `DelimiterCount` 判粗/斜体 → `H~2~O` 成斜体、
  `==key==` 成粗体。12.1.3.8 起按 `EmphasisInline.DelimiterChar` 分派：
  `~` 下标 / `^` 上标 / `=` 高亮 / `~~` 删除线 / `++` 下划线，**两版副本均已镜像**。
  `BaselineAlignment.Subscript/Superscript` 在 `Avalonia.Media`。这是**行为变更**。
- 五个包根目录都带 `readme.txt`（CsWin32 机制：VS 安装时自动弹出），写明必须调用的
  `Register()`；`README.md` 仍是 `PackageReadmeFile`。两者不冲突，仅直接安装的包弹窗。
- 三个扩展包专属图标源在 `F:/rycbq/Pictures/MRX/`，SVG 归档于组件仓 `assets/`。

## GitHub Actions 注意

- job 级 `if` **不能引用 `secrets` 上下文**（整个 workflow 会解析失败、0s 失败）。
  需要按 secret 有无来跳过步骤时，用 job 级 `env` 取值 + step 级 `if: env.XXX != ''`。

## 令牌权限

- 当前 GitHub PAT 无「创建仓库」权限（个人/组织均 403），新建仓库需先在网页创建再推送；
  对已有组织仓库（如 PML-2）具备 admin/push 权限。
