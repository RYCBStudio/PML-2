using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Plugin.Core;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels.PluginEditor;

/// <summary>
///     插件表单草稿（26.3.1 S6）。
///     表单编辑器的内存模型，保存时序列化为与运行时一致的 <c>RawPlugin</c> YAML；
///     打开编辑时由 YAML 反序列化回填。禁止另存一套 JSON 作为主存储。
/// </summary>
public class PluginDraft : ViewModelBase
{
    private string _id = "";
    private string _name = "";
    private string _description = "";
    private string _author = "";
    private string _version = "1.0";

    public string Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public string Description
    {
        get => _description;
        set => this.RaiseAndSetIfChanged(ref _description, value);
    }

    public string Author
    {
        get => _author;
        set => this.RaiseAndSetIfChanged(ref _author, value);
    }

    public string Version
    {
        get => _version;
        set => this.RaiseAndSetIfChanged(ref _version, value);
    }

    public ObservableCollection<TriggerDraft> Triggers { get; } = [];
}

public class TriggerDraft : ViewModelBase
{
    private PluginEventInfo? _selectedEvent;
    private string _on = "";
    private string? _condition;

    /// <summary>选中的事件注册表项（ComboBox SelectedItem 绑定；选择后同步 On）</summary>
    public PluginEventInfo? SelectedEvent
    {
        get => _selectedEvent;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedEvent, value);
            On = value?.Name ?? "";
        }
    }

    /// <summary>订阅的事件名（triggers.on，来自注册表）</summary>
    public string On
    {
        get => _on;
        set => this.RaiseAndSetIfChanged(ref _on, value);
    }

    /// <summary>条件表达式（可选，单条字符串）</summary>
    public string? Condition
    {
        get => _condition;
        set => this.RaiseAndSetIfChanged(ref _condition, value);
    }

    public ObservableCollection<ActionDraft> Actions { get; } = [];
}

public class ActionDraft : ViewModelBase
{
    private PluginActionInfo? _selectedAction;
    private string _name = "";

    /// <summary>选中的动作注册表项（ComboBox SelectedItem 绑定；选择后同步 Name 并重建参数表单）</summary>
    public PluginActionInfo? SelectedAction
    {
        get => _selectedAction;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedAction, value);
            Name = value?.Name ?? "";
            RebuildParams();
        }
    }

    /// <summary>动作名（actions.name，来自注册表）</summary>
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public ObservableCollection<ActionParamDraft> Params { get; } = [];

    /// <summary>按注册表重建参数表单（保留已填值）</summary>
    private void RebuildParams()
    {
        var info = _selectedAction;
        if (info == null)
        {
            Params.Clear();
            return;
        }

        var oldValues = Params.ToDictionary(p => p.Key, p => p.Value);
        Params.Clear();
        foreach (var param in info.Params)
        {
            Params.Add(new ActionParamDraft(param.Key, param.Label, param.Required)
            {
                Value = oldValues.GetValueOrDefault(param.Key) ?? ""
            });
        }
    }

    /// <summary>按参数键设置值（加载编辑时填充已有参数）</summary>
    public void SetParamValue(string key, string value)
    {
        var param = Params.FirstOrDefault(p => p.Key == key);
        if (param != null)
        {
            param.Value = value;
        }
    }
}

public class ActionParamDraft : ViewModelBase
{
    private string _value = "";

    public ActionParamDraft(string key, string label, bool required)
    {
        Key = key;
        Label = label;
        Required = required;
    }

    /// <summary>参数键（params 中的字段名）</summary>
    public string Key { get; }

    /// <summary>界面标签</summary>
    public string Label { get; }

    /// <summary>是否必填</summary>
    public bool Required { get; }

    /// <summary>参数值（表单输入）</summary>
    public string Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }
}
