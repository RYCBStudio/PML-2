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

**优化日期**: 2026-09-19
**优化版本**: release/26.3
**编译状态**: ✅ 0 error

---

# 第二轮：帧率与页签过渡（26.4.0，2026-10-02）

## 背景

第一轮解决的是「启动与恢复卡顿」。第二轮针对**持续帧率**与**页签切换观感**，
并补齐「编译通过、运行期才炸」这类问题的自检手段。

## 1. 渲染诊断叠加层改为按需开启

原实现**默认常开** FPS + 布局耗时图 + 渲染耗时图，问题有三：

1. 叠加层自身每帧都要绘制文字与曲线，**会轻微影响被测对象**，测量结果不可信；
2. `RendererDebugOverlays` **没有定义 `All` 成员**，必须显式组合 `Fps | LayoutTimeGraph | RenderTimeGraph | DirtyRects`（写成 `All` 直接编译失败）；
3. `DirtyRects` 会让 `CompositionTargetOverlays.RequireLayer` 额外引入一层合成层，**改变被观测对象的行为**。

改为由环境变量 `PML2_RENDER_DIAG` 控制，默认 `None`：

```csharp
// MainWindow.axaml.cs（仅 #if DEBUG）
RendererDiagnostics.DebugOverlays = ResolveDebugOverlays();
// 取值：1 / all → 全部；或 fps、layout、render、dirty 逗号组合
```

`dirty` 刻意不进入默认组合，仅在专门排查脏矩形时手动开启。

## 2. 渲染后端顺序：AngleEgl 必须排在 Vulkan 之前

这是一处**确定性缺陷**（非随机、每次复现）。`Win32GlManager.InitializeCore()` 中
**只有 AngleEgl 的成功分支会调用 `TryRegisterComposition()`**，从而注册
`WinUIComposition` / `DirectComposition` / `LowLatencyDxgiSwapChain`。

原默认列表把 `Vulkan` 放在最前，于是：

- Vulkan 优先成功 → composition **永不注册**；
- 渲染循环退回 `SleepLoopRenderTimer`，即**固定 60Hz 睡眠式**推进；
- 「低延迟渲染」开关**完全失效**（其依赖的合成模式根本没被注册）。

修正后的顺序与依据已写入 `Program.cs` 的 `BuildWin32Options` 注释：

```csharp
_ => [Win32RenderingMode.AngleEgl, Win32RenderingMode.Vulkan, Win32RenderingMode.Wgl, Win32RenderingMode.Software]
```

## 3. 强调色呼吸动画（根因修复）

### 现象
主题强调色呼吸动画持续运行期间，整界面出现周期性掉帧。

### 根因

动画每帧调用 `CustomAccentColor` setter → `UpdateAccentColors()` →
**移除并重建 7 键 `ResourceDictionary`** → `StyledElement` 递归通知**整棵可视树**资源变更。
即「每帧 × 全树」的失效风暴，且原实现每帧会被触发多次。

### 解决

`MainWindow.Themes.cs` 改为**每帧至多应用一次**：

- 用「待应用颜色」字段替代每帧直接写 setter，避免同一帧内重复触发重建；
- 顺带修复**时间漂移**（相位按累计时间计算，不随帧率抖动累积误差）与一处 **NRE**。

## 4. 数字滚动动画共用节拍器

`RollingNumberTextBlock` 与 `RollingNumberDoubleTextBlock` 原先**各自持有一个 16ms
`DispatcherTimer`**。首页 + 节点监控页同时常驻约 10 个实例，即约 **600 次/秒**的独立调度器唤醒，
且彼此**不同步**，会与渲染帧错位。

新增 `Controls/RollingAnimationTicker.cs`：全局共享一个 16ms 计时器，并改用
`DispatcherPriority.Render` 让回调与渲染帧合并到同一优先级批次；**无订阅者时自动停止**，空闲零开销。
两个控件改为注册/注销回调。

> 注意：滚动动画作用于文本/数字，属「内容变化」而非「变换」，因此这里通过降低调度开销而非
> `RenderTransform` 来优化。

## 5. FluentCard 阴影：Effect → BoxShadow

`Styles/_generic.axaml` 的 FluentCard 模板原用 `DropShadowEffect`，模板内一处、`:pointerover` 处再设一次。

`DropShadowEffect` 属**像素效果**，会把 Border 及其子树**光栅化到中间 surface** 再应用 `ImageFilter`，
每张卡因此多出一个**合成层**；首页/收件箱同时存在约 11 张 FluentCard，叠加第 3 节的
「每帧全树失效」后，这些层被反复重绘。

改为 `Border.BoxShadow`（绘制背景时直接画阴影，**不产生中间 surface**），并删除 `:pointerover` 处的
第二个 Effect —— 悬浮反馈改由「放大 + 模板内同一处阴影」共同提供。

## 6. 背景图采样降为中等质量

窗口背景是**整窗缩放**的。默认的 `BitmapInterpolationMode.Unspecified` 与 `HighQuality`
在上采样时都会走三次重采样（`HighQuality → SKCubicResampler.Mitchell`）；
内置 splash.png 为 1706×1066，在 2560 宽窗口下**每帧**都要做一次约 260 万像素的三次插值。

改为 `MediumQuality`。注意：该属性**没有对应的 CLR 属性**（只有
`RenderOptions.Get/SetBitmapInterpolationMode`），**无法写在 XAML 里**，只能代码设置。

## 7. 页签切换过渡（新增能力）

| 载体 | 机制 |
|---|---|
| `TabControl`（隧道管理 / 终端 / 主题 / 更新页） | Avalonia 12 原生 `TabControl.PageTransition`，直接绑定共享资源 |
| `TabStrip`（创建隧道 / 节点监控 / 更新内容窗口） | `TabStrip` **不承载内容、无过渡机制**，需自写附加属性行为（`Behaviours/TabStripContentTransitionBehavior.cs`） |

新增 `Styling/TabContentFadeScaleTransition.cs`（`IPageTransition`），并在 `App.axaml` 注册为共享资源
`TabContentTransition`；**运行时读取**「动画程度」配置，因此无需重建实例即可即时生效。

### ⚠️ 关键陷阱：不能对 `RenderTransform` 做关键帧动画

首版实现把 `Visual.RenderTransformProperty` 写进关键帧，**编译、`XamlSmoke` 全绿**，
但一运行就抛：

```
InvalidOperationException: No animator registered for the property RenderTransform.
```

原因：Avalonia 的动画器按**属性类型**自动注册（`Animation.AnimatorRegistry` 的 `Animators` 列表），
表中注册了 `double / bool / Color / Thickness / BoxShadows / IBrush / …`，
但**没有 `ITransform` / `TransformOperations` 条目**（`RenderTransform` 的类型正是 `ITransform`）。
且 `Animation.Animators` 与 `Animator<T>` 均为 **internal**，外部**无法手动补注册**。

正确做法是动画**具体变换对象上的数值属性**，命中 `DoubleAnimator` 后由 `TransformAnimator`
路由到该变换（Avalonia 自带 `PageSlide` 就是这么做的 —— 它动画 `TranslateTransform.X`）：

```csharp
// ✅ 正确：ScaleTransform 上的 double 属性
new Setter(ScaleTransform.ScaleXProperty, scale),
new Setter(ScaleTransform.ScaleYProperty, scale)

// ❌ 错误：运行期抛 InvalidOperationException
new Setter(Visual.RenderTransformProperty, TransformOperations.Parse("scale(0.98)"))
```

另外 `TransformAnimator` 对 `RenderTransform` 为 `TransformOperations` 的情况会直接
`return Disposable.Empty`（官方标注 `HACK: cannot reasonably animate CSS transforms`），
因此按 `ScaleX/ScaleY` 方案还能顺带绕开该分支。

### 过渡实现要点

- **只动 `Opacity` + `RenderTransform`**，绝不碰 `Width`/`Height`/`Margin`/`Padding`，不触发重新布局；
- 动画前显式建立 `RenderTransform = new ScaleTransform(...)`，使 `TransformAnimator` 走
  「已是目标类型」分支，对**该实例**做动画；
- `FillMode.Forward` 会保持终值，**结束后必须显式归位**（`Opacity = 1`、`RenderTransform = null`），
  否则元素会被提升为独立合成层并长期参与合成；已取消的情况下则**跳过归位**；
- 旧内容 `1/0.98` 缩退、新内容 `0.98 → 1` 放大，两者互逆，避免「两边同时变大」；
- `TabStrip` 的内容宿主是**同一个控件**（不像 `TabControl` 会轮换两个 ContentPresenter），
  因此用 `ConditionalWeakTable<TabStrip, CancellationTokenSource>` 在连续切换时**取消上一段过渡**；
  否则旧过渡完成后的归位会抹掉新过渡正在进行的变换，表现为「闪一下」。

## 8. 清理

删除未被引用的 `Controls/SpotlightBackground.axaml(.cs)`（保留 `AnimatedFAProgressRing`，
它被 `XamlSmoke` 作为探测目标引用）。

---

## 第二轮验证方式

| 手段 | 命令 | 结果 |
|---|---|---|
| 主项目编译 | `dotnet build MEFrpLauncherX/MEFrpLauncherX.csproj -c Debug -t:Compile` | **0 error** |
| XAML 运行期加载 | `dotnet run --project tools/XamlSmoke` | **exit 0** |
| 页签过渡运行期播放 | `dotnet run --project tools/TabTransitionVerify` | **exit 0**（不抛异常） |

> `-t:Compile` 只编译到 `obj`，**不会**复制到 `bin`；且主程序正在运行时 `bin` 可能被文件锁占用
> （`MSB3027`/`MSB3021`）。这两个报错与代码正确性无关，必要时先关闭应用再完整构建。

**已知限制**：`TabTransitionVerify` 在 headless 下**无法确定性推进动画时钟**（时钟只能靠
`ForceRenderTimerTick` 推进，实测推进幅度与 tick 次数非线性），因此该工具**只断言「播放过程不抛异常」**，
不断言「动画是否跑完、是否归位」。这两点需在真实窗口下人工确认。

## 参考资料

- [Avalonia Animation 文档](https://docs.avaloniaui.net/docs/next/animation/overview)
- [Avalonia 窗口操作 How-to](https://docs.avaloniaui.net/docs/how-to/window-how-to)
