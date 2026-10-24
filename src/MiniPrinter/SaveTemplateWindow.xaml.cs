using System.Windows;
using System.Windows.Input;
using MiniPrinter.Localization;

namespace MiniPrinter;

public partial class SaveTemplateWindow : Window
{
    public string TemplateName => NameBox.Text.Trim();

    public SaveTemplateWindow(string? suggested = null)
    {
        InitializeComponent();
        Title = Loc.T("save.title");
        TitleText.Text = Loc.T("save.title");
        PromptText.Text = Loc.T("save.prompt");
        OkBtn.Content = Loc.T("save.ok");
        CancelBtn.Content = Loc.T("save.cancel");
        NameBox.Text = suggested ?? "";
        NameBox.SelectAll();
        Loaded += (_, _) => NameBox.Focus();
        NameBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Ok_Click(this, new RoutedEventArgs());
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TemplateName))
        {
            MessageBox.Show(this, Loc.Error(ErrorCode.TemplateNameEmpty), Loc.T("save.title"));
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
