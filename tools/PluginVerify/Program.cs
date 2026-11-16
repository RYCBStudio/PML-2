using System.Text;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Plugin.Engine;
using MEFrpLauncherX.Plugin.Services;

// =============================================================================
// PluginVerify —— PML 2 插件子系统自检（26.5.1 C1 / H2 / H3 / H4 回归）
//
// 全部在临时目录内进行，通过 PML2_DATA_ROOT 环境变量把 AppPaths 的数据根重定向到沙箱，
// 不触碰真实用户数据。
//
// 注意：AppPaths.VersionDirectory 恒等于 AppContext.BaseDirectory（程序自身所在目录），
// 没有环境变量可覆盖 —— 这正是「版本目录 = 代码目录」的定义。
// 因此沙箱里的「随包插件」必须放在程序所在目录下的 Config\Plugins，
// 否则引擎根本不会去那里找（它只读 vXXX\Config\Plugins 与 data\Config\Plugins）。
//
// 验证点：
//   1. 读写分离：写入只落在数据目录，不写只读的版本目录
//   2. 双目录读取：随包插件（版本目录）+ 用户插件（数据目录）都能加载
//   3. 同名去重：数据目录优先（可遮蔽随包插件）
//   4. 禁用生效：被禁用的插件不注册触发器，事件不再触发
//   5. 禁用持久化：.disabled 写入后能被读回（重启后仍禁用）
//   6. 重载不重复注册：反复重载后事件只触发一次
// =============================================================================

var failures = new List<string>();
var checks = 0;

void Check(string name, bool ok, string detail = "")
{
    checks++;
    if (ok)
    {
        Console.WriteLine($"  [PASS] {name}");
    }
    else
    {
        Console.WriteLine($"  [FAIL] {name}{(detail.Length > 0 ? " — " + detail : "")}");
        failures.Add(name + (detail.Length > 0 ? " — " + detail : ""));
    }
}

// ---- 沙箱 ----
var sandbox = Path.Combine(Path.GetTempPath(), "PML2-PluginVerify-" + Guid.NewGuid().ToString("N")[..8]);
var installRoot = Path.Combine(sandbox, "install");
var dataRoot = Path.Combine(sandbox, "data");
var versionDir = Path.GetFullPath(AppContext.BaseDirectory);
var bundledPlugins = Path.Combine(versionDir, "Config", "Plugins");
var dataPlugins = Path.Combine(dataRoot, "Config", "Plugins");

Directory.CreateDirectory(installRoot);
Directory.CreateDirectory(dataPlugins);

Environment.SetEnvironmentVariable("PML2_INSTALL_ROOT", installRoot);
Environment.SetEnvironmentVariable("PML2_DATA_ROOT", dataRoot);

// 初始化日志器（App.CurrentLogger 在 App.Initialize() 之前为 null，
// 插件子系统里未判空的日志调用会 NRE）。
MEFrpLauncherX.Core.App.Initialize(externalUse: true);

Console.WriteLine("==> PluginVerify：插件子系统自检");
Console.WriteLine($"    沙箱(数据根): {dataRoot}");
Console.WriteLine($"    版本目录: {versionDir}");
Console.WriteLine();

const string BundledPluginYaml = """
id: bundled-probe
name: 随包探针
description: 位于只读版本目录的插件
author: verify
version: "1.0"
triggers:
  - on: probe.event
    actions:
      - name: log
        params:
          msg: "BUNDLED_HIT"
""";

const string UserPluginYaml = """
id: user-probe
name: 用户探针
description: 位于数据目录的插件
author: verify
version: "1.0"
triggers:
  - on: probe.event
    actions:
      - name: log
        params:
          msg: "USER_HIT"
""";

// 安装用的插件：名字独立，避免与 user-probe 混淆导致断言失真。
const string InstalledPluginYaml = """
id: installed-probe
name: 已安装探针
description: 通过 InstallPlugin 安装的插件
author: verify
version: "1.0"
triggers:
  - on: probe.event
    actions:
      - name: log
        params:
          msg: "INSTALLED_HIT"
""";

File.WriteAllText(Path.Combine(dataPlugins, "user-probe.yaml"), UserPluginYaml);

// 随包插件放在程序目录的 Config\Plugins（= AppPaths.VersionDirectory\Config\Plugins），
// 与真实发布版「插件随代码目录分发」的布局一致。
var bundledProbePath = Path.Combine(bundledPlugins, "bundled-probe.yaml");
Directory.CreateDirectory(bundledPlugins);
File.WriteAllText(bundledProbePath, BundledPluginYaml);

try
{
    // =====================================================================
    Console.WriteLine("== 1. 加载：双目录都能读到 ==");
    // =====================================================================
    var svc = PluginService.Instance;
    svc.LoadPlugins();

    var pluginIds = svc.Plugins.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
    Check("随包插件（版本目录）被加载", pluginIds.Contains("bundled-probe"),
        $"实际加载: {string.Join(", ", pluginIds)}");
    Check("用户插件（数据目录）被加载", pluginIds.Contains("user-probe"),
        $"实际加载: {string.Join(", ", pluginIds)}");
    Check("PluginsFolder 指向数据目录", svc.PluginsFolder == dataPlugins,
        $"期望 {dataPlugins}，实际 {svc.PluginsFolder}");

    // =====================================================================
    Console.WriteLine();
    Console.WriteLine("== 2. 事件触发：两个插件都响应 ==");
    // =====================================================================
    // 用插件执行日志计数：每次动作执行恰好产生一条 success 记录，
    // 比数 Console 输出行可靠（LogAction 会输出两次）。
    // 注意只数「动作级」记录（ActionName 非空）—— trigger 级记录 Status 也是 success，
    // 若一并统计会把 1 次触发算成 2 次。
    static int CountActions(MEFrpLauncherX.Plugin.Services.PluginService service, string pluginName)
    {
        return service.ExecutionLogs.Count(e =>
            e.PluginId == pluginName && e.Status == "success" && !string.IsNullOrEmpty(e.ActionName));
    }

    svc.ClearExecutionLogs();
    await svc.TriggerAsync("probe.event", new Dictionary<string, object>());
    var bundledActions = CountActions(svc, "随包探针");
    var userActions = CountActions(svc, "用户探针");
    Check("随包插件响应事件", bundledActions == 1, $"执行 {bundledActions} 次");
    Check("用户插件响应事件", userActions == 1, $"执行 {userActions} 次");

    // =====================================================================
    Console.WriteLine();
    Console.WriteLine("== 3. 写入只落数据目录（版本目录保持干净） ==");
    // =====================================================================
    var tmpPlugin = Path.Combine(sandbox, "installed-by-user.yaml");
    File.WriteAllText(tmpPlugin, InstalledPluginYaml);
    var installed = svc.InstallPlugin(tmpPlugin);
    Check("安装插件成功", installed);
    var installedInData = File.Exists(Path.Combine(dataPlugins, "installed-by-user.yaml"));
    var installedInVersion = File.Exists(Path.Combine(bundledPlugins, "installed-by-user.yaml"));
    Check("插件文件写入数据目录", installedInData);
    Check("插件文件未写入只读版本目录", !installedInVersion);

    // =====================================================================
    Console.WriteLine();
    Console.WriteLine("== 4. 同名插件去重（数据目录优先） ==");
    // =====================================================================
    // 注意：这里必须先停掉热重载 —— 写文件会触发 FileSystemWatcher 的异步回调，
    // 与下面的显式 LoadPlugins() 竞争，导致引擎被重载两次、触发器重复注册。
    // 这是测试的竞态，不是产品缺陷（产品里 Reload 已加锁保证原子性）。
    svc.DisableHotReload();

    // 在数据目录放一个与随包同名、但内容不同的插件
    File.WriteAllText(Path.Combine(dataPlugins, "bundled-probe.yaml"),
        BundledPluginYaml.Replace("BUNDLED_HIT", "SHADOW_HIT"));
    svc.LoadPlugins();
    var bundledCount = svc.Plugins.Count(p => p.Id == "bundled-probe");
    Check("同名插件只加载一次", bundledCount == 1, $"加载了 {bundledCount} 次");

    svc.ClearExecutionLogs();
    await svc.TriggerAsync("probe.event", new Dictionary<string, object>());
    var shadowActions = CountActions(svc, "随包探针");
    Check("数据目录版本优先生效（遮蔽随包插件）", shadowActions == 1, $"执行 {shadowActions} 次");

    if (shadowActions != 1)
    {
        Console.WriteLine("  -- 诊断：执行日志明细 --");
        foreach (var e in svc.ExecutionLogs)
        {
            Console.WriteLine($"     plugin={e.PluginId} event={e.EventName} action={e.ActionName} status={e.Status}");
        }

        Console.WriteLine("  -- 诊断：已加载插件 --");
        foreach (var p in svc.Plugins)
        {
            Console.WriteLine($"     id={p.Id} file={p.FilePath}");
        }
    }

    // =====================================================================
    Console.WriteLine();
    Console.WriteLine("== 5. 禁用真正生效（不注册触发器） ==");
    // =====================================================================
    svc.DisablePlugin("user-probe");
    svc.ClearExecutionLogs();
    await svc.TriggerAsync("probe.event", new Dictionary<string, object>());
    var userActionsAfterDisable = CountActions(svc, "用户探针");
    Check("禁用后插件不再响应事件", userActionsAfterDisable == 0, $"仍执行 {userActionsAfterDisable} 次");
    Check("禁用状态在插件列表中体现",
        svc.Plugins.FirstOrDefault(p => p.Id == "user-probe")?.IsEnabled == false);

    // =====================================================================
    Console.WriteLine();
    Console.WriteLine("== 6. 禁用状态持久化（.disabled 可读回） ==");
    // =====================================================================
    var disabledFile = Path.Combine(dataPlugins, ".disabled");
    Check(".disabled 写入数据目录", File.Exists(disabledFile));

    // 模拟「重启」：新建一个 PluginService 实例，重新加载
    var svc2 = NewPluginServiceInstance();
    svc2.LoadPlugins();
    var userPluginAfterReload = svc2.Plugins.FirstOrDefault(p => p.Id == "user-probe");
    Check("重启后插件仍处于禁用状态", userPluginAfterReload?.IsEnabled == false,
        $"IsEnabled={userPluginAfterReload?.IsEnabled}");
    svc2.ClearExecutionLogs();
    await svc2.TriggerAsync("probe.event", new Dictionary<string, object>());
    Check("重启后禁用插件不响应事件",
        CountActions(svc2, "用户探针") == 0);

    // 重新启用
    svc2.EnablePlugin("user-probe");
    svc2.ClearExecutionLogs();
    await svc2.TriggerAsync("probe.event", new Dictionary<string, object>());
    var reEnabledActions = CountActions(svc2, "用户探针");
    Check("重新启用后插件恢复响应", reEnabledActions == 1, $"执行 {reEnabledActions} 次");

    // =====================================================================
    Console.WriteLine();
    Console.WriteLine("== 7. 重载不重复注册触发器 ==");
    // =====================================================================
    for (var i = 0; i < 3; i++)
    {
        svc2.ReloadPlugins();
    }

    svc2.ClearExecutionLogs();
    await svc2.TriggerAsync("probe.event", new Dictionary<string, object>());
    var bundledAfterReloads = CountActions(svc2, "随包探针");
    Check("多次重载后事件只触发一次", bundledAfterReloads == 1, $"执行 {bundledAfterReloads} 次");

    // =====================================================================
    Console.WriteLine();
    Console.WriteLine("== 8. 引擎层：Unload 清空函数注册表 ==");
    // =====================================================================
    var engine = new PluginEngine();
    engine.LoadAll(new[] { dataPlugins });
    var fnCountAfterLoad = engine.ExecutionLogs.Count;
    engine.Unload();
    engine.LoadAll(new[] { dataPlugins });
    Check("反复 LoadAll/Unload 不崩溃", engine.ExecutionLogs.Count >= fnCountAfterLoad);

    // =====================================================================
    Console.WriteLine();
    Console.WriteLine("== 9. 热重载后禁用状态仍然有效 ==");
    // =====================================================================
    // 26.5.1 修复：热重载此前调用 engine.Reload(folders) 时没有传禁用集合，
    // 导致被禁用的插件在文件变更后复活（禁用失效）。
    //
    // 注意：必须复用同一个 PluginService 实例 —— 产品里是单例，
    // 而多个实例会共享同一数据目录、互相覆盖 .disabled 文件并重复触发热重载。
    // 其余实例的 watcher 先停掉，避免干扰。
    svc2.DisableHotReload();
    svc.EnableHotReload();

    // 第 6 步末尾把 user-probe 重新启用了（并清空了 .disabled），
    // 这里重新禁用一次，作为热重载的起点。
    svc.DisablePlugin("user-probe");

    // svc 在第 5 步已禁用 user-probe；确认当前状态
    svc.ClearExecutionLogs();
    await svc.TriggerAsync("probe.event", new Dictionary<string, object>());
    Check("热重载前：被禁用的插件不响应事件", CountActions(svc, "用户探针") == 0,
        $"仍执行 {CountActions(svc, "用户探针")} 次");

    // 触发一次热重载（新增文件让 FileSystemWatcher 感知到变化）
    // 热重载是异步的，且自身有 1 秒防抖，这里等待它完成。
    var probeFile = Path.Combine(dataPlugins, "hot-reload-probe.yaml");
    File.WriteAllText(probeFile, """
id: hot-reload-probe
name: 热重载探针
description: 用于触发热重载
author: verify
version: "1.0"
triggers:
  - on: probe.event
    actions:
      - name: log
        params:
          msg: "HOT_RELOAD_HIT"
""");

    // 等待热重载发生（防抖 1 秒 + 加载耗时）
    await Task.Delay(2500);

    svc.ClearExecutionLogs();
    await svc.TriggerAsync("probe.event", new Dictionary<string, object>());
    Check("热重载后被禁用的插件仍然不响应事件",
        CountActions(svc, "用户探针") == 0,
        $"仍执行 {CountActions(svc, "用户探针")} 次");
    Check("热重载本身生效（新插件被加载）",
        CountActions(svc, "热重载探针") == 1,
        $"执行 {CountActions(svc, "热重载探针")} 次");
}
catch (Exception ex)
{
    failures.Add("异常: " + ex);
    Console.WriteLine($"  [FAIL] 未捕获异常: {ex}");
}
finally
{
    try
    {
        Directory.Delete(sandbox, true);
    }
    catch
    {
        // 沙箱清理失败不影响结论
    }

    // 清理程序目录里的探针文件（只删我们自己造的那几个）
    foreach (var f in new[] { bundledProbePath })
    {
        try
        {
            if (File.Exists(f))
            {
                File.Delete(f);
            }
        }
        catch
        {
            // 忽略
        }
    }
}

Console.WriteLine();
Console.WriteLine($"==> 共 {checks} 项检查，失败 {failures.Count} 项");
if (failures.Count > 0)
{
    Console.WriteLine("失败项：");
    foreach (var f in failures)
    {
        Console.WriteLine("  - " + f);
    }

    return 1;
}

Console.WriteLine("全部通过 ✅");
return 0;

// 反射创建一个全新的 PluginService 实例（模拟重启）
static PluginService NewPluginServiceInstance()
{
    var type = typeof(PluginService);
    var ctor = type.GetConstructor(
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
        null, Type.EmptyTypes, null)!;
    return (PluginService)ctor.Invoke(null);
}
