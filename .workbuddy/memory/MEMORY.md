# 项目长期约定（MEFrpLauncherX / PML-2）

## FluentAvalonia.MarkdownRender 已独立

- 自 2026-10-02 起，该组件是 PML-2 的 **git submodule**，路径不变（`FluentAvalonia.MarkdownRender/`）。
  克隆 PML-2 必须加 `--recursive`，否则该目录为空、sln 构建失败。
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
- PML-2 的 submodule 路径不变，但 sln 里的项目路径多一层：
  `FluentAvalonia.MarkdownRender\FluentAvalonia.MarkdownRender\FluentAvalonia.MarkdownRender.csproj`。

## GitHub Actions 注意

- job 级 `if` **不能引用 `secrets` 上下文**（整个 workflow 会解析失败、0s 失败）。
  需要按 secret 有无来跳过步骤时，用 job 级 `env` 取值 + step 级 `if: env.XXX != ''`。

## 令牌权限

- 当前 GitHub PAT 无「创建仓库」权限（个人/组织均 403），新建仓库需先在网页创建再推送；
  对已有组织仓库（如 PML-2）具备 admin/push 权限。
