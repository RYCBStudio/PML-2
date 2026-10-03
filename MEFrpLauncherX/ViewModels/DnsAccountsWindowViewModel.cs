using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Layout;
using FluentAvalonia.MarkdownRender.Helper;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.Models;
using MEFrpLauncherX.Core.Services;
using ReactiveUI;
// ReactiveUI 25：无参 ReactiveCommand 的载体类型 RxVoid（替代 System.Reactive.Unit）位于此命名空间。
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace MEFrpLauncherX.ViewModels;

/// <summary>
///     DNS 账户管理窗口的 ViewModel（26.4 阶段 B）。
///     表单为<b>表驱动</b>：服务商字段来自 <see cref="DnsProviders" />，
///     切换厂商时由 <see cref="RebuildFields" /> 重建字段集合，新增厂商无需改动界面代码。
///     凭据只在保存时交给 <see cref="DnsAccountStore" /> 加密落盘，界面从不回显明文
///     （已有账户仅把 <see cref="SecretRedactor.Mask" /> 的掩码写进水印，留空表示沿用原值）。
/// </summary>
public class DnsAccountsWindowViewModel : ViewModelBase
{
    /// <summary>当前编辑的账户 Id（<see cref="Guid.Empty" /> 表示新建）</summary>
    private Guid _editingId = Guid.Empty;

    /// <summary>已选账户的明文凭据缓存（仅用于「留空沿用原值」，不参与展示）</summary>
    private Dictionary<string, string> _existingCredentials = new(StringComparer.Ordinal);

    private bool _isEmpty;
    private bool _isPermissionHintOpen = true;
    private string _permissionHint = string.Empty;
    private bool _isDocumentationEnabled;
    private string _accountName = string.Empty;
    private int _selectedProviderIndex;
    private DnsAccountSummary? _selectedAccount;

    /// <summary>
    ///     重载列表期间抑制「选中项变化」回调：
    ///     <see cref="Avalonia.Collections.AvaloniaList{T}" /> 的 Reset 会同步把 ListBox 的
    ///     SelectedItem 置空并回写到 <see cref="SelectedAccount" />，若不抑制会把刚载入的表单清掉。
    /// </summary>
    private bool _reloading;

    public DnsAccountsWindowViewModel()
    {
        AddCommand = ReactiveCommand.Create(AddAccount);
        DeleteCommand = ReactiveCommand.CreateFromTask(DeleteAccountAsync);
        SaveCommand = ReactiveCommand.Create(SaveAccount);
        SubmitProviderCommand = ReactiveCommand.CreateFromTask(SubmitProviderAsync);
        OpenDocumentationCommand = ReactiveCommand.Create(OpenDocumentation);
        CloseCommand = ReactiveCommand.Create(() => RequestClose?.Invoke());

        RebuildFields();
        ReloadAccounts();
    }

    /// <summary>左侧账户列表（<b>不含凭据</b>）</summary>
    public AvaloniaList<DnsAccountSummary> Accounts { get; } = [];

    /// <summary>右侧动态字段集合（随服务商变化整体重建）</summary>
    public AvaloniaList<DnsFieldViewModel> Fields { get; } = [];

    /// <summary>服务商下拉项（顺序与 <see cref="DnsProviders.All" /> 一致）</summary>
    public IReadOnlyList<string> ProviderNames { get; } = [.. DnsProviders.All.Select(p => p.DisplayName)];

    /// <summary>列表为空提示的可见性</summary>
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => this.RaiseAndSetIfChanged(ref _isEmpty, value);
    }

    /// <summary>当前服务商最小权限提示文案</summary>
    public string PermissionHint
    {
        get => _permissionHint;
        private set => this.RaiseAndSetIfChanged(ref _permissionHint, value);
    }

    /// <summary>是否有最小权限提示可展示</summary>
    public bool IsPermissionHintOpen
    {
        get => _isPermissionHintOpen;
        private set => this.RaiseAndSetIfChanged(ref _isPermissionHintOpen, value);
    }

    /// <summary>文档按钮可用性（未解析到服务商时禁用）</summary>
    public bool IsDocumentationEnabled
    {
        get => _isDocumentationEnabled;
        private set => this.RaiseAndSetIfChanged(ref _isDocumentationEnabled, value);
    }

    /// <summary>账户备注名（表单输入）</summary>
    public string AccountName
    {
        get => _accountName;
        set => this.RaiseAndSetIfChanged(ref _accountName, value);
    }
    /// <summary>服务商下拉选中索引（供 ComboBox SelectedIndex 绑定）</summary>
    public int SelectedProviderIndex
    {
        get => _selectedProviderIndex;
        set
        {
            var previous = _selectedProviderIndex;
            var index = value >= 0 && value < DnsProviders.All.Count ? value : 0;
            if (index == previous)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedProviderIndex, index);

            // 服务商变更：重建字段，避免把上一家的字段值误存给下一家
            RebuildFields();
        }
    }

    /// <summary>表单校验错误文案（为空时隐藏）</summary>
    public string? ValidationMessage
    {
        get;
        private set
        {
            var previous = field;
            if (string.Equals(previous, value, StringComparison.Ordinal))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(HasValidation));
        }
    }

    /// <summary>是否有校验错误需要展示</summary>
    public bool HasValidation => !string.IsNullOrWhiteSpace(ValidationMessage);

    /// <summary>左侧选中的账户摘要；为 null 表示「新建」</summary>
    public DnsAccountSummary? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (_reloading || ReferenceEquals(_selectedAccount, value))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedAccount, value);
            this.RaisePropertyChanged(nameof(IsDeleteEnabled));
            if (value is not null)
            {
                ApplyAccount(value);
            }
        }
    }

    /// <summary>删除按钮可用性（未选中账户时禁用）</summary>
    public bool IsDeleteEnabled => _selectedAccount is not null;

    /// <summary>新增账户</summary>
    public ReactiveCommand<RxVoid, RxVoid> AddCommand { get; }

    /// <summary>删除所选账户（需二次确认）</summary>
    public ReactiveCommand<RxVoid, RxVoid> DeleteCommand { get; }

    /// <summary>保存账户（校验必填项后交给加密存储）</summary>
    public ReactiveCommand<RxVoid, RxVoid> SaveCommand { get; }

    /// <summary>向官方提交未收录的 DNS 服务商</summary>
    public ReactiveCommand<RxVoid, RxVoid> SubmitProviderCommand { get; }

    /// <summary>打开当前服务商的最小权限文档</summary>
    public ReactiveCommand<RxVoid, RxVoid> OpenDocumentationCommand { get; }

    /// <summary>关闭窗口</summary>
    public ReactiveCommand<RxVoid, RxVoid> CloseCommand { get; }

    /// <summary>请求关闭窗口（由视图订阅后调用 <c>Close()</c>）</summary>
    public event Action? RequestClose;

    /// <summary>请求把焦点移到账户名输入框（新建账户后调用）</summary>
    public event Action? RequestFocusName;

    /// <summary>当前选中的服务商描述；未选择时返回 null。</summary>
    private DnsProviderDescriptor? SelectedProvider =>
        _selectedProviderIndex >= 0 && _selectedProviderIndex < DnsProviders.All.Count
            ? DnsProviders.All[_selectedProviderIndex]
            : null;

    /// <summary>重新加载左侧账户列表（不含凭据），尽量保持原选择。</summary>
    private void ReloadAccounts()
    {
        var preferredId = _selectedAccount?.Id ?? Guid.Empty;
        var items = DnsAccountStore.List();

        _reloading = true;
        try
        {
            Accounts.Clear();
            Accounts.AddRange(items);
            _selectedAccount = null;
        }
        finally
        {
            _reloading = false;
        }

        this.RaisePropertyChanged(nameof(SelectedAccount));
        this.RaisePropertyChanged(nameof(IsDeleteEnabled));
        IsEmpty = items.Count == 0;

        if (items.Count == 0)
        {
            ResetForm();
            return;
        }

        // 尽量保持原选择，否则选中第一项
        var target = items.FirstOrDefault(i => i.Id == preferredId) ?? items[0];
        _selectedAccount = target;
        this.RaisePropertyChanged(nameof(SelectedAccount));
        this.RaisePropertyChanged(nameof(IsDeleteEnabled));
        ApplyAccount(target);
    }

    /// <summary>清空表单为「新建」状态。</summary>
    private void ResetForm()
    {
        _editingId = Guid.Empty;
        _existingCredentials.Clear();
        AccountName = string.Empty;
        ValidationMessage = null;

        SetSelectedProviderIndex(0);
    }

    /// <summary>按当前服务商重建字段集合（表驱动核心）。</summary>
    private void RebuildFields()
    {
        Fields.Clear();

        var descriptor = SelectedProvider;
        if (descriptor is null)
        {
            PermissionHint = string.Empty;
            IsPermissionHintOpen = false;
            IsDocumentationEnabled = false;
            return;
        }

        // 最小权限提示
        var hint = ResolveText(descriptor.PermissionHintKey);
        PermissionHint = hint;
        IsPermissionHintOpen = !string.IsNullOrWhiteSpace(hint);
        IsDocumentationEnabled = true;

        foreach (var field in descriptor.Fields)
        {
            Fields.Add(new DnsFieldViewModel(field, FieldLabel(field)));
        }
    }

    /// <summary>切换服务商并重建字段（不触发 setter 二次重建）。</summary>
    private void SetSelectedProviderIndex(int index)
    {
        var safeIndex = index >= 0 && index < DnsProviders.All.Count ? index : 0;
        if (_selectedProviderIndex != safeIndex)
        {
            _selectedProviderIndex = safeIndex;
            this.RaisePropertyChanged(nameof(SelectedProviderIndex));
        }

        RebuildFields();
    }

    /// <summary>载入所选账户的元数据与字段水印（不回显明文凭据）。</summary>
    private void ApplyAccount(DnsAccountSummary summary)
    {
        _editingId = summary.Id;
        AccountName = summary.DisplayName;
        ValidationMessage = null;

        var index = 0;
        for (var i = 0; i < DnsProviders.All.Count; i++)
        {
            if (string.Equals(DnsProviders.All[i].Id, summary.Provider, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        SetSelectedProviderIndex(index);

        // 已有账户不回填明文凭据，仅在输入框给出掩码提示；留空表示保持原值不修改
        _existingCredentials.Clear();
        var entry = DnsAccountStore.Get(summary.Id);
        if (entry is null || DnsProviders.Resolve(entry.Meta.Provider) is null)
        {
            return;
        }

        _existingCredentials = new Dictionary<string, string>(entry.Credentials, StringComparer.Ordinal);

        foreach (var field in Fields)
        {
            if (entry.Credentials.TryGetValue(field.Key, out var value) && !string.IsNullOrEmpty(value))
            {
                field.Placeholder = SecretRedactor.Mask(value);
            }
        }
    }

    /// <summary>新建账户：清空选择与表单。</summary>
    private void AddAccount()
    {
        _reloading = true;
        try
        {
            _selectedAccount = null;
        }
        finally
        {
            _reloading = false;
        }

        this.RaisePropertyChanged(nameof(SelectedAccount));
        this.RaisePropertyChanged(nameof(IsDeleteEnabled));
        ResetForm();
        RequestFocusName?.Invoke();
    }

    /// <summary>删除所选账户（需二次确认）。</summary>
    private async Task DeleteAccountAsync()
    {
        if (_selectedAccount is not { } summary)
        {
            return;
        }

        var confirm = new FAContentDialog
        {
            Title = Languages.Text_Dns_DeleteConfirmTitle,
            Content = $"{summary.DisplayName}（{summary.ProviderDisplayName}）\n\n{Languages.Text_Dns_DeleteConfirm}",
            PrimaryButtonText = Languages.Text_Dns_Delete,
            CloseButtonText = Languages.Text_Global_Cancel,
            DefaultButton = FAContentDialogButton.Close
        };
        if (await confirm.ShowAsync() != FAContentDialogResult.Primary)
        {
            return;
        }

        if (DnsAccountStore.Delete(summary.Id))
        {
            Growl.Success(Languages.Text_Dns_Deleted);
            ReloadAccounts();
        }
        else
        {
            Growl.Error(Languages.Text_Dns_SaveFailed);
        }
    }

    /// <summary>保存账户：校验必填项后交给加密存储。</summary>
    private void SaveAccount()
    {
        ValidationMessage = null;

        var descriptor = SelectedProvider;
        if (descriptor is null)
        {
            ShowValidation(Languages.Text_Dns_Validation_ProviderRequired);
            return;
        }

        var displayName = AccountName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            ShowValidation(Languages.Text_Dns_Validation_NameRequired);
            return;
        }

        var credentials = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in Fields)
        {
            var value = field.Value?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                // 编辑已有账户时，留空的字段沿用原值（避免「只改名称」把凭据清空）
                if (_existingCredentials.TryGetValue(field.Key, out var previous) &&
                    !string.IsNullOrWhiteSpace(previous))
                {
                    credentials[field.Key] = previous;
                    continue;
                }

                if (field.Required)
                {
                    ShowValidation(string.Format(Languages.Text_Dns_Validation_FieldRequiredFormat, field.Label));
                    return;
                }

                continue;
            }

            credentials[field.Key] = value;
        }

        var meta = new DnsAccount
        {
            Id = _editingId,
            DisplayName = displayName,
            Provider = descriptor.Id
        };

        if (!DnsAccountStore.Save(meta, credentials))
        {
            ShowValidation(Languages.Text_Dns_SaveFailed);
            Growl.Error(Languages.Text_Dns_SaveFailed);
            return;
        }

        Growl.Success(Languages.Text_Dns_Saved);
        ReloadAccounts();
    }

    /// <summary>打开当前服务商的最小权限文档。</summary>
    private void OpenDocumentation()
    {
        var descriptor = SelectedProvider;
        if (descriptor is not null)
        {
            UrlHelper.OpenUrl(descriptor.DocumentationUrl);
        }
    }

    /// <summary>向官方提交未收录的 DNS 服务商。</summary>
    private async Task SubmitProviderAsync()
    {
        var panel = new StackPanel
        {
            Spacing = 5
        };
        panel.Children.Add(new TextBlock
        {
            Text = Languages.Text_Dns_SubmitDnsProvider_Tip
        });
        var box = new TextBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        panel.Children.Add(box);

        var cd = new FAContentDialog
        {
            Title = Languages.Text_Dns_SubmitProvider,
            Content = panel,
            PrimaryButtonText = Languages.Text_Global_Confirm,
            CloseButtonText = Languages.Text_Global_Cancel,
            DefaultButton = FAContentDialogButton.Primary
        };

        if (await cd.ShowAsync() != FAContentDialogResult.Primary)
        {
            return;
        }

        var provider = box.Text?.Trim();
        if (string.IsNullOrWhiteSpace(provider))
        {
            return;
        }

        await RYCBApiConverter.SendEmailAsync("html", "rycbqyf@163.com", provider, "DNS Provider 提交");
    }

    /// <summary>显示表单校验错误。</summary>
    private void ShowValidation(string message)
    {
        ValidationMessage = message;
    }

    /// <summary>字段标签：优先取资源键对应文案，缺失时回退到键名。</summary>
    private static string FieldLabel(DnsProviderField field)
    {
        var text = ResolveText(field.LabelKey);
        return string.IsNullOrWhiteSpace(text) ? field.Key : text;
    }

    /// <summary>按资源键取本地化文案（资源缺失或为空时返回空串）。</summary>
    internal static string ResolveText(string? resourceKey)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return string.Empty;
        }

        try
        {
            return Languages.ResourceManager.GetString(resourceKey, Languages.Culture) ?? string.Empty;
        }
        catch (Exception ex)
        {
            // 注意：本文件位于 MEFrpLauncherX.ViewModels 命名空间，直接写 App 会解析到
            // MEFrpLauncherX.App（Avalonia Application），因此显式写成 Core.App。
            Core.App.CurrentLogger?.Warning($"读取本地化文案失败（{resourceKey}）：{ex.Message}");
            return string.Empty;
        }
    }
}

/// <summary>
///     DNS 表单中的单个动态字段（表驱动）。
///     敏感字段以密码样式展示；已有账户仅显示 <see cref="SecretRedactor.Mask" /> 掩码水印，从不回填明文。
/// </summary>
public sealed class DnsFieldViewModel : ViewModelBase
{
    private string _value = string.Empty;
    private string _placeholder;

    public DnsFieldViewModel(DnsProviderField field, string label)
    {
        Key = field.Key;
        Label = label;
        Required = field.Required;
        IsSecret = field.IsSecret;
        PasswordChar = field.IsSecret ? '●' : '\0';
        _placeholder = DnsAccountsWindowViewModel.ResolveText(field.PlaceholderKey);
    }

    /// <summary>凭据字典中的键名（加密负载中的字段名）</summary>
    public string Key { get; }

    /// <summary>界面标签</summary>
    public string Label { get; }

    /// <summary>是否必填</summary>
    public bool Required { get; }

    /// <summary>是否为敏感值（日志中一律脱敏，输入框使用密码样式）</summary>
    public bool IsSecret { get; }

    /// <summary>输入框的密码掩码字符（非敏感字段为 <c>'\0'</c>，即不掩码）</summary>
    public char PasswordChar { get; }

    /// <summary>用户输入（仅在保存时交给加密存储）</summary>
    public string Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }

    /// <summary>水印：默认取字段资源键，编辑已有账户时改为掩码文本</summary>
    public string Placeholder
    {
        get => _placeholder;
        set => this.RaiseAndSetIfChanged(ref _placeholder, value);
    }
}
