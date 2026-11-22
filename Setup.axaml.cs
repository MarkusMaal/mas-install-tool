using System;
using System.IO;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Win32;

namespace mas_install_tool;

public partial class Setup : Window
{
    private bool _closing = false;
    internal bool Result { get; set; } = false;
    public Setup()
    {
        InitializeComponent();
    }

    private void BlinkWarning()
    {
        IBrush? initialColor = null;
        Dispatcher.UIThread.Post(() =>
        {
            initialColor = WarningLabel.Foreground;
        });
        var red = false;
        while (true)
        {
            red = !red;
            var red1 = red;
            var breakNow = false;
            Dispatcher.UIThread.Post(() =>
            {
                if (_closing)
                {
                    breakNow = true;
                    return;
                }
                WarningLabel.Foreground = red1 ? new SolidColorBrush(Colors.Red) : initialColor; 
            });
            if (breakNow) break;
            Thread.Sleep(1000);
        }
    }

    private void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (OperatingSystem.IsWindows())
        {
            
            var winver = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", "")?.ToString();
            if (winver != null)
            {
                for (var i = 10; i < 1900; i++) // note: may break in the future if Microsoft decide to
                {                               // ditch the numerical names for the Windows operating system
                    if (!winver.Contains($"Windows {i}")) continue;
                    WxCheck.IsChecked = true;
                    break;
                }
                GpCheck.IsChecked = winver.Contains("PRO", StringComparison.InvariantCultureIgnoreCase)
                                    || winver.Contains("ENTERPRISE", StringComparison.InvariantCultureIgnoreCase)
                                    || winver.Contains("ULTIMATE", StringComparison.InvariantCultureIgnoreCase);
            }
            RmCheck.IsChecked = Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\\Rainmeter");
            CsCheck.IsChecked = Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\\StartAllBack") || Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\\Open-Shell") || Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\\Classic-Shell");
            LtCheck.IsChecked = Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\\LiveTuner");
            RmCheck.IsEnabled = RmCheck.IsChecked ?? false;
            CsCheck.IsEnabled = CsCheck.IsChecked ?? false;
            LtCheck.IsEnabled = LtCheck.IsChecked ?? false;
        }
        PremiumRadioButton.IsChecked = true;
        PremiumRadioButton.IsChecked = false;
        BPlusRadioButton.IsChecked = true;
        new Thread(BlinkWarning).Start();
    }
    
    

    private void Window_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        _closing = true;
    }

    private void ToggleButton_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (ProRadioButton.IsChecked ?? false)
        {
            MaiaCheck.IsChecked = true;
            IpCheck.IsChecked = true;
            NoteCheck.IsChecked = true;
            OptCheck.IsChecked = true;
            OptLabel.IsVisible = true;
            OptPanel.IsVisible = true;
            return;
        }

        if (PremiumRadioButton.IsChecked ?? false)
        {
            MaiaCheck.IsChecked = false;
            IpCheck.IsChecked = true;
            NoteCheck.IsChecked = true;
            OptCheck.IsChecked = true;
            OptLabel.IsVisible = true;
            OptPanel.IsVisible = true;
            return;
        }

        if (!(BPlusRadioButton.IsChecked ?? false)) return;
        MaiaCheck.IsChecked = false;
        IpCheck.IsChecked = false;
        NoteCheck.IsChecked = false;
        OptCheck.IsChecked = false;
        OptLabel.IsVisible = false;
        OptPanel.IsVisible = false;
    }

    private void NoCheck(object? sender, RoutedEventArgs e)
    {
        var premiumOrPro = (ProRadioButton.IsChecked ?? false) || (PremiumRadioButton.IsChecked ?? false);
        InfoCheck.IsChecked = true;
        WallCheck.IsChecked = true;
        FlashCheck.IsChecked = true;
        MaiaCheck.IsChecked = ProRadioButton.IsChecked;
        IpCheck.IsChecked = premiumOrPro;
        NoteCheck.IsChecked = premiumOrPro;
        OptCheck.IsChecked = premiumOrPro;
    }

    private void ConfirmButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Result = true;
        Close();
    }
}