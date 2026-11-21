using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SFML.Audio;

namespace mas_install_tool;

public partial class MainWindow : Window
{
    private Music? _mp;
    private readonly string _bgmFile = Path.GetTempFileName();
    private bool _animate;
    private readonly string? _assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
    
    public MainWindow()
    {
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
}