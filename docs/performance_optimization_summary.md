# 软件性能优化总结

## 背景

第一轮优化引入的代码存在 **4 处编译错误**，因此从未真正生效（此前观察到的"优化无效"即由此导致）。
本轮先修复编译错误，再重做各项优化。

```
MEFrpLauncherX/Views/MainWindow.axaml.cs(84,9): error CS0103: 名称"UseHostedCompositionRenderLoop"不存在
MEFrpLauncherX/Views/SettingsPage.axaml.cs(507,29): error CS1061: "AppWindow"未包含"Children"的定义
MEFrpLauncherX/Views/SettingsPage.axaml.cs(553,28): error CS0103: 名称"None"不存在
MEFrpLauncherX/Views/SettingsPage.axaml.cs(571,33): error CS1061: "AppWindow"未包含"Children"的定义
```

- `UseHostedCompositionRenderLoop` 在 Avalonia 11.3.20 中**并不存在**（已核对 `Avalonia.Controls.dll` / `FluentAvalonia.dll`），必须删除。
- `Window`（含 `AppWindow`）没有 `Children`，只有 `Content`；遮罩层应挂到窗口内容根 `Panel` 上。
- `FillMode` 枚举需写成 `FillMode.Forward`。

---

## 1. 最小化恢复卡顿

### 现象
最小化后恢复，窗口左上角先出现一块色块，卡顿 2~5 秒后才显示完整界面。

### 根因

**主因：每次窗口激活都会同步解码整张背景大图。**
`Activated` 在"任务栏恢复窗口"时同样会触发，而原 `OnActivated` 每次都会执行：

```csharp
Background = new ImageBrush(new Bitmap(背景图路径));   // UI 线程同步解码，大图可达数秒
```

同时 `UpdateLayout`/`InvalidateVisual` 无法在解码完成前推进，于是窗口内容整体延后显示，
期间只绘制出上一帧的局部脏区（表现为左上角色块）。

**次因：恢复瞬间未强制整窗重绘。** 渲染器可能只刷新局部脏区。

### 解决方案

#### 1.1 背景图只应用一次，解码移出 UI 线程

`MainWindow.axaml.cs`：

```csharp
private bool _backgroundApplied;
private (string Path, Bitmap? Image)? _backgroundCache;

private async void OnActivated(object? sender, EventArgs e)
{
    if (!_backgroundApplied)
    {
        _backgroundApplied = true;
        await ApplyBackgroundAsync();   // Task.Run 中解码，并缓存 Bitmap
    }
    // ...
}
```

- 解码放入 `Task.Run`，UI 线程不再被阻塞。
- 按路径缓存 `Bitmap`，切换拉伸/填充模式时不重复解码。
- 失败时写日志并安全返回，不再抛出。

#### 1.2 恢复时强制整窗重绘

`Window` 没有公开的 `WindowStateChanged` 事件（该成员只在内部平台接口 `IWindowImpl` 上），
因此改为重写 `OnPropertyChanged` 监听 `WindowStateProperty`：

```csharp
protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
{
    base.OnPropertyChanged(change);

    if (change.Property != WindowStateProperty || change.GetNewValue<WindowState>() == WindowState.Minimized)
    {
        return;
    }

    RequestFullRedraw();
}

private void RequestFullRedraw()
{
    Dispatcher.UIThread.Post(() =>
    {
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
    }, DispatcherPriority.Render);

    // 第二帧补一次，覆盖恢复动画期间到达的迟到帧
    Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Background);
}
```

配合 `CompositionOptions.UseRegionDirtyRectClipping = true`（脏区裁剪）时，
必须显式整窗失效才能避免"只刷出一块色块"。

---

## 2. 主题切换动画

### 问题
主题切换生硬，无过渡；且原实现的遮罩层挂载点非法（`AppWindow.Children`）。

### 解决方案

遮罩层挂到窗口内容根（`MainWindow` 的根节点是 `Panel`），以目标主题底色淡入覆盖 →
切换主题 → 淡出：

```csharp
if (Core.App.MainWindow.Content is not Panel root)
{
    Application.Current?.RequestedThemeVariant = newVariant;
    return;
}

var overlay = new Border
{
    Background = new SolidColorBrush(newVariant == ThemeVariant.Dark
        ? Color.Parse("#FF202020")
        : Color.Parse("#FFF3F3F3")),
    Opacity = 0,
    IsHitTestVisible = false,   // 过渡期间不拦截输入
    ZIndex = 9999
};
root.Children.Add(overlay);
```

- 总时长约 660ms（淡入 300ms + 60ms + 淡出 300ms）。
- 用 `_isThemeTransitioning` 防止动画叠加；重复切换时退化为直接切换。
- `finally` 中移除遮罩并复位标志。

---

## 3. 启动与渲染性能

### 3.1 启动路径去重（`MainWindow.OnLoaded`）

| 问题 | 处理 |
|------|------|
| `CreateContextMenu()` 被调用两次（其中一次结果丢弃），每次都额外解码一次图标位图 | 删除多余调用 |
| `OnLoaded` 中 `new MainWindowViewModel()` 覆盖了 `App` 已创建的实例，导致整棵绑定树重新求值 | 改为 `DataContext as MainWindowViewModel ?? new MainWindowViewModel()` |

### 3.2 `AnimatedProgressRing` 常驻 60fps 定时器

该控件在构造函数中启动 16ms `DispatcherTimer` 且**从不停止**，
即使加载动画早已隐藏仍永久占用 UI 线程。

改为按可见性/挂载状态启停：

```csharp
protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
{
    base.OnAttachedToVisualTree(e);
    SetRunning(IsVisible);
}

protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
{
    SetRunning(false);
    base.OnDetachedFromVisualTree(e);
}

protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
{
    base.OnPropertyChanged(change);
    if (change.Property == IsVisibleProperty)
    {
        SetRunning(change.GetNewValue<bool>());
    }
}
```

`Stopwatch` 支持断点续计，停止后重新开始不会重置动画相位。

### 3.3 启动进度动画（`AsymptoticProgressBehavior`，全新文件）

`MainWindow.axaml` 的 `ProgressBar` 挂载渐近进度行为，假进度平滑逼近 `Target=95`，
初始化结束后由 `MainWindowViewModel.IsBusy = false` 触发平滑冲满：

```xml
<behaviors:AsymptoticProgressBehavior
    IsRunning="{Binding IsBusy, FallbackValue=True}"
    IsFakeMode="True"
    Target="95"
    Speed="0.15"
    UpdateInterval="0:0:0.030"
    JumpToMaximumOnStop="True"
    CompleteAnimationDuration="0:0:0.4" />
```

渐近公式：`progress = base + (Target - base) * (1 - e^(-k·t))`。

---

## 文件修改清单

| 文件 | 修改内容 |
|------|----------|
| `MEFrpLauncherX/Views/MainWindow.axaml.cs` | 删除不存在的 `UseHostedCompositionRenderLoop`；新增 `OnPropertyChanged`/`RequestFullRedraw`；`OnActivated` 改为背景图仅应用一次并异步解码（`ApplyBackgroundAsync`/`GetBackgroundBitmapAsync`）；复用 ViewModel；删除重复的 `CreateContextMenu()` |
| `MEFrpLauncherX/Views/SettingsPage.axaml.cs` | 遮罩层挂到 `MainWindow.Content` 的 `Panel`；修正 `FillMode.Forward`；新增过渡重入保护与 `IsHitTestVisible = false` |
| `MEFrpLauncherX/Controls/AnimatedProgressRing.axaml.cs` | 16ms 定时器按可见性/挂载状态启停 |
| `MEFrpLauncherX/Behaviours/AsymptoticProgressBehavior.cs` | 新增（渐近假进度行为） |
| `MEFrpLauncherX/Views/MainWindow.axaml` | `ProgressBar` 挂载 `AsymptoticProgressBehavior` |
| `MEFrpLauncherX.Core/ViewModels/MainWindowViewModel.cs` | 新增 `IsBusy` 属性 |
| `MEFrpLauncherX/Program.cs` | （既有）`CompositionOptions` / `Win32PlatformOptions` 渲染配置 |

---

## 已验证 / 待验证

- 已验证：`dotnet build MEFrpLauncherX/MEFrpLauncherX.csproj -c Debug -t:Compile` → **0 error**。
- 待人工验证：最小化→恢复是否仍需长时间才显示完整窗口；
  若仍存在明显延迟，下一步应排查最小化期间仍在运行的终端/图表渲染对恢复首帧的影响。

---

## 参考资料

- [Avalonia Animation 文档](https://docs.avaloniaui.net/docs/next/animation/overview)
- [Avalonia 窗口操作 How-to](https://docs.avaloniaui.net/docs/how-to/window-how-to)

---

**优化日期**: 2026-09-19
**优化版本**: release/26.3
**编译状态**: ✅ 0 error
