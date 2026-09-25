using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace MarkdownAIRender.CodeRender;

/// <summary>
///     基于 AvaloniaEdit + TextMate 的代码块渲染器。
///     <list type="bullet">
///         <item>使用 <see cref="TextEditor" /> 渲染，具备滚动与文本选择能力；“复制”仍由外部按钮完成，行为不变。</item>
///         <item>语法高亮交由 TextMateSharp 内置语法处理，覆盖 TypeScript、Rust、C/C++、Java、Go、Python、JSONC、YAML、Dockerfile、PowerShell、Groovy 等数十种语言。</item>
///         <item>语言解析顺序：语言 ID → 别名归一化（如 <c>ts</c> / <c>rs</c> / <c>c++</c>）→ 文件扩展名；无法识别时退化为纯文本着色，不会抛异常。</item>
///         <item><see cref="RegistryOptions" /> 按主题缓存（其内部缓存已编译的 grammar），避免每个代码块都重新编译语法。</item>
///     </list>
/// </summary>
public static class CodeRender
{
    #region 静态资源

    /// <summary>
    ///     主题 → RegistryOptions 缓存。RegistryOptions 内部会缓存已编译的 grammar，
    ///     因此按主题复用可避免每个代码块都触发一次 grammar 编译（首次编译约 10~40ms）。
    /// </summary>
    private static readonly Dictionary<ThemeName, RegistryOptions> RegistryCache = new();

    /// <summary>代码块字体（缓存解析结果，避免每个代码块都查一次资源）。</summary>
    private static FontFamily? s_codeFontFamily;

    /// <summary>
    ///     Markdown 语言标识 → TextMate 语言 ID 的别名表。
    ///     TextMateSharp 只认语言 ID 或扩展名，像 <c>ts</c> / <c>rs</c> / <c>py</c> / <c>c++</c> 这类需要先归一化。
    /// </summary>
    private static readonly Dictionary<string, string> LanguageAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // 前端 / 脚本
        { "ts", "typescript" },
        { "typescript", "typescript" },
        { "tsx", "typescriptreact" },
        { "js", "javascript" },
        { "mjs", "javascript" },
        { "cjs", "javascript" },
        { "node", "javascript" },
        { "jsx", "javascriptreact" },
        { "htm", "html" },
        { "xhtml", "html" },
        { "vue", "html" },
        { "svelte", "html" },
        { "astro", "html" },
        { "css3", "css" },
        { "sass", "scss" },
        { "hbs", "handlebars" },

        // 系统级语言
        { "rs", "rust" },
        { "py", "python" },
        { "python3", "python" },
        { "cs", "csharp" },
        { "c#", "csharp" },
        { "golang", "go" },
        { "kt", "kotlin" },
        { "kts", "kotlin" },
        { "h", "cpp" },
        { "hpp", "cpp" },
        { "cc", "cpp" },
        { "cxx", "cpp" },
        { "c++", "cpp" },
        { "cu", "cuda-cpp" },
        { "cuda", "cuda-cpp" },
        { "objc", "objective-c" },
        { "objcpp", "objective-cpp" },
        { "mm", "objective-cpp" },
        { "vbnet", "vb" },
        { "fs", "fsharp" },
        { "as", "x86asm" },
        { "nasm", "x86asm" },

        // 配置 / 数据
        { "json5", "json" },
        { "jsonl", "json" },
        { "yml", "yaml" },
        { "toml", "ini" },
        { "conf", "ini" },
        { "cfg", "ini" },
        { "env", "ini" },
        { "docker", "dockerfile" },
        { "containerfile", "dockerfile" },
        { "md", "markdown" },
        { "mkd", "markdown" },
        { "xaml", "xml" },
        { "axaml", "xml" },
        { "svg", "xml" },
        { "cshtml", "razor" },

        // Shell / 终端
        { "sh", "shellscript" },
        { "bash", "shellscript" },
        { "zsh", "shellscript" },
        { "shell", "shellscript" },
        { "console", "shellscript" },
        { "cmd", "bat" },
        { "batch", "bat" },
        { "ps1", "powershell" },
        { "pwsh", "powershell" },
        { "ps", "powershell" },

        // 构建 / 其他
        { "make", "makefile" },
        { "cmake", "makefile" },
        { "cmakelists", "makefile" },
        { "gradle", "groovy" },
        { "rb", "ruby" },
        { "jl", "julia" },
        { "clj", "clojure" },
        { "cljs", "clojure" },
        { "pl", "perl" },
        { "patch", "diff" },
        { "tex", "latex" },
        { "styl", "css" },
        { "glsl", "hlsl" },
        { "vert", "hlsl" },
        { "frag", "hlsl" },
        { "shader", "shaderlab" },
        { "typ", "typst" },

        // 纯文本类：TextMate 里 text.log 是最接近“无高亮”的 grammar
        { "txt", "log" },
        { "text", "log" },
        { "plain", "log" },
        { "plaintext", "log" },
        { "output", "log" },
        { "terminal", "shellscript" }
    };

    /// <summary>
    ///     语言 ID → 扩展名：当语言 ID 与别名都解析不到 scope 时，用扩展名再兜底一次。
    /// </summary>
    private static readonly Dictionary<string, string> LanguageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        { "typescript", ".ts" },
        { "typescriptreact", ".tsx" },
        { "javascript", ".js" },
        { "javascriptreact", ".jsx" },
        { "rust", ".rs" },
        { "python", ".py" },
        { "csharp", ".cs" },
        { "go", ".go" },
        { "kotlin", ".kt" },
        { "swift", ".swift" },
        { "dart", ".dart" },
        { "ruby", ".rb" },
        { "php", ".php" },
        { "java", ".java" },
        { "groovy", ".groovy" },
        { "lua", ".lua" },
        { "json", ".json" },
        { "jsonc", ".jsonc" },
        { "yaml", ".yaml" },
        { "ini", ".ini" },
        { "toml", ".toml" },
        { "xml", ".xml" },
        { "html", ".html" },
        { "css", ".css" },
        { "scss", ".scss" },
        { "less", ".less" },
        { "markdown", ".md" },
        { "dockerfile", ".dockerfile" },
        { "makefile", ".mk" },
        { "shellscript", ".sh" },
        { "powershell", ".ps1" },
        { "bat", ".bat" },
        { "sql", ".sql" },
        { "diff", ".diff" },
        { "log", ".log" },
        { "latex", ".tex" },
        { "c", ".c" },
        { "cpp", ".cpp" },
        { "cuda-cpp", ".cu" },
        { "hlsl", ".hlsl" },
        { "shaderlab", ".shader" },
        { "objective-c", ".m" },
        { "objective-cpp", ".mm" },
        { "fsharp", ".fs" },
        { "vb", ".vb" },
        { "perl", ".pl" },
        { "clojure", ".clj" },
        { "julia", ".jl" },
        { "r", ".r" },
        { "pascal", ".pas" },
        { "typst", ".typ" },
        { "razor", ".razor" },
        { "x86asm", ".asm" },
        { "ignore", ".gitignore" },
        { "properties", ".properties" },
        { "asciidoc", ".adoc" },
        { "coffeescript", ".coffee" },
        { "handlebars", ".hbs" }
    };

    /// <summary>无法识别语言时使用的退避 grammar（text.log 等价于无高亮，且必然存在）。</summary>
    private const string FallbackScopeName = "text.log";

    #endregion

    #region 语言解析

    /// <summary>
    ///     把 Markdown 里写的语言标识归一化成 TextMate 可用的 scope 名。
    /// </summary>
    /// <param name="language">代码块标注，如 <c>ts</c>、<c>rust</c>、<c>c++</c>、<c>jsonc</c>，也兼容 <c>```csharp title=demo</c> 这类带附加信息的写法。</param>
    /// <param name="options">当前主题对应的 <see cref="RegistryOptions" />。</param>
    /// <returns>可用的 scope 名；无法识别时返回 <see langword="null" />。</returns>
    public static string? ResolveScopeName(string? language, RegistryOptions options)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        // Markdown 中常见写法："```csharp title=foo"、"{.ts}"、"ts,typescript"，只取第一个有效 token
        var token = language
            .Split([' ', '\t', '\r', '\n', ',', ';', '{', '}', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        token = token.TrimStart('.').Trim();

        // 1) 直接作为 TextMate 语言 ID
        var scope = options.GetScopeByLanguageId(token);
        if (!string.IsNullOrEmpty(scope))
        {
            return scope;
        }

        // 2) 别名归一化为标准语言 ID
        var languageId = token;
        if (LanguageAliases.TryGetValue(token, out var mapped))
        {
            languageId = mapped;
            scope = options.GetScopeByLanguageId(languageId);
            if (!string.IsNullOrEmpty(scope))
            {
                return scope;
            }
        }

        // 3) 当作文件扩展名
        scope = options.GetScopeByExtension("." + token);
        if (!string.IsNullOrEmpty(scope))
        {
            return scope;
        }

        // 4) 别名对应语言 ID 的扩展名
        if (LanguageExtensions.TryGetValue(languageId, out var extension))
        {
            scope = options.GetScopeByExtension(extension);
            if (!string.IsNullOrEmpty(scope))
            {
                return scope;
            }
        }

        return null;
    }

    #endregion

    #region 渲染

    /// <summary>
    ///     渲染代码块。
    /// </summary>
    /// <param name="code">代码内容。</param>
    /// <param name="language">代码块语言标识，可为空或无法识别（此时退化为纯文本）。</param>
    /// <param name="themeName">TextMate 主题（浅色用 <see cref="ThemeName.LightPlus" />，深色用 <see cref="ThemeName.DarkPlus" />）。</param>
    /// <returns>只读的 <see cref="TextEditor" /> 控件。</returns>
    public static Control Render(string? code, string? language, ThemeName themeName)
    {
        var options = GetOrCreateRegistryOptions(themeName);

        var editor = new TextEditor
        {
            Text = code ?? string.Empty,
            IsReadOnly = true,
            ShowLineNumbers = false,
            WordWrap = false, // 代码保持原样不折行，超出宽度时水平滚动
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = GetCodeFontFamily(),
            FontSize = 13,
            Padding = new Thickness(6, 4),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent
        };

        // 只读展示：隐藏输入光标
        editor.TextArea.Caret.CaretBrush = Brushes.Transparent;

        // 关闭编辑器特有的交互与装饰，让输出更像静态代码块
        editor.Options.EnableHyperlinks = false;
        editor.Options.EnableEmailHyperlinks = false;
        editor.Options.EnableTextDragDrop = false;
        editor.Options.EnableVirtualSpace = false;
        editor.Options.AllowScrollBelowDocument = false;
        editor.Options.HighlightCurrentLine = false;

        // TextMate 需要 TextView 就绪后才能安装，同时挂载/卸载成对处理其后台分词任务
        editor.AttachedToVisualTree += (_, _) => AttachTextMate(editor, options, language);
        editor.DetachedFromVisualTree += (_, _) => DetachTextMate(editor);

        return editor;
    }

    private static FontFamily GetCodeFontFamily()
    {
        if (s_codeFontFamily != null)
        {
            return s_codeFontFamily;
        }

        // 优先复用宿主应用提供的等宽字体资源，取不到时回退到通用等宽字体串
        if (Application.Current is { } app && app.TryGetResource("Jbm", out var value) && value is FontFamily fontFamily)
        {
            return s_codeFontFamily = fontFamily;
        }

        return s_codeFontFamily = FontFamily.Parse("Consolas, Cascadia Mono, Jetbrains Mono, Menlo, DejaVu Sans Mono, monospace");
    }

    #endregion

    #region TextMate 生命周期

    /// <summary>
    ///     控件进入视觉树时安装 TextMate：设置主题与 grammar。
    /// </summary>
    private static void AttachTextMate(TextEditor editor, RegistryOptions options, string? language)
    {
        // 控件可能被反复挂载，避免重复安装
        if (editor.Tag is TextMate.Installation)
        {
            return;
        }

        try
        {
            // 必须让安装器初始化当前文档对应的分词模型（initCurrentDocument: true），
            // 否则 TextMate 不会创建 TMModel，语法高亮将完全不生效。
            var installation = editor.InstallTextMate(options);
            installation.SetTheme(options.GetDefaultTheme());

            var scopeName = ResolveScopeName(language, options);
            installation.SetGrammar(string.IsNullOrEmpty(scopeName) ? FallbackScopeName : scopeName);

            editor.Tag = installation;
        }
        catch (Exception)
        {
            // 语法高亮失败不应影响代码展示：保持无高亮的只读文本
            editor.SyntaxHighlighting = null;
            editor.Tag = null;
        }
    }

    /// <summary>
    ///     控件离开视觉树时释放 TextMate，停止其后台分词任务。
    /// </summary>
    private static void DetachTextMate(TextEditor editor)
    {
        if (editor.Tag is not TextMate.Installation installation)
        {
            return;
        }

        editor.Tag = null;

        try
        {
            installation.Dispose();
        }
        catch (Exception)
        {
            // 释放失败不应阻塞渲染流程
        }
    }

    private static RegistryOptions GetOrCreateRegistryOptions(ThemeName themeName)
    {
        lock (RegistryCache)
        {
            if (!RegistryCache.TryGetValue(themeName, out var options))
            {
                options = new RegistryOptions(themeName);
                RegistryCache[themeName] = options;
            }

            return options;
        }
    }

    #endregion
}
