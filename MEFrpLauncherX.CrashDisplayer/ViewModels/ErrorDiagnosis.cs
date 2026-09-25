using System.Collections.Generic;

namespace MEFrpLauncherX.CrashDisplayer.ViewModels;

/// <summary>解决方案项可执行的动作。</summary>
public enum SolutionAction
{
    None,
    OpenLogDirectory,
    OpenConfigDirectory,
    RestartApplication,
    ReportIssue
}

/// <summary>一条解决方案建议：文本步骤 + 可选的动作按钮。</summary>
public sealed record SolutionItem(string Text, SolutionAction Action = SolutionAction.None)
{
    public bool HasAction => Action != SolutionAction.None;

    public string ActionLabel => Action switch
    {
        SolutionAction.OpenLogDirectory => CrashStrings.OpenLogDirectory,
        SolutionAction.OpenConfigDirectory => CrashStrings.OpenConfigDirectory,
        SolutionAction.RestartApplication => CrashStrings.RestartApplication,
        SolutionAction.ReportIssue => CrashStrings.ReportIssue,
        _ => ""
    };
}

/// <summary>一次智能诊断的结果：明确的错误原因说明 + 分步解决方案。</summary>
public sealed record ErrorDiagnosis(string Icon, string Title, string Description, IReadOnlyList<SolutionItem> Solutions);

/// <summary>
///     智能化错误诊断引擎：按异常类型与错误消息中的已知模式匹配，
///     将原始异常翻译为用户可理解的「错误原因」说明，并给出针对性的解决步骤。
///     无法识别时回退为通用建议（重启 / 查日志 / 反馈）。
/// </summary>
public static class ErrorDiagnosisEngine
{
    private static string Lang => CrashStrings.CurrentLanguage;

    public static ErrorDiagnosis Diagnose(string? exceptionType, string? exceptionMessage)
    {
        var type = exceptionType ?? "";
        var message = exceptionMessage ?? "";
        var combined = $"{type} {message}".ToLowerInvariant();

        ErrorDiagnosis? diagnosis =
            MatchMemory(type, combined) ??
            MatchComponent(type, combined) ??
            MatchRendering(type, combined) ??
            MatchAccessViolation(type, combined) ??
            MatchPermission(type, combined) ??
            MatchFileLock(type, combined) ??
            MatchConfig(type, combined) ??
            MatchNetwork(type, combined) ??
            MatchCancelled(type, combined);

        return diagnosis ?? Fallback();
    }

    /// <summary>所有诊断通用的收尾建议：查日志（可执行动作）。</summary>
    private static SolutionItem OpenLogStep => new(CrashStrings.GenericOpenLogHint, SolutionAction.OpenLogDirectory);

    private static ErrorDiagnosis? MatchMemory(string type, string combined)
    {
        if (!type.Contains("OutOfMemoryException") && !combined.Contains("out of memory") &&
            !combined.Contains("内存不足") && !combined.Contains("memory"))
        {
            return null;
        }

        return new ErrorDiagnosis("🧠", T("内存不足", "記憶體不足", "Out of memory"),
            T("系统可用内存耗尽，程序无法继续运行。这通常发生在长时间运行、打开过多隧道或系统负载过高时。",
                "系統可用記憶體耗盡，程式無法繼續執行。這通常發生在長時間執行、開啟過多隧道或系統負載過高時。",
                "The system ran out of available memory. This usually happens after long uptime, too many active tunnels, or heavy system load."),
            [
                new SolutionItem(T("关闭其他占用内存较大的程序（如浏览器多余标签页），然后重启本应用",
                    "關閉其他佔用記憶體較大的程式（如瀏覽器多餘分頁），然後重新啟動本應用程式",
                    "Close other memory-hungry programs (e.g. extra browser tabs), then restart this app")),
                new SolutionItem(T("适当增加系统的虚拟内存（页面文件）大小",
                    "適當增加系統的虛擬記憶體（分頁檔）大小",
                    "Increase the system page file (virtual memory) size")),
                new SolutionItem(T("若频繁出现，请减少同时运行的隧道数量并反馈该问题",
                    "若頻繁出現，請減少同時執行的隧道數量並回報該問題",
                    "If this happens often, reduce concurrent tunnels and report the issue"), SolutionAction.ReportIssue),
                OpenLogStep
            ]);
    }

    private static ErrorDiagnosis? MatchComponent(string type, string combined)
    {
        if (!type.Contains("FileNotFoundException") && !type.Contains("DllNotFoundException") &&
            !type.Contains("TypeLoadException") && !type.Contains("FileLoadException") &&
            !type.Contains("BadImageFormatException") && !type.Contains("MissingMethodException") &&
            !combined.Contains("could not load file or assembly") && !combined.Contains("未能加载文件或程序集"))
        {
            return null;
        }

        return new ErrorDiagnosis("🧩", T("程序组件缺失或损坏", "程式元件缺失或損毀", "Missing or corrupted program files"),
            T("程序运行所需的文件缺失、被删除或已损坏，常见原因是安装不完整、更新中断或杀毒软件误删。",
                "程式執行所需的檔案缺失、被刪除或已損毀，常見原因是安裝不完整、更新中斷或防毒軟體誤刪。",
                "Files required by the app are missing, deleted, or corrupted — usually caused by an incomplete install, an interrupted update, or antivirus quarantine."),
            [
                new SolutionItem(T("重新下载并覆盖安装最新版本（配置文件会保留）",
                    "重新下載並覆蓋安裝最新版本（設定檔會保留）",
                    "Download and reinstall the latest version (your settings are preserved)")),
                new SolutionItem(T("检查杀毒软件/安全软件的隔离区，将被误删的程序文件恢复并加入白名单",
                    "檢查防毒軟體/安全軟體的隔離區，將被誤刪的程式檔案還原並加入白名單",
                    "Check your antivirus quarantine and restore/whitelist the app files")),
                OpenLogStep
            ]);
    }

    private static ErrorDiagnosis? MatchRendering(string type, string combined)
    {
        if (!combined.Contains("skia") && !combined.Contains("vulkan") && !combined.Contains("opengl") &&
            !combined.Contains("directx") && !combined.Contains("direct3d") && !combined.Contains("angle") &&
            !combined.Contains("gpu") && !combined.Contains("render") && !combined.Contains("显卡") &&
            !combined.Contains("顯示卡") && !combined.Contains("graphic"))
        {
            return null;
        }

        return new ErrorDiagnosis("🎨", T("图形渲染异常", "圖形渲染異常", "Graphics rendering failure"),
            T("图形子系统（显卡驱动 / GPU 渲染管线）发生错误，通常与显卡驱动版本或当前渲染模式有关。",
                "圖形子系統（顯示卡驅動 / GPU 渲染管線）發生錯誤，通常與顯示卡驅動版本或目前的渲染模式有關。",
                "The graphics subsystem (GPU driver / rendering pipeline) failed — usually related to the driver version or the current rendering mode."),
            [
                new SolutionItem(T("更新显卡驱动到最新版本后重启应用",
                    "更新顯示卡驅動到最新版本後重新啟動應用程式",
                    "Update your graphics driver to the latest version, then restart the app")),
                new SolutionItem(T("打开配置目录，将 Render.json 中的 RenderingMode 改为 SOFTWARE（软件渲染）后重启",
                    "開啟設定目錄，將 Render.json 中的 RenderingMode 改為 SOFTWARE（軟體渲染）後重新啟動",
                    "Open the config folder and set RenderingMode to SOFTWARE in Render.json, then restart"),
                    SolutionAction.OpenConfigDirectory),
                new SolutionItem(CrashStrings.RestartSucceedsHint, SolutionAction.RestartApplication),
                OpenLogStep
            ]);
    }

    private static ErrorDiagnosis? MatchAccessViolation(string type, string combined)
    {
        if (!type.Contains("AccessViolationException") && !combined.Contains("access violation") &&
            !combined.Contains("0xc0000005"))
        {
            return null;
        }

        return new ErrorDiagnosis("💥", T("原生组件发生严重错误", "原生元件發生嚴重錯誤", "Native component fault"),
            T("程序调用的原生（非托管）组件发生内存访问冲突，常见于显卡驱动、终端组件或系统运行库问题。",
                "程式呼叫的原生（非受控）元件發生記憶體存取違規，常見於顯示卡驅動、終端元件或系統執行庫問題。",
                "A native (unmanaged) component hit an access violation — often related to GPU drivers, the terminal component, or system runtimes."),
            [
                new SolutionItem(T("更新显卡驱动与系统运行库（如 Visual C++ Redistributable）",
                    "更新顯示卡驅動與系統執行庫（如 Visual C++ Redistributable）",
                    "Update your graphics driver and system runtimes (e.g. Visual C++ Redistributable)")),
                new SolutionItem(T("重新安装本应用，排除程序文件损坏",
                    "重新安裝本應用程式，排除程式檔案損毀",
                    "Reinstall the app to rule out corrupted program files")),
                new SolutionItem(T("若问题持续，请复制错误信息并反馈（附崩溃日志）",
                    "若問題持續，請複製錯誤資訊並回報（附崩潰記錄）",
                    "If it persists, copy the error info and report it (attach the crash log)"), SolutionAction.ReportIssue),
                OpenLogStep
            ]);
    }

    private static ErrorDiagnosis? MatchPermission(string type, string combined)
    {
        if (!type.Contains("UnauthorizedAccessException") && !type.Contains("SecurityException") &&
            !combined.Contains("access is denied") && !combined.Contains("拒绝访问") && !combined.Contains("拒絕存取") &&
            !combined.Contains("permission denied"))
        {
            return null;
        }

        return new ErrorDiagnosis("🔒", T("访问权限不足", "存取權限不足", "Permission denied"),
            T("程序没有获得足够的文件/系统访问权限，常见原因是安装目录受保护或被杀毒软件拦截。",
                "程式沒有獲得足夠的檔案/系統存取權限，常見原因是安裝目錄受保護或被防毒軟體攔截。",
                "The app lacks sufficient file/system access rights — usually because the install directory is protected or antivirus is blocking it."),
            [
                new SolutionItem(T("尝试右键 →「以管理员身份运行」本应用",
                    "嘗試右鍵 →「以系統管理員身分執行」本應用程式",
                    "Try right-click → “Run as administrator”")),
                new SolutionItem(T("将程序安装/移动到非系统保护目录（避免 C:\\Program Files、桌面同步目录等）",
                    "將程式安裝/移動到非系統保護目錄（避免 C:\\Program Files、桌面同步目錄等）",
                    "Install/move the app to a non-protected directory (avoid C:\\Program Files, synced Desktop folders, etc.)")),
                new SolutionItem(T("检查杀毒软件是否拦截了程序的文件写入，并将程序目录加入白名单",
                    "檢查防毒軟體是否攔截了程式的檔案寫入，並將程式目錄加入白名單",
                    "Check whether antivirus blocked file writes and whitelist the app directory")),
                OpenLogStep
            ]);
    }

    private static ErrorDiagnosis? MatchFileLock(string type, string combined)
    {
        if (!type.Contains("IOException") ||
            (!combined.Contains("being used by another process") && !combined.Contains("另一个程序正在使用") &&
             !combined.Contains("lock") && !combined.Contains("佔用") && !combined.Contains("占用")))
        {
            return null;
        }

        return new ErrorDiagnosis("📁", T("文件被其他进程占用", "檔案被其他處理程序佔用", "File in use by another process"),
            T("程序需要读写的文件正被其他进程占用，常见原因是应用未完全退出或有多个实例在运行。",
                "程式需要讀寫的檔案正被其他處理程序佔用，常見原因是應用程式未完全結束或有多個執行個體在執行。",
                "A file the app needs is locked by another process — usually because a previous instance didn't exit cleanly or multiple instances are running."),
            [
                new SolutionItem(T("在任务管理器中结束所有残留的 PML 2 / MEFrpLauncherX 进程后重试",
                    "在工作管理員中結束所有殘留的 PML 2 / MEFrpLauncherX 處理程序後重試",
                    "End any leftover PML 2 / MEFrpLauncherX processes in Task Manager, then retry")),
                new SolutionItem(CrashStrings.RestartSucceedsHint, SolutionAction.RestartApplication),
                OpenLogStep
            ]);
    }

    private static ErrorDiagnosis? MatchConfig(string type, string combined)
    {
        if (!type.Contains("JsonException") && !type.Contains("FormatException") &&
            !type.Contains("InvalidDataException") && !combined.Contains("settings.json") &&
            !combined.Contains("配置文件") && !combined.Contains("設定檔") && !combined.Contains("config"))
        {
            return null;
        }

        return new ErrorDiagnosis("⚙️", T("配置文件损坏", "設定檔損毀", "Corrupted configuration"),
            T("配置文件内容损坏或格式不正确，可能是异常断电、磁盘错误或手动编辑失误导致。",
                "設定檔內容損毀或格式不正確，可能是異常斷電、磁碟錯誤或手動編輯失誤導致。",
                "The configuration file is corrupted or malformed — possibly caused by power loss, disk errors, or a manual editing mistake."),
            [
                new SolutionItem(T("打开配置目录，将 Settings.json 重命名或删除后重启应用（程序会自动生成默认配置）",
                    "開啟設定目錄，將 Settings.json 重新命名或刪除後重新啟動應用程式（程式會自動產生預設設定）",
                    "Open the config folder, rename or delete Settings.json, then restart (defaults are regenerated)"),
                    SolutionAction.OpenConfigDirectory),
                new SolutionItem(T("如果存在 Settings.json.corrupt-* 备份文件，可从中手动恢复你的个性化设置",
                    "如果存在 Settings.json.corrupt-* 備份檔案，可從中手動還原你的個人化設定",
                    "If a Settings.json.corrupt-* backup exists, you can manually restore your custom settings from it")),
                new SolutionItem(CrashStrings.RestartSucceedsHint, SolutionAction.RestartApplication)
            ]);
    }

    private static ErrorDiagnosis? MatchNetwork(string type, string combined)
    {
        if (!type.Contains("SocketException") && !type.Contains("HttpRequestException") &&
            !type.Contains("WebException") && !type.Contains("TimeoutException") &&
            !type.Contains("AuthenticationException") && !combined.Contains("network") &&
            !combined.Contains("socket") && !combined.Contains("timed out") && !combined.Contains("dns") &&
            !combined.Contains("unreachable") && !combined.Contains("connection refused") &&
            !combined.Contains("网络") && !combined.Contains("網路") && !combined.Contains("連線逾時") &&
            !combined.Contains("连接超时"))
        {
            return null;
        }

        return new ErrorDiagnosis("🌐", T("网络连接异常", "網路連線異常", "Network failure"),
            T("程序在访问网络时发生错误，常见原因是网络中断、代理/防火墙拦截或目标服务器不可达。",
                "程式在存取網路時發生錯誤，常見原因是網路中斷、代理/防火牆攔截或目標伺服器無法連線。",
                "A network operation failed — usually due to connectivity loss, proxy/firewall blocking, or an unreachable server."),
            [
                new SolutionItem(T("检查网络连接是否正常（尝试打开网页确认）",
                    "檢查網路連線是否正常（嘗試開啟網頁確認）",
                    "Check your internet connection (try opening a web page)")),
                new SolutionItem(T("检查系统代理 / VPN / 防火墙设置，确认未拦截本应用的网络访问",
                    "檢查系統代理 / VPN / 防火牆設定，確認未攔截本應用程式的網路存取",
                    "Check system proxy / VPN / firewall settings and make sure this app is not blocked")),
                new SolutionItem(T("稍后重试；若仅个别节点不可用，可尝试更换其他节点",
                    "稍後重試；若僅個別節點無法使用，可嘗試更換其他節點",
                    "Retry later; if only certain nodes fail, try switching to another node")),
                OpenLogStep
            ]);
    }

    private static ErrorDiagnosis? MatchCancelled(string type, string combined)
    {
        if (!type.Contains("OperationCanceledException") && !type.Contains("TaskCanceledException"))
        {
            return null;
        }

        return new ErrorDiagnosis("⏱️", T("操作超时或被取消", "操作逾時或被取消", "Operation timed out or was cancelled"),
            T("某个耗时操作未能在限定时间内完成（通常是网络请求），随后被中止。",
                "某個耗時操作未能在限定時間內完成（通常是網路請求），隨後被中止。",
                "A long-running operation (usually a network request) didn't finish in time and was aborted."),
            [
                new SolutionItem(CrashStrings.RestartSucceedsHint, SolutionAction.RestartApplication),
                new SolutionItem(T("若反复出现，请检查网络质量或稍后重试",
                    "若反覆出現，請檢查網路品質或稍後重試",
                    "If it keeps happening, check your network quality or retry later")),
                OpenLogStep
            ]);
    }

    private static ErrorDiagnosis Fallback() =>
        new("❓", T("未识别的内部错误", "未識別的內部錯誤", "Unrecognized internal error"),
            T("这是一个尚未收录的错误类型。别担心，错误详情已完整保存，按下面的步骤操作即可。",
                "這是一個尚未收錄的錯誤類型。別擔心，錯誤詳情已完整儲存，按下面的步驟操作即可。",
                "This is an error type we haven't catalogued yet. Don't worry — the full details have been saved; just follow the steps below."),
            [
                new SolutionItem(CrashStrings.RestartSucceedsHint, SolutionAction.RestartApplication),
                new SolutionItem(T("复制错误信息并反馈给开发团队，帮助我们改进",
                    "複製錯誤資訊並回報給開發團隊，協助我們改進",
                    "Copy the error info and report it to help us improve"), SolutionAction.ReportIssue),
                OpenLogStep
            ]);

    /// <summary>三语文案选择（cn / hant / en）。</summary>
    private static string T(string cn, string hant, string en) => Lang switch
    {
        "cn" => cn,
        "hant" => hant,
        _ => en
    };
}
