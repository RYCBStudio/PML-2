using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MEFrpLauncherX
{
    public class InputWindow : Window
    {
        public string CaptchaResult
        {
            get; private set;
        }

        public InputWindow(string prompt, string title = "验证码输入")
        {
            Title = title;
            Width = 350;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var stackPanel = new StackPanel
            {
                Margin = new Thickness(15)
            };

            var promptText = new SelectableTextBlock
            {
                Text = prompt,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15)
            };

            var inputBox = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 15)
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var cancelButton = new Button
            {
                Content = "取消",
                Width = 80,
                Margin = new Thickness(0, 0, 10, 0)
            };
            cancelButton.Click += (s, e) => Close("cancel");

            var confirmButton = new Button
            {
                Content = "确定",
                Width = 80,
                IsDefault = true
            };
            confirmButton.Click += (s, e) =>
            {
                CaptchaResult = inputBox.Text;
                Close(CaptchaResult);
            };
            
            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(confirmButton);

            stackPanel.Children.Add(promptText);
            stackPanel.Children.Add(inputBox);
            stackPanel.Children.Add(buttonPanel);

            Content = stackPanel;
        }
        
        public async new Task<string> ShowDialog(Window owner)
        {
            var result = await base.ShowDialog<string>(owner);
            return result;
        }
    }
}
