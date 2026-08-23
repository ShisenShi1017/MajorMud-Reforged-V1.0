using System.Windows;

namespace Reforged.App.Views;

public partial class NvidiaApiKeyWindow : Window
{
    public NvidiaApiKeyWindow() => InitializeComponent();

    public string ApiKey => ApiKeyBox.Password;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ApiKeyBox.Password))
        {
            MessageBox.Show(this, "Enter an NVIDIA NIM API key.", "Borin NVIDIA NIM", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
