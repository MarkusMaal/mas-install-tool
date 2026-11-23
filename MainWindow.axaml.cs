using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using SFML.Audio;

namespace mas_install_tool;

public partial class MainWindow : Window
{
    private Music? _mp;
    private string _bgmFile = Path.GetTempFileName();
    private bool _animate;
    private readonly string? _assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
    private readonly string[]? _driveInfo;
    
    public MainWindow()
    {
        if (!Design.IsDesignMode)
        {
            if (OperatingSystem.IsWindows()) // uses WMIC (System.Management)
            {
                List<string> drives = [];
                foreach (var drv in DriveInfo.GetDrives())
                {
                    if (drv.DriveType == DriveType.Removable)
                    {
                        drives.Add($";{GetSerialFromDrive(drv.RootDirectory.FullName[..^1])};");
                    }
                }

                _driveInfo = [.. drives];
            }

            if (OperatingSystem.IsLinux()) // uses LibUsbDotNet
            {
                var commandLine = Environment.ProcessPath;
                if (commandLine == null) return;
                var p = new Process()
                {
                    StartInfo =
                        new ProcessStartInfo("pkexec",
                                commandLine + " --collect-info") // prompts the user for root permissions
                            {
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                            }
                };
                p.Start();
                p.WaitForExit();
                var stdout = p.StandardOutput.ReadToEnd();
                var stderr = p.StandardError.ReadToEnd();
                _driveInfo = stdout.Split(Environment.NewLine);
                _driveInfo = [.. _driveInfo.Take(_driveInfo.Length - 1)];
                if (stderr.Contains("error", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine("Root access is required for USB device identification");
                    Environment.Exit(255);
                }
            }
        }

        InitializeComponent();
        if (Design.IsDesignMode) return;
        new Thread(() =>
        {
            using var os = new FileStream(_bgmFile, FileMode.Create);
            var asset = AssetLoader.Open(new Uri($"avares://{_assemblyName}/Resources/root_bg_begin.wav"));
            var buffer = new byte[asset.Length];
            asset.ReadExactly(buffer);
            os.Write(buffer);
            asset.Close();
            asset.Dispose();
            os.Close();
            _mp = new Music(_bgmFile);
            _mp.Play();
            while (_mp.Status == SoundStatus.Playing)
            {
                Thread.Sleep(100);
            }

            if (OperatingSystem.IsWindows())
            {
                _bgmFile = Path.GetTempFileName();
            }

            File.Delete(_bgmFile);
            using var los = new FileStream(_bgmFile, FileMode.Create);
            asset = AssetLoader.Open(new Uri($"avares://{_assemblyName}/Resources/root_bg_loop.wav"));
            buffer = new byte[asset.Length];
            asset.ReadExactly(buffer);
            los.Write(buffer);
            asset.Close();
            asset.Dispose();
            los.Close();
            _mp = new Music(_bgmFile);
            _mp.IsLooping = true;
            _mp.Play();
        }).Start();
        new Thread(() =>
        {
            Thread.Sleep(1000);
            var vf = new Verifile();
            if (Verifile.CheckVerifileTamper() && vf.MakeAttestation().Equals("VERIFIED"))
            {
                Task? task = null;
                ToggleCenter(false);
                Dispatcher.UIThread.Post(() =>
                {
                    task = MessageBoxShow(
                        "Arvuti on juba juurutatud. Soovi korral saate käivitada programm --bless argumendiga, et lisada sõrmejälg mälupulgale.",
                        "Markuse asjade juurutamise tööriist", ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Success);
                });
                while (!task?.IsCompleted ?? true) Thread.Sleep(100);
                Dispatcher.UIThread.Post(Close);
                return;
            }
            if (Verifile.CheckVerifileTamper() && !vf.MakeAttestation().Equals("VERIFIED"))
            {
                Task? task = null;
                ToggleCenter(false);
                Dispatcher.UIThread.Post(() =>
                {
                    task = MessageBoxShow(
                        "Arvuti pole õigesti juurutatud. Palun kustuta .mas kataloog kasutaja kaustast ning käivita see programm uuesti. Programm sulgub nüüd.",
                        "Markuse asjade juurutamise tööriist", ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error);
                });
                while (!task?.IsCompleted ?? true) Thread.Sleep(100);
                Dispatcher.UIThread.Post(Close);
                return;
            }
            Dispatcher.UIThread.Post(() =>
            {
                var str = AssetLoader.Open(new Uri($"avares://{_assemblyName}/Resources/root_idc_key.png"));
                CenterImage.Source = new Bitmap(str);
                CenterText.Content = "Andmete dekrüpteerimine";
            });
            var attempts = 0;
            do
            {
                Thread.Sleep(1000);
                attempts++;
            } while (!InstallTools.CheckAuthority(_driveInfo ?? []) && attempts < 10);
            if (attempts >= 10)
            {
                Task? task = null;
                ToggleCenter(false);
                Dispatcher.UIThread.Post(() =>
                {
                    task = MessageBoxShow(
                        "Sobiva sõrmejäljega mälupulka ei leitud. Programm sulgub nüüd.",
                        "Markuse asjade juurutamise tööriist", ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error);
                });
                while (!task?.IsCompleted ?? true) Thread.Sleep(100);
                Dispatcher.UIThread.Post(Close);
                return;
            }
            ToggleCenter(false);
            Thread.Sleep(100);
            InstallTools.RunSetup(this);
        }).Start();
    }

    internal void ToggleCenter(bool visible)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CenterImage.IsVisible = visible;
            CenterText.IsVisible = visible;
            MidProgress.IsVisible = visible;
        });
    }

    internal void Finish()
    {
        _animate = false;
        Dispatcher.UIThread.Post(() =>
        {
            TopLeftText.Content = "Valmis";
            NoticeText.IsVisible = false;
        });
        new Thread(() =>
        {
            Task? task = null;
            ToggleCenter(false);
            Dispatcher.UIThread.Post(() =>
            {
                task = MessageBoxShow("Seade juurutati edukalt", "Markuse asjade juurutamise tööriist", ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Success);
            });
            while (!task?.IsCompleted ?? true) Thread.Sleep(100);
            Dispatcher.UIThread.Post(Close);
            while (!task.IsCompleted)
            {
                Thread.Sleep(100);
            }

            Dispatcher.UIThread.Post(Close);
            Thread.Sleep(1000);
            Environment.Exit(0);
        }).Start();
    }

    private void Window_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_animate)
        {
            e.Cancel = true;
            return;
        }
        if (_mp?.Status == SoundStatus.Playing) 
            _mp?.Stop();
        if (File.Exists(_bgmFile) && !OperatingSystem.IsWindows())
            File.Delete(_bgmFile);
    }

    internal void AnimateCenter()
    {
        List<Stream> animObjects = [];
        for (var i = 1; i <= 12; i++)
        {
            animObjects.Add(AssetLoader.Open(new Uri($"avares://{_assemblyName}/Resources/root_root_{i}.png")));
        }
        _animate = true;
        new Thread(() =>
        {
            while (_animate)
            {
                foreach (var ao in  animObjects)
                {
                    ao.Position = 0;
                    Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            CenterImage.Source = new Bitmap(ao);
                        }
                        catch (ArgumentException)
                        {
                            Console.WriteLine("Error displaying bitmap!");
                        }
                    });
                    Thread.Sleep(200);
                }
            }
            foreach (var ao in animObjects)
            {
                ao.Close();
            }
        }).Start();
    }

    private static string GetSerialFromDrive(string mount)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var partitions = new ManagementObjectSearcher("ASSOCIATORS OF {Win32_LogicalDisk.DeviceID='" +
                                                                    mount +
                                                                    "'} WHERE ResultClass=Win32_DiskPartition");
                foreach (var partition in partitions.Get())
                {
                    using var drives = new ManagementObjectSearcher("ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" +
                                                                    partition["DeviceID"] +
                                                                    "'} WHERE ResultClass=Win32_DiskDrive");
                    foreach (var drive in drives.Get())
                    {
                        return (string)drive["SerialNumber"];
                    }
                }
            }
            catch
            {
                return "<error>";
            }
        }
        else
        {
            return "<unsupported>";
        }
        return "<unknown>";
    }
    // Reimplementation of WinForms MessageBox.Show
    private Task<ButtonResult> MessageBoxShow(string message, string caption = "Markuse arvuti juurutamise tööriist", ButtonEnum buttons = ButtonEnum.Ok, Icon icon = MsBox.Avalonia.Enums.Icon.None)
    {
        var box = MessageBoxManager.GetMessageBoxStandard(caption, message, buttons, icon);
        var result = box.ShowWindowDialogAsync(this);
        return result;
    }
}