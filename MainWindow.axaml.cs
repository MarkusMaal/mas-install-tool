using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LibUsbDotNet;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;
using SFML.Audio;
using Usb.Net;
using UsbDevice = Usb.Net.UsbDevice;

namespace mas_install_tool;

public partial class MainWindow : Window
{
    private Music? _mp;
    private readonly string _bgmFile = Path.GetTempFileName();
    private bool _animate;
    private readonly string? _assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
    private readonly string[]? _driveInfo;
    
    public MainWindow()
    {
        if (OperatingSystem.IsWindows()) // uses WMIC (System.Management)
        {
            foreach (var drv in DriveInfo.GetDrives())
            {
                if (drv.DriveType == DriveType.Removable)
                    _driveInfo = [$";{GetSerialFromDrive(drv.RootDirectory.FullName)};"];
            }
        }

        if (OperatingSystem.IsLinux()) // uses LibUsbDotNet
        {
            var commandLine = Environment.ProcessPath;
            if (commandLine == null) return;
            var p = new Process()
            {
                StartInfo = new ProcessStartInfo("pkexec", commandLine + " --collect-info") // prompts the user for root permissions
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
            Thread.Sleep(2500);
            Dispatcher.UIThread.Post(() =>
            {
                var str = AssetLoader.Open(new Uri($"avares://{_assemblyName}/Resources/root_idc_key.png"));
                CenterImage.Source = new Bitmap(str);
                CenterText.Content = "Andmete dekrüpteerimine";
            });
            do
            {
                Thread.Sleep(100);
            } while (!InstallTools.CheckAuthority(_driveInfo ?? []));

            Dispatcher.UIThread.Post(() =>
            {
                CenterImage.IsVisible = false;
                CenterText.IsVisible = false;
            });
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
        if (File.Exists(_bgmFile))
            File.Delete(_bgmFile);
    }

    private void AnimateCenter()
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
                        CenterImage.Source = new Bitmap(ao);
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

    private string GetSerialFromDrive(string mount)
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
}