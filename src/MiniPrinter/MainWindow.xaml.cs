using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MiniPrinter.Bluetooth;
using MiniPrinter.Localization;
using MiniPrinter.Printing;
using MiniPrinter.Templates;

namespace MiniPrinter;

public partial class MainWindow : Window
{
    private readonly List<PrinterDeviceInfo> _allDevices = [];
    private readonly ObservableCollection<PrinterDeviceInfo> _devices = [];
    private readonly ObservableCollection<TemplateRow> _templates = [];
    private readonly DeviceScanner _scanner = new();
    private readonly AppSettings _settings;
    private IPrinterTransport? _transport;
    private string? _connectedName;
    private CancellationTokenSource? _scanCts;
    private bool _suppressUi;
    private string _lastSample = "";

    public MainWindow()
    {
        _settings = AppSettings.Load();
        Loc.Set(_settings.Language);
        InitializeComponent();
        DeviceList.ItemsSource = _devices;
        DeviceList.DisplayMemberPath = nameof(PrinterDeviceInfo.Name);
        TemplateList.ItemsSource = _templates;
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
        ReloadTemplates(_settings.LastTemplateId);
        WidthBox.Text = _settings.WidthMm.ToString(CultureInfo.InvariantCulture);
        HeightBox.Text = _settings.HeightMm.ToString(CultureInfo.InvariantCulture);
        ApplyLanguage();
        PaperBox.SelectedIndex = 1;
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

    private void ReloadTemplates(string? selectId)
    {
        _templates.Clear();
        foreach (var t in LabelTemplates.BuiltIn)
            _templates.Add(new TemplateRow(t));
        foreach (var t in TemplateStore.Load())
            _templates.Add(new TemplateRow(t));
        TemplateList.SelectedItem = _templates.FirstOrDefault(x => x.Model.Id == selectId) ?? _templates.FirstOrDefault();
    }

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
        DevicesTitle.Text = Loc.T("devices");
        ShowAllBox.Content = Loc.T("showAll");
        FilterHint.Text = Loc.T("filter.hint");
        EditorTitle.Text = Loc.T("editor");
        TemplatesTitle.Text = Loc.T("templates");
        TemplatesHint.Text = Loc.T("templates.hint");
        TextTitle.Text = Loc.T("text");
        SaveTemplateButton.Content = WithIcon("\uE74E", Loc.T("saveTemplate"));
        DeleteTemplateButton.Content = WithIcon("\uE74D", Loc.T("deleteTemplate"));
        PrintButton.Content = WithIcon("\uE749", Loc.T("print"));
        PreviewTitle.Text = Loc.T("preview.live");
        SizeTitle.Text = Loc.T("size");
        WidthLabel.Content = Loc.T("width");
        HeightLabel.Content = Loc.T("height");
        PresetsLabel.Content = Loc.T("presets");
        PaperLabel.Content = Loc.T("paper");
        ProtocolLabel.Content = Loc.T("protocol");
        DensityLabel.Content = Loc.T("density");
        CopiesLabel.Content = Loc.T("copies");
        LangEn.Content = Loc.T("lang.en");
        LangKa.Content = Loc.T("lang.ka");
        LangEn.Background = (Brush)FindResource(Loc.IsGeorgian ? "PanelAlt" : "AccentSoft");
        LangKa.Background = (Brush)FindResource(Loc.IsGeorgian ? "AccentSoft" : "PanelAlt");

        FillCombo(SizePreset, [
            ("ORICO S1 — 57×30", "57,30"),
            ("57×40", "57,40"),
            ("57×50", "57,50"),
            ("57×80", "57,80"),
            ("40×30", "40,30"),
            ("57×100", "57,100")
        ]);
        FillCombo(PaperBox, [Loc.T("paper.gap"), Loc.T("paper.cont")]);
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
        if (PaperBox.SelectedIndex < 0) PaperBox.SelectedIndex = 1;

        foreach (var row in _templates)
            row.Refresh();
        HintText.Text = Current is null ? "" : Current.Hint;
        if (string.IsNullOrWhiteSpace(BodyBox.Text) || BodyBox.Text == _lastSample)
        {
            BodyBox.Text = Loc.T("sample");
            _lastSample = BodyBox.Text;
        }
        foreach (var d in _allDevices.Where(x => x.IsSerial))
            d.Name = Loc.T("comPort", d.ComPort ?? "");
        RefreshDeviceList();
        UpdateConnectionChip();
        if (_transport is null && (StatusText.Text.Length == 0 || StatusText.Tag is null))
            StatusText.Text = Loc.T("status.disconnected");
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

    private TemplateRow? Current => TemplateList.SelectedItem as TemplateRow;

    private void SetDisconnectedUi()
    {
        _transport = null;
        _connectedName = null;
        UpdateConnectionChip();
        PrintButton.IsEnabled = false;
        StatusText.Text = Loc.T("status.disconnected");
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

    private void TemplateList_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUi || Current is null) return;
        HintText.Text = Current.Hint;
        if (!Current.Model.IsBuiltIn)
        {
            BodyBox.Text = Current.Model.Body;
            WidthBox.Text = Current.Model.WidthMm.ToString(CultureInfo.InvariantCulture);
            HeightBox.Text = Current.Model.HeightMm.ToString(CultureInfo.InvariantCulture);
            PaperBox.SelectedIndex = Current.Model.PaperIndex;
            DensityBox.Text = Current.Model.Density.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            WidthBox.Text = Current.Model.WidthMm.ToString(CultureInfo.InvariantCulture);
            HeightBox.Text = Current.Model.HeightMm.ToString(CultureInfo.InvariantCulture);
        }
        DeleteTemplateButton.IsEnabled = Current is { Model.IsBuiltIn: false };
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
        ScanButton.IsEnabled = ConnectButton.IsEnabled = DisconnectButton.IsEnabled = !on;
        ShowAllBox.IsEnabled = PrintButton.IsEnabled = SaveTemplateButton.IsEnabled = !on && (!on || _transport is not null);
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
                Paper = PaperBox.SelectedIndex == 0 ? PaperKind.Gap : PaperKind.Continuous,
                Density = ParseInt(DensityBox.Text, 3),
                Copies = copies,
                Protocol = (PrintProtocolKind)ProtocolBox.SelectedIndex
            };
            var protocol = ProtocolFactory.Resolve(job.Protocol, _transport.Services);
            var payload = protocol.BuildJob(job);
            StatusText.Text = Loc.T("status.printing", Loc.Protocol(protocol.Kind));
            for (var i = 0; i < copies; i++)
                await _transport.WriteAsync(payload, chunkSize: 64, delayMs: 40, CancellationToken.None);
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

    private void SaveTemplate_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveTemplateWindow(Current?.DisplayName) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var saved = new SavedTemplate
        {
            Name = dlg.TemplateName,
            Layout = Current?.Model.Layout ?? LayoutKind.Custom,
            WidthMm = ParseDouble(WidthBox.Text, 57),
            HeightMm = ParseDouble(HeightBox.Text, 30),
            Body = BodyBox.Text,
            PaperIndex = Math.Max(0, PaperBox.SelectedIndex),
            Density = ParseInt(DensityBox.Text, 3),
            Icon = Current?.Model.Icon ?? "\uE8A5"
        };
        var row = new TemplateRow(saved);
        _templates.Add(row);
        TemplateStore.Save(_templates.Select(t => t.Model));
        TemplateList.SelectedItem = row;
        StatusText.Text = Loc.T("status.saved");
    }

    private void DeleteTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (Current is null || Current.Model.IsBuiltIn) return;
        _templates.Remove(Current);
        TemplateStore.Save(_templates.Select(t => t.Model));
        TemplateList.SelectedIndex = 0;
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
        var layout = Current?.Model.Layout ?? LayoutKind.Plain;
        var head = 384;
        if (HeadBox.SelectedItem is ComboBoxItem hi && hi.Tag is string tag)
            int.TryParse(tag, out head);
        return LabelRenderer.Render(w, h, 203, LabelTemplates.Build(layout, BodyBox.Text), head);
    }

    private void Persist()
    {
        _settings.Language = Loc.Language;
        _settings.LastTemplateId = Current?.Model.Id ?? "plain";
        _settings.WidthMm = ParseDouble(WidthBox.Text, 57);
        _settings.HeightMm = ParseDouble(HeightBox.Text, 30);
        _settings.Save();
        TemplateStore.Save(_templates.Select(t => t.Model));
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
    public string Hint => Model.IsBuiltIn ? Loc.T(LabelTemplates.HintKey(Model.Layout)) : Loc.T("templates.hint");
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public void Refresh() => PropertyChanged?.Invoke(this, new(nameof(DisplayName)));
}
