using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MEFrpLauncherX.Views
{
    public class InputControl : UserControl
    {
        public string CaptchaResult
        {
            get; private set;
        }

        public InputControl(string prompt)
        {

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

            inputBox.TextChanged += (s, e) =>
            {
                CaptchaResult = inputBox.Text;
            };
            stackPanel.Children.Add(promptText);
            stackPanel.Children.Add(inputBox);

            Content = stackPanel;
        }
    }
}
