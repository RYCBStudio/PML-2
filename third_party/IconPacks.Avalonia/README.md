# IconPacks.Avalonia（本地 fork）

本目录是 [MahApps/IconPacks.Avalonia](https://github.com/MahApps/IconPacks.Avalonia) 的**最小化本地 fork**，
用于 Avalonia 12.1 升级（`.qoder/plans/Avalonia_12_升级_SPike_5e38763a.md` 计划 P1.6）。

只保留项目实际使用的 4 个图标包及其依赖 `Core`，上游其余 40 余个图标包未纳入。

## 上游基线

| 项 | 值 |
| --- | --- |
| 仓库 | `https://github.com/MahApps/IconPacks.Avalonia.git` |
| 分支 | `release/2.0.0` |
| 提交 | `ff2dff06a942f015d8c1862f090644b3e1d97e78` |
| 许可 | MIT（见 [LICENSE](LICENSE)） |
| 原目标框架 | `net8.0;net6.0;netstandard2.0` |
| 原 Avalonia 版本 | `11.0.13` |

## 为什么必须 fork

NuGet 上的 `IconPacks.Avalonia.* 2.0.0` 是针对 **Avalonia 11.0.13** 编译的二进制。
Avalonia 12 中存在一处二进制不兼容（`MissingMethodException`，非编译期错误）：

```
Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension.ProvideValue(System.IServiceProvider)
    返回类型：Avalonia 11 = Avalonia.Data.IBinding
              Avalonia 12 = Avalonia.Data.BindingBase
```

`PackIcon.axaml` / `PackIconControlBase.axaml` 的 `ControlTheme` 内部使用
`<StaticResource>` / `<DynamicResource>`，因此**直接引用 NuGet 2.0.0 时**：

- 还原与编译**均成功**（无任何告警）；
- `AvaloniaXamlLoader.Load(avares://IconPacks.Avalonia.Lucide/Lucide.axaml)` 也**成功**；
- 但在 Avalonia 12 下真正套用 `ControlTheme` 时抛 `MissingMethodException`，
  图标**静默渲染为空白**（不会崩溃，只在调试输出里可见）。

即：这是一处会「编译通过、运行失败」的陷阱，仅靠 restore/build 无法发现。
重新针对 Avalonia 12 编译本 fork 即可消除。

> 复现与验证脚本：`probe/`（本目录内的诊断工程，可重复执行）。
> 覆盖 4 个包的实例化、4 个 `avares://` 主题加载、`ControlTheme` 套用与 `Path.Data` 解析：
>
> ```powershell
> dotnet run --project third_party/IconPacks.Avalonia/probe   # 退出码 0 = 通过
> ```
>
> 该工程**不属于**主 sln，也不被任何项目引用，仅在需要时显式运行。

## 相对上游的改动（全部必要且最小）

1. **目标框架** `net8.0;net6.0;netstandard2.0` → `net10.0`，与主仓库一致。
2. **Avalonia** `11.0.13` → `12.1.3`（见 `src/Directory.Packages.props`）。
3. **`ConstructorArgumentAttribute` 的命名空间迁移**：
   Avalonia 12 将其从 `Avalonia.Markup.Xaml` 移至 `Avalonia.Metadata`（位于 `Avalonia.Base`）。
   共修改 8 个 `*Extension.cs`（4 个包的 `PackIcon*Extension.cs` 与 `PackIcon*ImageExtension.cs`）。
4. **移除 `JetBrains.Annotations`**：本 fork 保留的 5 个项目对其**无任何用法**
   （已全量检索 `[NotNull]` / `[PublicAPI]` / `[CanBeNull]` / `[UsedImplicitly]` 等均为空），
   移除可减少构建期依赖与 AOT 裁剪面。
5. **移除 `System.Text.Json` 显式引用**：自 `net10.0` 起内置于 BCL，显式引用只会触发 `NU1510`。
6. **不导入**上游 `build/SourceLink.props` 与 `build/SharedVersion.props`（已从 fork 中删除）：
   前者需 `Microsoft.SourceLink.GitHub`，后者是 NuGet 打包元数据，均与本地 fork 无关，
   导入只会给主程序引入多余的构建期依赖与 AOT 裁剪面。
   程序集签名（`build/SignAssembly.props` + `build/IconPacks.Avalonia.snk`）保留，以保持程序集标识连续。

`src/Directory.Build.props` 与 `src/Directory.Packages.props` 为**新写**（非上游原样拷贝）。

## 关键约束：程序集名与资源路径不可变

主程序 `App.axaml` 中 4 处 `StyleInclude` 依赖以下 URI，**必须保持不变**：

```
avares://IconPacks.Avalonia.Lucide/Lucide.axaml
avares://IconPacks.Avalonia.FileIcons/FileIcons.axaml
avares://IconPacks.Avalonia.Material/Material.axaml
avares://IconPacks.Avalonia.SimpleIcons/SimpleIcons.axaml
```

因此各 `csproj` 的 `<AssemblyName>`、`*.axaml` 文件名与
`Properties/AssemblyInfo.cs` 中的 `XmlnsDefinition` / `XmlnsPrefix`
（`https://github.com/MahApps/IconPacks.Avalonia`）均**不得重命名**。

## 如何引用

主程序 `MEFrpLauncherX.csproj` 以 `ProjectReference` 引用 4 个图标包：

```xml
<ProjectReference Include="..\third_party\IconPacks.Avalonia\src\IconPacks.Avalonia.Lucide\IconPacks.Avalonia.Lucide.csproj" />
```

`packages.lock.json` / 中央包版本管理（根 `Directory.Packages.props`）中的
`IconPacks.Avalonia.*` 条目**应保持存在**（供回滚使用），但不再是主程序的解析来源。

## 如何回滚

按计划「回滚」章节，把 4 处 `ProjectReference` 换回 `PackageReference`
（`IconPacks.Avalonia.* 2.0.0`）即可 —— 版本号仍保留在根 `Directory.Packages.props` 中。
**注意**：回滚到 NuGet 2.0.0 后必须同时停留在 Avalonia 11；在 Avalonia 12 下会复现上述
`MissingMethodException` 导致图标空白。

## 如何随上游更新

```powershell
git clone --depth 1 --branch release/2.0.0 https://github.com/MahApps/IconPacks.Avalonia.git
# 仅取：src/IconPacks.Avalonia.{Core,Lucide,Material,FileIcons,SimpleIcons}、
#       build/IconPacks.Avalonia.snk 与 build/SignAssembly.props、LICENSE
# 然后重新施加「相对上游的改动」中的第 3 项（ConstructorArgumentAttribute 命名空间迁移）
```

## 新增图标包

上游有 40 余个图标包。如需新增（例如 `IconPacks.Avalonia.Codicons`）：
从上游拷贝对应 `src/IconPacks.Avalonia.<Name>/` 目录，施加第 3 项命名空间改动，并在
根 `Directory.Packages.props` 中补一条 `PackageVersion`（供回滚），
同时在本文件上方表格中登记。
