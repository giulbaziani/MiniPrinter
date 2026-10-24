using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using MiniPrinter.Bluetooth;
using MiniPrinter.Localization;
using MiniPrinter.Printing;
using MiniPrinter.Templates;

namespace MiniPrinter;

public partial class MainWindow : Window
{
    private readonly List<PrinterDeviceInfo> _allDevices = [];
    private readonly ObservableCollection<PrinterDeviceInfo> _devices = [];
    private readonly ObservableCollection<TemplateRow> _layouts = [];
    private readonly ObservableCollection<TemplateRow> _saved = [];
    private readonly DeviceScanner _scanner = new();
    private readonly AppSettings _settings;
    private IPrinterTransport? _transport;
    private string? _connectedName;
    private CancellationTokenSource? _scanCts;
    private bool _suppressUi;
    private string _lastSample = "";
    private string? _imagePath;

    public MainWindow()
    {
        _settings = AppSettings.Load();
        Loc.Set(_settings.Language);
        InitializeComponent();
        DeviceList.ItemsSource = _devices;
        DeviceList.DisplayMemberPath = nameof(PrinterDeviceInfo.Name);
        foreach (var t in LabelTemplates.BuiltIn)
            _layouts.Add(new TemplateRow(t));
        LayoutList.ItemsSource = _layouts;
        SavedList.ItemsSource = _saved;
        _scanner.DeviceFound += d => Dispatcher.Invoke(() =>
        {
            if (_allDevices.Any(x => x.Id == d.Id || (!x.IsSerial && !d.IsSerial && x.BluetoothAddress == d.BluetoothAddress)))
                return;
            if (d.IsSerial)
                d.Name = Loc.T("comPort", d.ComPort ?? "");
            _allDevices.Add(d);
            RefreshDeviceList();
        });
        BodyBox.TextChanged += Preview_Changed;
        BodyBox.Document.LineHeight = 1.15;
        ReloadSaved(_settings.LastTemplateId);
        WidthBox.Text = _settings.WidthMm.ToString(CultureInfo.InvariantCulture);
        HeightBox.Text = _settings.HeightMm.ToString(CultureInfo.InvariantCulture);
        ApplyLanguage();
        FontSizeBox.SelectedIndex = 4;
        PaperBox.SelectedIndex = 0;
        ProtocolBox.SelectedIndex = 1;
        SetDisconnectedUi();
        Loaded += (_, _) => RefreshPreview();
        Closed += async (_, _) =>
        {
            Persist();
            _scanner.Dispose();
            if (_transport is not null)
                await _transport.DisposeAsync();
        };
        Loc.Changed += () => Dispatcher.Invoke(ApplyLanguage);
    }

    private void ReloadSaved(string? selectId)
    {
        _saved.Clear();
        foreach (var t in TemplateStore.Load())
            _saved.Add(new TemplateRow(t));
        if (selectId is not null)
            SavedList.SelectedItem = _saved.FirstOrDefault(x => x.Model.Id == selectId);
    }

    private TemplateRow? CurrentLayout => LayoutList.SelectedItem as TemplateRow;
    private TemplateRow? CurrentSaved => SavedList.SelectedItem as TemplateRow;

    private void ApplyLanguage()
    {
        _suppressUi = true;
        Title = Loc.T("app.title");
        TitleLabel.Text = Loc.T("app.name");
        SubtitleLabel.Text = Loc.T("header.subtitle");
        PrinterTitle.Text = Loc.T("printer");
        PrinterHint.Text = Loc.T("printer.hint");
        ScanButton.Content = WithIcon("\uE721", Loc.T("scan"));
        ConnectButton.Content = WithIcon("\uE8FB", Loc.T("connect"));
        DisconnectButton.Content = WithIcon("\uE8F8", Loc.T("disconnect"));
        DisconnectSavedButton.Content = WithIcon("\uE8F8", Loc.T("disconnect"));
        BackToSavedButton.Content = WithIcon("\uE8F1", Loc.T("backSaved"));
        ShowDevicesButton.Content = WithIcon("\uE702", Loc.T("showDevices"));
        DevicesTitle.Text = Loc.T("devices");
        ShowAllBox.Content = Loc.T("showAll");
        FilterHint.Text = Loc.T("filter.hint");
        EditorTitle.Text = Loc.T("editor");
        TemplatesTitle.Text = Loc.T("templates");
        TemplatesHint.Text = Loc.T("templates.hint");
        TextTitle.Text = Loc.T("text");
        SaveTemplateButton.Content = WithIcon("\uE74E", Loc.T("saveTemplate"));
        DeleteSavedButton.Content = WithIcon("\uE74D", Loc.T("deleteTemplate"));
        PrintButton.Content = WithIcon("\uE749", Loc.T("print"));
        OpenImageButton.Content = WithIcon("\uE8B9", Loc.T("image.open"));
        ClearImageButton.Content = WithIcon("\uE74D", Loc.T("image.clear"));
        ImageHint.Text = string.IsNullOrEmpty(_imagePath) ? Loc.T("image.hint") : Loc.T("image.loaded", Path.GetFileName(_imagePath));
        IssueButton.Content = WithIcon("\uE713", Loc.T("issue"));
        NeedPrinterTitle.Text = Loc.T("need.printer");
        NeedPrinterHint.Text = Loc.T("need.printer.hint");
        SavedTitle.Text = Loc.T("saved.title");
        SavedHint.Text = _saved.Count == 0 ? Loc.T("saved.empty") : "";
        SavedHint.Visibility = _saved.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PreviewTitle.Text = Loc.T("preview.live");
        SizeTitle.Text = Loc.T("size");
        WidthLabel.Content = Loc.T("width");
        HeightLabel.Content = Loc.T("height");
        PresetsLabel.Content = Loc.T("presets");
        PaperLabel.Content = Loc.T("paper");
        ProtocolLabel.Content = Loc.T("protocol");
        DensityLabel.Content = Loc.T("density");
        CopiesLabel.Content = Loc.T("copies");
        LangEn.Background = (Brush)FindResource(Loc.IsGeorgian ? "PanelAlt" : "AccentSoft");
        LangKa.Background = (Brush)FindResource(Loc.IsGeorgian ? "AccentSoft" : "PanelAlt");

        FillCombo(SizePreset, [
            ("57×30", "57,30"),
            ("57×40", "57,40"),
            ("57×50", "57,50"),
            ("57×80", "57,80"),
            ("40×30", "40,30"),
            ("57×100", "57,100")
        ]);
        FillCombo(FontSizeBox, ["12", "14", "16", "18", "20", "24", "28", "32", "36", "42", "48"]);
        if (FontSizeBox.SelectedIndex < 0) FontSizeBox.SelectedIndex = 4;
        FillCombo(PaperBox, [Loc.T("paper.cont"), Loc.T("paper.gap")]);
        FillCombo(ProtocolBox, [
            Loc.T("proto.auto"), Loc.T("proto.luck"), Loc.T("proto.escpos"),
            Loc.T("proto.cat"), Loc.T("proto.tspl")
        ]);
        HeadLabel.Content = Loc.T("headWidth");
        FillCombo(HeadBox, [
            (Loc.T("head.384"), "384"),
            (Loc.T("head.96"), "96"),
            (Loc.T("head.192"), "192"),
            (Loc.T("head.576"), "576"),
            (Loc.T("head.native"), "0")
        ]);
        if (PaperBox.SelectedIndex < 0) PaperBox.SelectedIndex = 0;

        foreach (var row in _layouts.Concat(_saved))
            row.Refresh();
        HintText.Text = CurrentLayout is null ? "" : CurrentLayout.Hint;
        var plain = GetBodyText();
        if (string.IsNullOrWhiteSpace(plain) || plain == _lastSample)
        {
            SetBodyText(Loc.T("sample"));
            _lastSample = Loc.T("sample");
        }
        foreach (var d in _allDevices.Where(x => x.IsSerial))
            d.Name = Loc.T("comPort", d.ComPort ?? "");
        RefreshDeviceList();
        UpdateConnectionChip();
        _suppressUi = false;
        RefreshPreview();
    }

    private static object WithIcon(string glyph, string text)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 14,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        panel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }

    private static void FillCombo(ComboBox box, IReadOnlyList<string> items)
    {
        var i = box.SelectedIndex;
        box.Items.Clear();
        foreach (var s in items)
            box.Items.Add(s);
        box.SelectedIndex = i >= 0 && i < items.Count ? i : 0;
    }

    private static void FillCombo(ComboBox box, (string Text, string Tag)[] items)
    {
        var tag = (box.SelectedItem as ComboBoxItem)?.Tag as string;
        box.Items.Clear();
        foreach (var (text, t) in items)
            box.Items.Add(new ComboBoxItem { Content = text, Tag = t });
        box.SelectedIndex = Math.Max(0, items.ToList().FindIndex(x => x.Tag == tag));
    }

    private void SetDisconnectedUi()
    {
        _transport = null;
        _connectedName = null;
        UpdateConnectionChip();
        ShowWorkspace(false);
        PrinterPane.Visibility = Visibility.Visible;
        SavedPane.Visibility = Visibility.Collapsed;
        DisconnectButton.Visibility = BackToSavedButton.Visibility = Visibility.Collapsed;
        StatusText.Text = Loc.T("status.disconnected");
    }

    private void ShowWorkspace(bool on)
    {
        ConnectPrompt.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        EditorCard.Visibility = PreviewCard.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (on)
        {
            PrinterPane.Visibility = Visibility.Collapsed;
            SavedPane.Visibility = Visibility.Visible;
        }
    }

    private void ShowDevices_Click(object sender, RoutedEventArgs e)
    {
        SavedPane.Visibility = Visibility.Collapsed;
        PrinterPane.Visibility = Visibility.Visible;
        DisconnectButton.Visibility = Visibility.Visible;
        BackToSavedButton.Visibility = Visibility.Visible;
    }

    private void BackToSaved_Click(object sender, RoutedEventArgs e)
    {
        PrinterPane.Visibility = Visibility.Collapsed;
        SavedPane.Visibility = Visibility.Visible;
    }

    private void UpdateConnectionChip()
    {
        var on = _transport is not null;
        ConnChip.Background = (Brush)FindResource(on ? "OkSoft" : "DangerSoft");
        ConnIcon.Text = on ? "\uE8FB" : "\uE8F8";
        ConnIcon.Foreground = ConnLabel.Foreground = (Brush)FindResource(on ? "Ok" : "Danger");
        ConnLabel.Text = on
            ? Loc.T("status.connectedNamed", _connectedName ?? "")
            : Loc.T("status.disconnected");
        PrintButton.IsEnabled = on;
        PrintButton.Opacity = on ? 1 : 0.45;
    }

    private void LangEn_Click(object sender, RoutedEventArgs e)
    {
        Loc.Set("en");
        _settings.Language = "en";
        _settings.Save();
    }

    private void LangKa_Click(object sender, RoutedEventArgs e)
    {
        Loc.Set("ka");
        _settings.Language = "ka";
        _settings.Save();
    }

    private void Issue_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/giulbaziani/MiniPrinter/issues/new",
            UseShellExecute = true
        });
    }

    private void LayoutList_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUi || CurrentLayout is null) return;
        HintText.Text = CurrentLayout.Hint;
        WidthBox.Text = CurrentLayout.Model.WidthMm.ToString(CultureInfo.InvariantCulture);
        HeightBox.Text = CurrentLayout.Model.HeightMm.ToString(CultureInfo.InvariantCulture);
        RefreshPreview();
    }

    private void SavedList_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUi || CurrentSaved is null) return;
        var m = CurrentSaved.Model;
        if (!string.IsNullOrWhiteSpace(m.BodyXaml))
            SetBodyXaml(m.BodyXaml);
        else
            SetBodyText(m.Body);
        WidthBox.Text = m.WidthMm.ToString(CultureInfo.InvariantCulture);
        HeightBox.Text = m.HeightMm.ToString(CultureInfo.InvariantCulture);
        PaperBox.SelectedIndex = m.PaperIndex;
        DensityBox.Text = m.Density.ToString(CultureInfo.InvariantCulture);
        LayoutList.SelectedItem = _layouts.FirstOrDefault(x => x.Model.Layout == m.Layout) ?? LayoutList.SelectedItem;
        _imagePath = TemplateStore.ResolveImage(m.ImageFileName);
        ImageHint.Text = _imagePath is null ? Loc.T("image.hint") : Loc.T("image.loaded", Path.GetFileName(_imagePath));
        SelectFontSize(m.FontSize);
        BoldBtn.IsChecked = m.Bold;
        ItalicBtn.IsChecked = m.Italic;
        UnderlineBtn.IsChecked = m.Underline;
        ApplyEditorFont();
        RefreshPreview();
    }

    private void ShowAll_Click(object sender, RoutedEventArgs e) => RefreshDeviceList();

    private void RefreshDeviceList()
    {
        var showAll = ShowAllBox.IsChecked == true;
        var selected = DeviceList.SelectedItem as PrinterDeviceInfo;
        _devices.Clear();
        foreach (var d in _allDevices.Where(d => showAll || d.IsLikelyPrinter))
            _devices.Add(d);
        if (selected is not null)
            DeviceList.SelectedItem = _devices.FirstOrDefault(x => x.Id == selected.Id);
        var hidden = _allDevices.Count - _devices.Count;
        FilterHint.Text = hidden > 0 && !showAll
            ? Loc.T("status.foundHidden", _devices.Count, hidden)
            : Loc.T("filter.hint");
    }

    private void SetBusy(bool on, string? message = null)
    {
        BusyOverlay.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        BusyText.Text = message ?? "";
        if (Resources["BusySpin"] is Storyboard spin)
        {
            if (on) spin.Begin(this, true);
            else spin.Stop(this);
        }
        ScanButton.IsEnabled = ConnectButton.IsEnabled = DisconnectButton.IsEnabled = !on;
        ShowAllBox.IsEnabled = PrintButton.IsEnabled = SaveTemplateButton.IsEnabled = !on;
        OpenImageButton.IsEnabled = ClearImageButton.IsEnabled = !on;
        if (!on)
            PrintButton.IsEnabled = _transport is not null;
    }

    private void Scan_Click(object sender, RoutedEventArgs e)
    {
        _allDevices.Clear();
        _devices.Clear();
        _scanner.IncludeSerialPorts = ShowAllBox.IsChecked == true;
        StatusText.Text = Loc.T("status.scanning");
        SetBusy(true, Loc.T("status.busy.scan"));
        _scanner.Start();
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;
        _ = Task.Run(async () =>
        {
            await Task.Delay(10000, token);
            if (token.IsCancellationRequested) return;
            Dispatcher.Invoke(() =>
            {
                _scanner.Stop();
                SetBusy(false);
                RefreshDeviceList();
                StatusText.Text = _devices.Count == 0
                    ? Loc.T("status.noneFound")
                    : _allDevices.Count > _devices.Count
                        ? Loc.T("status.foundHidden", _devices.Count, _allDevices.Count - _devices.Count)
                        : Loc.T("status.found", _devices.Count);
            });
        }, token);
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceList.SelectedItem is not PrinterDeviceInfo device)
        {
            StatusText.Text = Loc.Error(ErrorCode.DeviceNotSelected);
            return;
        }

        try
        {
            SetBusy(true, Loc.T("status.busy.connect"));
            StatusText.Text = Loc.T("status.connecting");
            if (_transport is not null)
                await _transport.DisposeAsync();
            _transport = device.IsSerial
                ? new SerialPrinterTransport(device.ComPort!)
                : await BlePrinterTransport.ConnectAsync(device.BluetoothAddress, CancellationToken.None);
            _connectedName = device.Name;
            UpdateConnectionChip();
            ShowWorkspace(true);
            StatusText.Text = Loc.T("status.connectedNamed", device.Name);
        }
        catch (Exception ex)
        {
            SetDisconnectedUi();
            StatusText.Text = ex is AppException ? Loc.FromException(ex) : Loc.Error(ErrorCode.ConnectFailed);
        }
        finally
        {
            SetBusy(false);
            UpdateConnectionChip();
        }
    }

    private async void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        if (_transport is not null)
            await _transport.DisposeAsync();
        SetDisconnectedUi();
    }

    private async void Print_Click(object sender, RoutedEventArgs e)
    {
        if (_transport is null)
        {
            StatusText.Text = Loc.Error(ErrorCode.PrinterDisconnected);
            UpdateConnectionChip();
            return;
        }

        try
        {
            SetBusy(true, Loc.T("status.busy.print"));
            var (bmp, _) = BuildLabel();
            var copies = ParseInt(CopiesBox.Text, 1);
            var job = new LabelJob
            {
                Bitmap = bmp,
                Paper = PaperBox.SelectedIndex == 1 ? PaperKind.Gap : PaperKind.Continuous,
                Density = ParseInt(DensityBox.Text, 3),
                Copies = copies,
                Protocol = (PrintProtocolKind)ProtocolBox.SelectedIndex
            };
            var protocol = ProtocolFactory.Resolve(job.Protocol, _transport.Services);
            var payload = protocol.BuildJob(job);
            StatusText.Text = Loc.T("status.printing", Loc.Protocol(protocol.Kind));
            for (var i = 0; i < copies; i++)
                await _transport.WriteAsync(payload, chunkSize: 20, delayMs: 18, CancellationToken.None);
            await Task.Delay(400);
            StatusText.Text = Loc.T("status.printed");
        }
        catch (Exception ex)
        {
            StatusText.Text = ex is AppException ? Loc.FromException(ex) : Loc.Error(ErrorCode.PrintFailed);
        }
        finally
        {
            SetBusy(false);
            UpdateConnectionChip();
        }
    }

    private void OpenImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All|*.*"
        };
        if (dlg.ShowDialog(this) != true) return;
        _imagePath = dlg.FileName;
        ImageHint.Text = Loc.T("image.loaded", Path.GetFileName(_imagePath));
        RefreshPreview();
    }

    private void ClearImage_Click(object sender, RoutedEventArgs e)
    {
        _imagePath = null;
        ImageHint.Text = Loc.T("image.hint");
        RefreshPreview();
    }

    private void SaveTemplate_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveTemplateWindow(CurrentSaved?.DisplayName ?? CurrentLayout?.DisplayName) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var saved = new SavedTemplate
        {
            Name = dlg.TemplateName,
            Layout = CurrentLayout?.Model.Layout ?? LayoutKind.Custom,
            WidthMm = ParseDouble(WidthBox.Text, 57),
            HeightMm = ParseDouble(HeightBox.Text, 30),
            Body = GetBodyText(),
            BodyXaml = GetBodyXaml(),
            PaperIndex = Math.Max(0, PaperBox.SelectedIndex),
            Density = ParseInt(DensityBox.Text, 3),
            Icon = CurrentLayout?.Model.Icon ?? "\uE8A5",
            ImageFileName = TemplateStore.CopyImage(_imagePath ?? ""),
            FontSize = CurrentFontSize(),
            Bold = BoldBtn.IsChecked == true,
            Italic = ItalicBtn.IsChecked == true,
            Underline = UnderlineBtn.IsChecked == true
        };
        var row = new TemplateRow(saved);
        _saved.Add(row);
        TemplateStore.Save(_saved.Select(t => t.Model));
        SavedList.SelectedItem = row;
        SavedHint.Visibility = Visibility.Collapsed;
        StatusText.Text = Loc.T("status.saved");
    }

    private void DeleteTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentSaved is null) return;
        _saved.Remove(CurrentSaved);
        TemplateStore.Save(_saved.Select(t => t.Model));
        SavedList.SelectedIndex = _saved.Count > 0 ? 0 : -1;
        SavedHint.Text = _saved.Count == 0 ? Loc.T("saved.empty") : "";
        SavedHint.Visibility = _saved.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = Loc.T("status.deleted");
    }

    private void SizePreset_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUi) return;
        if (SizePreset.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;
        var parts = tag.Split(',');
        WidthBox.Text = parts[0];
        HeightBox.Text = parts[1];
        RefreshPreview();
    }

    private void Preview_Changed(object sender, RoutedEventArgs e) => RefreshPreview();

    private void Font_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressUi) return;
        var sel = BodyBox.Selection;
        if (sender == FontSizeBox || ReferenceEquals(e.OriginalSource, FontSizeBox))
            sel.ApplyPropertyValue(TextElement.FontSizeProperty, (double)CurrentFontSize());
        if (sender == BoldBtn)
            sel.ApplyPropertyValue(TextElement.FontWeightProperty,
                BoldBtn.IsChecked == true ? FontWeights.Bold : FontWeights.Normal);
        if (sender == ItalicBtn)
            sel.ApplyPropertyValue(TextElement.FontStyleProperty,
                ItalicBtn.IsChecked == true ? FontStyles.Italic : FontStyles.Normal);
        if (sender == UnderlineBtn)
            sel.ApplyPropertyValue(Inline.TextDecorationsProperty,
                UnderlineBtn.IsChecked == true ? TextDecorations.Underline : null);
        BodyBox.Focus();
        RefreshPreview();
    }

    private void Body_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressUi) return;
        _suppressUi = true;
        var weight = BodyBox.Selection.GetPropertyValue(TextElement.FontWeightProperty);
        BoldBtn.IsChecked = weight is FontWeight fw && fw == FontWeights.Bold;
        var style = BodyBox.Selection.GetPropertyValue(TextElement.FontStyleProperty);
        ItalicBtn.IsChecked = style is FontStyle fs && fs == FontStyles.Italic;
        var dec = BodyBox.Selection.GetPropertyValue(Inline.TextDecorationsProperty);
        UnderlineBtn.IsChecked = dec is TextDecorationCollection td && td.Count > 0;
        _suppressUi = false;
    }

    private void ApplyEditorFont()
    {
        BodyBox.FontSize = CurrentFontSize();
    }

    private string GetBodyText()
    {
        var range = new TextRange(BodyBox.Document.ContentStart, BodyBox.Document.ContentEnd);
        return range.Text.TrimEnd('\r', '\n');
    }

    private void SetBodyText(string text)
    {
        BodyBox.Document.Blocks.Clear();
        var p = new Paragraph(new Run(text)) { FontSize = CurrentFontSize(), Margin = new Thickness(0) };
        BodyBox.Document.Blocks.Add(p);
    }

    private string GetBodyXaml()
    {
        return XamlWriter.Save(BodyBox.Document);
    }

    private void SetBodyXaml(string xaml)
    {
        try
        {
            if (XamlReader.Parse(xaml) is FlowDocument doc)
                BodyBox.Document = doc;
            else
                SetBodyText("");
        }
        catch
        {
            SetBodyText("");
        }
    }

    private List<TextSpan> CollectBodySpans()
    {
        var spans = new List<TextSpan>();
        foreach (var block in BodyBox.Document.Blocks)
        {
            if (block is not Paragraph para) continue;
            if (spans.Count > 0)
                spans.Add(new TextSpan { Text = "\n" });
            CollectInlines(para.Inlines, spans);
        }
        return spans;
    }

    private static void CollectInlines(InlineCollection inlines, List<TextSpan> spans)
    {
        foreach (var inline in inlines)
        {
            if (inline is Run run && run.Text.Length > 0)
            {
                spans.Add(new TextSpan
                {
                    Text = run.Text,
                    Bold = run.FontWeight == FontWeights.Bold,
                    Italic = run.FontStyle == FontStyles.Italic,
                    Underline = run.TextDecorations is { Count: > 0 }
                });
            }
            else if (inline is LineBreak)
                spans.Add(new TextSpan { Text = "\n" });
            else if (inline is Span nested)
                CollectInlines(nested.Inlines, spans);
        }
    }

    private int CurrentFontSize()
    {
        var text = FontSizeBox.SelectedItem as string ?? FontSizeBox.Text;
        return ParseInt(text, 18);
    }

    private void SelectFontSize(int size)
    {
        for (var i = 0; i < FontSizeBox.Items.Count; i++)
        {
            if (FontSizeBox.Items[i] is string s && s == size.ToString(CultureInfo.InvariantCulture))
            {
                FontSizeBox.SelectedIndex = i;
                return;
            }
        }
        FontSizeBox.Text = size.ToString(CultureInfo.InvariantCulture);
    }

    private void RefreshPreview()
    {
        if (!IsLoaded || _suppressUi) return;
        try
        {
            var (bmp, preview) = BuildLabel();
            PreviewImage.Source = preview;
            PreviewMeta.Text = Loc.T("dots", bmp.WidthDots, bmp.HeightDots);
        }
        catch (Exception)
        {
            PreviewMeta.Text = Loc.Error(ErrorCode.PreviewFailed);
        }
    }

    private (LabelBitmap Bitmap, System.Windows.Media.Imaging.BitmapSource Preview) BuildLabel()
    {
        var w = ParseDouble(WidthBox.Text, 57);
        var h = ParseDouble(HeightBox.Text, 30);
        var head = 384;
        if (HeadBox.SelectedItem is ComboBoxItem hi && hi.Tag is string tag)
            int.TryParse(tag, out head);
        if (!string.IsNullOrEmpty(_imagePath) && File.Exists(_imagePath))
            return LabelRenderer.RenderImage(_imagePath, w, h, 203, head);
        var layout = CurrentLayout?.Model.Layout ?? LayoutKind.Plain;
        var plain = GetBodyText();
        var content = LabelTemplates.Build(layout, plain);
        var size = CurrentFontSize();
        content.BodySize = size;
        if (content.TitleSize < size) content.TitleSize = size;
        content.BodySpans = CollectBodySpans();
        if (content.BodySpans.Count > 0)
        {
            content.Body = string.Concat(content.BodySpans.Select(s => s.Text));
            if (string.IsNullOrEmpty(content.QrPayload) && string.IsNullOrEmpty(content.BarcodePayload))
            {
                content.Title = "";
                content.Footer = "";
            }
        }
        return LabelRenderer.Render(w, h, 203, content, head);
    }

    private void Persist()
    {
        _settings.Language = Loc.Language;
        _settings.LastTemplateId = CurrentSaved?.Model.Id ?? CurrentLayout?.Model.Id ?? "plain";
        _settings.WidthMm = ParseDouble(WidthBox.Text, 57);
        _settings.HeightMm = ParseDouble(HeightBox.Text, 30);
        _settings.Save();
        TemplateStore.Save(_saved.Select(t => t.Model));
    }

    private static int ParseInt(string text, int fallback) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static double ParseDouble(string text, double fallback) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
}

public sealed class TemplateRow : System.ComponentModel.INotifyPropertyChanged
{
    public TemplateRow(SavedTemplate model) => Model = model;
    public SavedTemplate Model { get; }
    public string Icon => Model.Icon;
    public string DisplayName => Model.IsBuiltIn ? Loc.T(LabelTemplates.TitleKey(Model.Layout)) : Model.Name;
    public string Hint => Model.IsBuiltIn ? Loc.T(LabelTemplates.HintKey(Model.Layout)) : Loc.T("saved.hint");
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public void Refresh() => PropertyChanged?.Invoke(this, new(nameof(DisplayName)));
    public override string ToString() => DisplayName;
}
