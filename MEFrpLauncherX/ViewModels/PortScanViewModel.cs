// PortScannerViewModel.cs

using System;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Threading;
using MEFrpLauncherX.Core;
using ReactiveUI;
using RYCB.PML2.Extensions.MinecraftExtension;

namespace MEFrpLauncherX.ViewModels
{
    public class PortScannerViewModel : ViewModelBase
    {
        private readonly PortScannerService _portScanner;
        private CancellationTokenSource _cancellationTokenSource;

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
        } = "就绪";

        public AvaloniaList<PortScannerService.ScanResult> ScanResults
        {
            get;
        }= [];

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
                    StatusMessage = "扫描中...";
            });
        }

        private async Task StartScanAsync()
        {
            try
            {
                if (!ValidateInputs()) return;

                _cancellationTokenSource = new CancellationTokenSource();
                ClearResults();

                int start = int.Parse(StartPort);
                int end = int.Parse(EndPort);
                int totalPorts = end - start + 1;
                int scannedPorts = 0;

                var ports = Enumerable.Range(start, totalPorts).ToList();

                // 分批扫描，避免阻塞UI
                int batchSize = 50;
                for (int i = 0; i < ports.Count; i += batchSize)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                        break;

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
                        StatusMessage = $"已扫描 {scannedPorts}/{totalPorts} 个端口";
                    });
                }

                StatusMessage = $"扫描完成，找到 {ScanResults.Count} 个开放端口";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "扫描已取消";
            }
            catch (Exception ex)
            {
                StatusMessage = $"扫描出错: {ex.Message}";
            }
        }

        private async Task QuickScanAsync()
        {
            try
            {
                ClearResults();
                _cancellationTokenSource = new CancellationTokenSource();

                IsScanning = true;
                StatusMessage = "快速扫描MC常用端口中...";

                var results = await _portScanner.ScanCommonMCPortsAsync(TargetIp, _cancellationTokenSource.Token);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var result in results)
                    {
                        ScanResults.Add(result);
                    }

                    StatusMessage = $"快速扫描完成，找到 {ScanResults.Count} 个可能的MC端口";
                    IsScanning = false;
                });
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "扫描已取消";
                IsScanning = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"扫描出错: {ex.Message}";
                IsScanning = false;
            }
        }

        private void StopScan()
        {
            _cancellationTokenSource?.Cancel();
            StatusMessage = "正在停止扫描...";
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
                StatusMessage = $"已复制端口 {SelectedResult.Port} 到剪贴板";
            }
        }

        private void UsePort(int port)
        {
            // 这里可以将端口传递给主界面的配置
            // 例如通过消息总线或事件
            MessageBus.Current.SendMessage(new PortSelectedMessage(port));
            StatusMessage = $"已选择端口 {port}";
        }

        private bool ValidateInputs()
        {
            if (string.IsNullOrEmpty(TargetIp))
            {
                StatusMessage = "请输入目标IP地址";
                return false;
            }

            if (!int.TryParse(StartPort, out int start) ||
                !int.TryParse(EndPort, out int end))
            {
                StatusMessage = "端口号必须为数字";
                return false;
            }

            if (start < 1 || start > 65535 || end < 1 || end > 65535)
            {
                StatusMessage = "端口号必须在1-65535之间";
                return false;
            }

            if (start > end)
            {
                StatusMessage = "起始端口不能大于结束端口";
                return false;
            }

            return true;
        }
    }

    // 消息类，用于传递选择的端口
    public class PortSelectedMessage
    {
        public int Port
        {
            get;
        }

        public PortSelectedMessage(int port)
        {
            Port = port;
        }
    }

    // 转换器（需要在App.axaml中注册）
    public class BoolToColorConverter : Avalonia.Data.Converters.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is bool b && b ? "#4CAF50" : "#F44336";
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToStatusConverter : Avalonia.Data.Converters.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is bool b && b ? "开放" : "关闭";
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}