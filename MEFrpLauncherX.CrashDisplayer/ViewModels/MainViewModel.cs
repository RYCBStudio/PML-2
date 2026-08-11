using System;
using System.Text;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace MEFrpLauncherX.CrashDisplayer.ViewModels;

public partial class MainViewModel
{
    public string JokeMessage { get; set; }
    public string ErrorSummary { get; set; }
    public string ErrorDetails { get; set; }
    public ICommand CopyCommand { get; set; }
    public ICommand CloseCommand { get; set; }

    public static string OopsCrashed => CrashStrings.OopsCrashed;
    public static string ProgrammerHumor => CrashStrings.ProgrammerHumor;
    public static string HumorNote => CrashStrings.HumorNote;
    public static string ErrorDetailsLabel => CrashStrings.ErrorDetailsLabel;
    public static string CopyErrorInfo => CrashStrings.CopyErrorInfo;
    public static string Close => CrashStrings.Close;

    public MainViewModel(string exJson, string crashLogEncrypted)
    {
        // 幽默消息
        var jokes = CrashStrings.Jokes;

        var random = new Random();
        JokeMessage = jokes[random.Next(jokes.Length)];
        var exInfo = Base64Decode(exJson).Split("||");
        var ex = new ExceptionInfo
        {
            Type = exInfo[0],
            Message = exInfo[1],
            StackTrace = exInfo[2]
        };
        if (ex.Type.Contains("QuicException")) Environment.Exit(0);
        var crashLog = Base64Decode(crashLogEncrypted);
        // 错误摘要
        ErrorSummary = $"{CrashStrings.ErrorTypeLabel}: {ex.Type}\n\n" +
                       $"{CrashStrings.ErrorMessageLabel}: {ex.Message}\n\n" +
                       $"{CrashStrings.SuggestionLabel}: {(ex.Message.Contains("内存")||ex.Message.Contains("Memory") ? CrashStrings.CheckAvailableMemory : CrashStrings.TryRestartProgram)}";

        // 错误详情
        ErrorDetails = crashLog;

        // 命令
        CopyCommand = new RelayCommand(_ =>
            (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow.Clipboard
            .SetTextAsync(crashLog));
        CloseCommand = new RelayCommand(_ =>
            (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).Shutdown());
    }
    
    public static string Base64Decode(string base64EncodedData)
    {
        var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
        return Encoding.UTF8.GetString(base64EncodedBytes);
    }
}

public class ExceptionInfo
{
    public string Type { get; set; }
    public string Message { get; set; }
    public string StackTrace { get; set; }

    public override string ToString()
    {
        return $"{Type}: {Message}{Environment.NewLine}{StackTrace}";
    }
}

public class RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
    : ICommand
{
    private readonly Action<object> _execute = execute ?? throw new ArgumentNullException(nameof(execute));

    public bool CanExecute(object parameter) => canExecute?.Invoke(parameter) ?? true;

    public void Execute(object parameter) => _execute(parameter);

    public event EventHandler CanExecuteChanged;
}