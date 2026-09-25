using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.Models;
using MEFrpLauncherX.Core.Services;
using MarkdownAIRender.Helper;

namespace MEFrpLauncherX.Views;

/// <summary>
///     DNS 账户管理窗口（26.4 阶段 B）。
///     表单为<b>表驱动</b>：服务商字段来自 <see cref="DnsProviders" />，
///     切换厂商时自动重建输入框，新增厂商无需改动本文件。
///     凭据只在保存时交给 <see cref="DnsAccountStore" /> 加密落盘，界面不回显明文。
/// </summary>
public partial class DnsAccountsWindow : Window
{
    /// <summary>动态生成字段对应的输入框（键为 <see cref="DnsProviderField.Key" />）</summary>
    private readonly Dictionary<string, TextBox> _fieldBoxes = new(StringComparer.Ordinal);

    /// <summary>当前编辑的账户 Id（<see cref="Guid.Empty" /> 表示新建）</summary>
    private Guid _editingId = Guid.Empty;

    public DnsAccountsWindow()
    {
        InitializeComponent();
        BuildProviderBox();
        ReloadAccounts();
    }

    /// <summary>填充服务商下拉（顺序与 <see cref="DnsProviders.All" /> 一致）。</summary>
    private void BuildProviderBox()
    {
        ProviderBox.ItemsSource = DnsProviders.All.Select(p => p.DisplayName).ToList();
        ProviderBox.SelectedIndex = 0;
    }

    /// <summary>重新加载左侧账户列表（不含凭据）。</summary>
    private void ReloadAccounts()
    {
        var items = DnsAccountStore.List();
        var selectedId = (AccountList.SelectedItem as DnsAccountSummary)?.Id;

        AccountList.ItemsSource = items;
        EmptyHint.IsVisible = items.Count == 0;
        DeleteButton.IsEnabled = false;

        if (items.Count == 0)
        {
            ResetForm();
            return;
        }

        // 尽量保持原选择，否则选中第一项
        var target = items.FirstOrDefault(i => i.Id == selectedId) ?? items[0];
        AccountList.SelectedItem = target;
    }

    /// <summary>清空表单为「新建」状态。</summary>
    private void ResetForm()
    {
        _editingId = Guid.Empty;
        NameBox.Text = string.Empty;
        ProviderBox.SelectedIndex = 0;
        RebuildFields();
    }

    /// <summary>按当前服务商重建字段输入框（表驱动核心）。</summary>
    private void RebuildFields()
    {
        _fieldBoxes.Clear();
        FieldsPanel.Children.Clear();

        var descriptor = DnsProviders.Resolve(GetSelectedProviderId());
        if (descriptor is null)
        {
            PermissionHint.IsOpen = false;
            DocButton.IsEnabled = false;
            return;
        }

        // 最小权限提示
        var hint = ResolveText(descriptor.PermissionHintKey);
        PermissionHint.Message = hint;
        PermissionHint.IsOpen = !string.IsNullOrWhiteSpace(hint);
        DocButton.IsEnabled = true;

        foreach (var field in descriptor.Fields)
        {
            var label = new TextBlock
            {
                Text = FieldLabel(field),
                Foreground = (IBrush?)this.FindResource("TextFillColorSecondaryBrush") ?? Brushes.Gray
            };

            var box = new TextBox
            {
                Name = $"Field_{field.Key}",
                Watermark = ResolveText(field.PlaceholderKey),
                PasswordChar = field.IsSecret ? '●' : '\0'
            };

            var panel = new StackPanel { Spacing = 4 };
            panel.Children.Add(label);
            panel.Children.Add(box);

            _fieldBoxes[field.Key] = box;
            FieldsPanel.Children.Add(panel);
        }
    }

    /// <summary>当前选中的服务商标识；未选择时返回 null。</summary>
    private string? GetSelectedProviderId()
    {
        var index = ProviderBox.SelectedIndex;
        return index >= 0 && index < DnsProviders.All.Count ? DnsProviders.All[index].Id : null;
    }

    /// <summary>服务商变更：重建字段（避免把上一家的字段值误存给下一家）。</summary>
    private void ProviderChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }

        RebuildFields();
    }

    /// <summary>左侧选择变化：载入该账户的元数据与字段标签（不回显明文凭据）。</summary>
    private void AccountSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (AccountList.SelectedItem is not DnsAccountSummary summary)
        {
            DeleteButton.IsEnabled = false;
            return;
        }

        DeleteButton.IsEnabled = true;
        _editingId = summary.Id;
        NameBox.Text = summary.DisplayName;

        var index = 0;
        for (var i = 0; i < DnsProviders.All.Count; i++)
        {
            if (string.Equals(DnsProviders.All[i].Id, summary.Provider, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        ProviderBox.SelectedIndex = index;
        RebuildFields();

        // 已有账户不回填明文凭据，仅在输入框给出掩码提示；留空表示保持原值不修改
        var entry = DnsAccountStore.Get(summary.Id);
        var descriptor = entry is null ? null : DnsProviders.Resolve(entry.Meta.Provider);
        if (entry is null || descriptor is null)
        {
            return;
        }

        foreach (var field in descriptor.Fields)
        {
            if (!_fieldBoxes.TryGetValue(field.Key, out var box))
            {
                continue;
            }

            if (entry.Credentials.TryGetValue(field.Key, out var value) && !string.IsNullOrEmpty(value))
            {
                box.Watermark = SecretRedactor.Mask(value);
            }
        }
    }

    /// <summary>新建账户：清空选择与表单。</summary>
    private void AddAccount(object? sender, RoutedEventArgs e)
    {
        AccountList.SelectedItem = null;
        DeleteButton.IsEnabled = false;
        ResetForm();
        NameBox.Focus();
    }

    /// <summary>删除所选账户（需二次确认）。</summary>
    private async void DeleteAccount(object? sender, RoutedEventArgs e)
    {
        if (AccountList.SelectedItem is not DnsAccountSummary summary)
        {
            return;
        }

        var confirm = new FluentAvalonia.UI.Controls.ContentDialog
        {
            Title = Languages.Text_Dns_DeleteConfirmTitle,
            Content = $"{summary.DisplayName}（{summary.ProviderDisplayName}）\n\n{Languages.Text_Dns_DeleteConfirm}",
            PrimaryButtonText = Languages.Text_Certificate_Delete,
            CloseButtonText = Languages.Text_Global_Cancel,
            DefaultButton = FluentAvalonia.UI.Controls.ContentDialogButton.Close
        };
        if (await confirm.ShowAsync() != FluentAvalonia.UI.Controls.ContentDialogResult.Primary)
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
    private void SaveAccount(object? sender, RoutedEventArgs e)
    {
        ValidationText.IsVisible = false;

        var providerId = GetSelectedProviderId();
        if (string.IsNullOrWhiteSpace(providerId))
        {
            ShowValidation(Languages.Text_Dns_Validation_ProviderRequired);
            return;
        }

        var displayName = NameBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            ShowValidation(Languages.Text_Dns_Validation_NameRequired);
            return;
        }

        var descriptor = DnsProviders.Resolve(providerId)!;
        var credentials = new Dictionary<string, string>(StringComparer.Ordinal);

        // 编辑已有账户时，留空的字段沿用原值（避免「只改名称」把凭据清空）
        var existing = _editingId == Guid.Empty ? null : DnsAccountStore.Get(_editingId);

        foreach (var field in descriptor.Fields)
        {
            if (!_fieldBoxes.TryGetValue(field.Key, out var box))
            {
                continue;
            }

            var value = box.Text?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                if (existing is not null &&
                    existing.Credentials.TryGetValue(field.Key, out var previous) &&
                    !string.IsNullOrWhiteSpace(previous))
                {
                    credentials[field.Key] = previous;
                    continue;
                }

                if (field.Required)
                {
                    ShowValidation(string.Format(Languages.Text_Dns_Validation_FieldRequiredFormat,
                        FieldLabel(field)));
                    return;
                }

                continue;
            }

            credentials[field.Key] = value;
        }

        var meta = new DnsAccount
        {
            Id = _editingId,
            DisplayName = displayName!,
            Provider = providerId
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
    private void OpenDocumentation(object? sender, RoutedEventArgs e)
    {
        var descriptor = DnsProviders.Resolve(GetSelectedProviderId());
        if (descriptor is not null)
        {
            UrlHelper.OpenUrl(descriptor.DocumentationUrl);
        }
    }

    private void CloseWindow(object? sender, RoutedEventArgs e) => Close();

    /// <summary>显示表单校验错误。</summary>
    private void ShowValidation(string message)
    {
        ValidationText.Text = message;
        ValidationText.IsVisible = true;
    }

    /// <summary>字段标签：优先取资源键对应文案，缺失时回退到键名。</summary>
    private static string FieldLabel(DnsProviderField field)
    {
        var text = ResolveText(field.LabelKey);
        return string.IsNullOrWhiteSpace(text) ? field.Key : text;
    }

    /// <summary>按资源键取本地化文案（资源缺失或为空时返回空串）。</summary>
    private static string ResolveText(string? resourceKey)
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
            Core.App.CurrentLogger?.Warning($"读取本地化文案失败（{resourceKey}）：{ex.Message}");
            return string.Empty;
        }
    }

    private async void SubmitProvider(object? sender, RoutedEventArgs e)
    {
        var panel = new StackPanel()
        {
            Spacing = 5,
            Orientation = Orientation.Horizontal
        };
        panel.Children.Add(new TextBlock()
        {
            Text = Languages.Text_Dns_SubmitDnsProvider_Tip
        });
        panel.Children.Add(new TextBox()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        });
        var cd = new ContentDialog()
        {
            Title = Languages.Text_Dns_SubmitProvider,
            Content = panel,
            PrimaryButtonText = Languages.Text_Global_Confirm,
            CloseButtonText = Languages.Text_Global_Cancel,
            DefaultButton = ContentDialogButton.Primary
        };
        var res = await cd.ShowAsync();
        if (res == ContentDialogResult.Primary)
        {
            var textBox = panel.Children[1] as TextBox;
            if (textBox is not null)
            {
                var provider = textBox.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(provider))
                {
                    await RYCBApiConverter.SendEmailAsync("html", "rycbqyf@163.com", provider, "DNS Provider 提交");
                }
            }
        }
    }
}