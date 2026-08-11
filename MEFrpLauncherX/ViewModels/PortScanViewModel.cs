// PortScannerViewModel.cs

using System;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Data.Converters;
using Avalonia.Threading;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Languages;
using ReactiveUI;
using RYCB.PML2.Extensions.MinecraftExtension;

namespace MEFrpLauncherX.ViewModels;

public class PortScannerViewModel : ViewModelBase
{
    private readonly PortScannerService _portScanner;
    private CancellationTokenSource _cancellationTokenSource;

    public PortScannerViewModel()
    {
        _portScanner = new PortScannerService();

        // 初始化命令
        StartScanCommand = ReactiveCommand.CreateFromTask(StartScanAsync);
        StopScanCommand = ReactiveCommand.Create(StopScan);
        QuickScanCommand = ReactiveCommand.CreateFromTask(QuickScanAsync);
        UseLocalhostCommand = ReactiveCommand.Create<Unit>(Unit => TargetIp = "127.0.0.1");
        UseCommonPortsCommand = ReactiveCommand.Create(SetCommonPorts);
        ClearResultsCommand = ReactiveCommand.Create(ClearResults);
        CopySelectedPortCommand = ReactiveCommand.CreateFromTask(CopySelectedPortAsync);
        UsePortCommand = ReactiveCommand.Create<int>(UsePort);

        // 处理命令的可执行性
        StartScanCommand.IsExecuting.Subscribe(isExecuting =>
        {
            IsScanning = isExecuting;
            if (isExecuting)
            {
                StatusMessage = Languages.Text_PortScan_Scanning;
            }
        });
    }

    public string TargetIp
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "127.0.0.1";

    public string StartPort
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "25560";

    public string EndPort
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "25570";

    public bool IsScanning
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsNotScanning));
        }
    }

    public bool IsNotScanning => !IsScanning;

    public double ScanProgress
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string StatusMessage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = Languages.Text_PortScan_Ready;

    public AvaloniaList<PortScannerService.ScanResult> ScanResults
    {
        get;
    } = [];

    public bool HasResults => ScanResults.Any();
    public bool HasSelection => SelectedResult != null;

    public PortScannerService.ScanResult? SelectedResult
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(HasSelection));
        }
    }

    public int ResultCount => ScanResults.Count;

    // 命令
    public ReactiveCommand<Unit, Unit> StartScanCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> StopScanCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> QuickScanCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> UseLocalhostCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> UseCommonPortsCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> ClearResultsCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> CopySelectedPortCommand
    {
        get;
    }

    public ReactiveCommand<int, Unit> UsePortCommand
    {
        get;
    }

    private async Task StartScanAsync()
    {
        try
        {
            if (!ValidateInputs())
            {
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            ClearResults();

            var start = int.Parse(StartPort);
            var end = int.Parse(EndPort);
            var totalPorts = end - start + 1;
            var scannedPorts = 0;

            var ports = Enumerable.Range(start, totalPorts).ToList();

            // 分批扫描，避免阻塞UI
            var batchSize = 50;
            for (var i = 0; i < ports.Count; i += batchSize)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    break;
                }

                var batch = ports.Skip(i).Take(batchSize);
                var tasks = batch.Select(port =>
                    _portScanner.CheckPortAsync(TargetIp, port, 200, _cancellationTokenSource.Token));

                var results = await Task.WhenAll(tasks);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var result in results)
                    {
                        if (result.IsOpen)
                        {
                            ScanResults.Add(result);
                        }
                    }

                    scannedPorts += batchSize;
                    ScanProgress = (double)scannedPorts / totalPorts * 100;
                    StatusMessage = string.Format(Languages.Text_PortScan_ScannedProgressFormat, scannedPorts, totalPorts);
                });
            }

            StatusMessage = string.Format(Languages.Text_PortScan_ScanCompletedFormat, ScanResults.Count);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Languages.Text_PortScan_ScanCancelled;
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Languages.Text_PortScan_ScanErrorFormat, ex.Message);
        }
    }

    private async Task QuickScanAsync()
    {
        try
        {
            ClearResults();
            _cancellationTokenSource = new CancellationTokenSource();

            IsScanning = true;
            StatusMessage = Languages.Text_PortScan_QuickScanning;

            var results = await _portScanner.ScanCommonMCPortsAsync(TargetIp, _cancellationTokenSource.Token);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var result in results)
                {
                    ScanResults.Add(result);
                }

                StatusMessage = string.Format(Languages.Text_PortScan_QuickScanCompletedFormat, ScanResults.Count);
                IsScanning = false;
            });
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Languages.Text_PortScan_ScanCancelled;
            IsScanning = false;
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Languages.Text_PortScan_ScanErrorFormat, ex.Message);
            IsScanning = false;
        }
    }

    private void StopScan()
    {
        _cancellationTokenSource?.Cancel();
        StatusMessage = Languages.Text_PortScan_StoppingScan;
    }

    private void SetCommonPorts()
    {
        StartPort = "25560";
        EndPort = "25570";
    }

    private void ClearResults()
    {
        ScanResults.Clear();
        this.RaisePropertyChanged(nameof(HasResults));
        this.RaisePropertyChanged(nameof(ResultCount));
    }

    private async Task CopySelectedPortAsync()
    {
        if (SelectedResult != null)
        {
            await Core.App.MainWindow.Clipboard.SetTextAsync(SelectedResult.Port.ToString());
            StatusMessage = string.Format(Languages.Text_PortScan_PortCopiedFormat, SelectedResult.Port);
        }
    }

    private void UsePort(int port)
    {
        // 这里可以将端口传递给主界面的配置
        // 例如通过消息总线或事件
        MessageBus.Current.SendMessage(new PortSelectedMessage(port));
        StatusMessage = string.Format(Languages.Text_PortScan_PortSelectedFormat, port);
    }

    private bool ValidateInputs()
    {
        if (string.IsNullOrEmpty(TargetIp))
        {
            StatusMessage = Languages.Text_PortScan_EnterTargetIp;
            return false;
        }

        if (!int.TryParse(StartPort, out var start) ||
            !int.TryParse(EndPort, out var end))
        {
            StatusMessage = Languages.Text_PortScan_PortMustBeNumber;
            return false;
        }

        if (start < 1 || start > 65535 || end < 1 || end > 65535)
        {
            StatusMessage = Languages.Text_PortScan_PortRange;
            return false;
        }

        if (start > end)
        {
            StatusMessage = Languages.Text_PortScan_StartGreaterEnd;
            return false;
        }

        return true;
    }
}

// 消息类，用于传递选择的端口
public class PortSelectedMessage
{
    public PortSelectedMessage(int port)
    {
        Port = port;
    }

    public int Port
    {
        get;
    }
}

// 转换器（需要在App.axaml中注册）
public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b && b ? "#4CAF50" : "#F44336";

    public object ConvertBack(object value, Type targetType, object parameter,
        CultureInfo culture) =>
        throw new NotImplementedException();
}

public class BoolToStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b && b ? Languages.Text_PortScan_Open : Languages.Text_PortScan_Closed;

    public object ConvertBack(object value, Type targetType, object parameter,
        CultureInfo culture) =>
        throw new NotImplementedException();
}