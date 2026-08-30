using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Plugin.Core;
using MEFrpLauncherX.Plugin.Services;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels.PluginEditor;

/// <summary>
///     表单式插件编辑器 ViewModel（26.3.1 S6）。
///     数据流：表单 Draft → RawPlugin → YAML 字符串 → 校验 → 写入 Config/Plugins/{id}.yaml。
///     打开编辑：读 YAML → 反序列化 RawPlugin → 回填 Draft。
/// </summary>
public class PluginEditorViewModel : ViewModelBase
{
    private readonly PluginService _pluginService = PluginService.Instance;
    private string _previewYaml = "";
    private string? _validationMessage;

    public PluginEditorViewModel()
    {
        // 新建插件：默认一条空规则
        Draft.Triggers.Add(new TriggerDraft());
        InitCommands();
    }

    public PluginEditorViewModel(string yamlContent)
    {
        LoadFromYaml(yamlContent);
        InitCommands();
    }

    /// <summary>表单草稿</summary>
    public PluginDraft Draft { get; } = new();

    /// <summary>预览 YAML（只读）</summary>
    public string PreviewYaml
    {
        get => _previewYaml;
        set => this.RaiseAndSetIfChanged(ref _previewYaml, value);
    }

    /// <summary>校验/错误消息（保存失败原因）</summary>
    public string? ValidationMessage
    {
        get => _validationMessage;
        set => this.RaiseAndSetIfChanged(ref _validationMessage, value);
    }

    /// <summary>事件注册表（编辑器下拉只绑定此处，与运行时同源）</summary>
    public IReadOnlyList<PluginEventInfo> AvailableEvents => PluginCatalog.Events;

    /// <summary>动作注册表</summary>
    public IReadOnlyList<PluginActionInfo> AvailableActions => PluginCatalog.Actions;

    public ReactiveCommand<Unit, Unit> AddTriggerCommand
    {
        get;
        private set;
    }

    public ReactiveCommand<TriggerDraft, Unit> RemoveTriggerCommand
    {
        get;
        private set;
    }

    public ReactiveCommand<TriggerDraft, Unit> AddActionCommand
    {
        get;
        private set;
    }

    public ReactiveCommand<ActionDraft, Unit> RemoveActionCommand
    {
        get;
        private set;
    }

    public ReactiveCommand<Unit, Unit> RefreshPreviewCommand
    {
        get;
        private set;
    }

    public ReactiveCommand<Unit, Unit> SaveCommand
    {
        get;
        private set;
    }

    /// <summary>保存成功后请求关闭编辑器窗口</summary>
    public event Action? RequestClose;

    private void InitCommands()
    {
        AddTriggerCommand = ReactiveCommand.Create(() => Draft.Triggers.Add(new TriggerDraft()));
        RemoveTriggerCommand = ReactiveCommand.Create<TriggerDraft>(t => Draft.Triggers.Remove(t));
        AddActionCommand = ReactiveCommand.Create<TriggerDraft>(t =>
        {
            var action = new ActionDraft
            {
                // 默认选中第一个动作并生成参数表单
                SelectedAction = PluginCatalog.Actions.FirstOrDefault()
            };
            t.Actions.Add(action);
        });
        RemoveActionCommand = ReactiveCommand.Create<ActionDraft>(a => { });
        RefreshPreviewCommand = ReactiveCommand.Create(RefreshPreview);
        SaveCommand = ReactiveCommand.Create(Save);
    }

    /// <summary>刷新预览 YAML（不校验，展示当前表单序列化结果）</summary>
    private void RefreshPreview()
    {
        try
        {
            var raw = BuildRawPlugin(out var error, strict: false);
            PreviewYaml = raw == null ? error ?? "" : _pluginService.SerializePluginYaml(raw);
        }
        catch (Exception ex)
        {
            PreviewYaml = $"序列化失败: {ex.Message}";
        }
    }

    /// <summary>表单 → RawPlugin；strict 时执行完整校验（保存用），非 strict 只做基础构建</summary>
    private RawPlugin? BuildRawPlugin(out string? error, bool strict)
    {
        error = null;
        var id = Draft.Id.Trim();
        var name = Draft.Name.Trim();
        if (strict)
        {
            if (string.IsNullOrEmpty(id))
            {
                error = "插件 ID 不能为空";
                return null;
            }

            var safeName = Path.GetFileName(id + ".yaml");
            if (safeName != id + ".yaml")
            {
                error = "插件 ID 不能包含路径分隔符等非法字符";
                return null;
            }

            if (string.IsNullOrEmpty(name))
            {
                error = "插件名称不能为空";
                return null;
            }

            if (Draft.Triggers.Count == 0)
            {
                error = "至少需要一个规则（trigger）";
                return null;
            }
        }

        var raw = new RawPlugin
        {
            Id = id,
            Name = name,
            Description = Draft.Description.Trim(),
            Author = Draft.Author.Trim(),
            Version = string.IsNullOrWhiteSpace(Draft.Version) ? "1.0" : Draft.Version.Trim()
        };

        foreach (var trigger in Draft.Triggers)
        {
            var on = trigger.On.Trim();
            if (strict && PluginCatalog.FindEvent(on) == null)
            {
                error = $"未知事件: {on}";
                return null;
            }

            var condition = string.IsNullOrWhiteSpace(trigger.Condition) ? null : trigger.Condition.Trim();
            if (strict && condition != null)
            {
                try
                {
                    Plugin.Condition.ConditionParser.Parse(condition);
                }
                catch (Exception ex)
                {
                    error = $"条件表达式语法错误: {ex.Message}";
                    return null;
                }
            }

            var triggerDef = new TriggerDefinition
            {
                On = on,
                Condition = condition
            };
            foreach (var action in trigger.Actions)
            {
                var actionName = action.Name.Trim();
                if (strict && PluginCatalog.FindAction(actionName) == null)
                {
                    error = $"未知动作: {actionName}";
                    return null;
                }

                var actionDef = new ActionDefinition
                {
                    Name = actionName,
                    Params = new Dictionary<string, object>()
                };
                foreach (var param in action.Params)
                {
                    if (strict && param.Required && string.IsNullOrWhiteSpace(param.Value))
                    {
                        error = $"动作 {actionName} 缺少必填参数 {param.Label}";
                        return null;
                    }

                    if (!string.IsNullOrWhiteSpace(param.Value))
                    {
                        actionDef.Params[param.Key] = param.Value.Trim();
                    }
                }

                triggerDef.Actions.Add(actionDef);
            }

            raw.Triggers.Add(triggerDef);
        }

        return raw;
    }

    /// <summary>保存：构建 → 序列化 → 反序列化校验 → 一次性写入 Config/Plugins</summary>
    private void Save()
    {
        var raw = BuildRawPlugin(out var error, strict: true);
        if (raw == null)
        {
            ValidationMessage = error;
            return;
        }

        string yaml;
        try
        {
            yaml = _pluginService.SerializePluginYaml(raw);
        }
        catch (Exception ex)
        {
            ValidationMessage = $"序列化失败: {ex.Message}";
            return;
        }

        // 校验失败不写盘：写入前用运行时同一模型反序列化一遍
        try
        {
            _pluginService.DeserializePluginYaml(yaml);
        }
        catch (Exception ex)
        {
            ValidationMessage = $"YAML 校验失败: {ex.Message}";
            return;
        }

        if (!_pluginService.SavePluginContent($"{raw.Id}.yaml", yaml, out var saveError))
        {
            ValidationMessage = $"保存失败: {saveError}";
            return;
        }

        ValidationMessage = null;
        RequestClose?.Invoke();
    }

    /// <summary>打开编辑：YAML → RawPlugin → 回填表单；失败抛异常由调用方提示</summary>
    private void LoadFromYaml(string yamlContent)
    {
        var raw = _pluginService.DeserializePluginYaml(yamlContent);
        Draft.Id = raw.Id ?? "";
        Draft.Name = raw.Name ?? "";
        Draft.Description = raw.Description ?? "";
        Draft.Author = raw.Author ?? "";
        Draft.Version = string.IsNullOrWhiteSpace(raw.Version) ? "1.0" : raw.Version;

        foreach (var trigger in raw.Triggers)
        {
            var triggerDraft = new TriggerDraft
            {
                // 通过注册表项同步 On（未知事件则保留原字符串）
                SelectedEvent = PluginCatalog.FindEvent(trigger.On ?? ""),
                Condition = trigger.Condition
            };
            if (triggerDraft.SelectedEvent == null)
            {
                triggerDraft.On = trigger.On ?? "";
            }

            foreach (var action in trigger.Actions)
            {
                var actionDraft = new ActionDraft();
                var info = PluginCatalog.FindAction(action.Name ?? "");
                if (info != null)
                {
                    actionDraft.SelectedAction = info;
                    foreach (var param in info.Params)
                    {
                        actionDraft.SetParamValue(param.Key, action.Params.GetValueOrDefault(param.Key)?.ToString() ?? "");
                    }
                }
                else
                {
                    // 未知动作（手写 YAML 可能包含表单暂不支持的动作）：兜底展示参数键
                    actionDraft.Name = action.Name ?? "";
                    foreach (var kv in action.Params)
                    {
                        actionDraft.Params.Add(new ActionParamDraft(kv.Key, kv.Key, false)
                        {
                            Value = kv.Value?.ToString() ?? ""
                        });
                    }
                }

                triggerDraft.Actions.Add(actionDraft);
            }

            Draft.Triggers.Add(triggerDraft);
        }
    }
}
