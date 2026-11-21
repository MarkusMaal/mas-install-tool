using Avalonia;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LibUsbDotNet;
using LibUsbDotNet.Main;

namespace mas_install_tool;

internal abstract class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--help"))
        {
            Console.WriteLine("\nmas-install-tool\n");
            Console.WriteLine("--deflate-image [source]\t\t\tAllows you to create a .NPG image from a folder");
            Console.WriteLine("--inflate-image [source] [destination]\t\tAllows you to unpack .NPG image");
            Console.WriteLine("--collect-info\t\t\t\t\tDisplays all USB devices connected to the computer with their serial numbers");
            Console.WriteLine("--bless [flash drive root dir] [serial number]\tBlesses USB drive with verification data (requires Verifile to pass)");
            Console.WriteLine("--bless-check\t\t\t\t\tChecks if the device has been blessed correctly (Linux only)");
            return;
        }

        if (args.Contains("--deflate-image"))
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Not enough arguments");
                return;
            }
            NpgTool.Pack(args[args.IndexOf("--deflate-image") + 1]);
            return;
        }

        if (args.Contains("--inflate-image"))
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Not enough arguments");
                return;
            }
            NpgTool.Unpack(args[args.IndexOf("--inflate-image") + 1], args[args.IndexOf("--inflate-image") + 2]);
            return;
        }

        if (args.Contains("--collect-info"))
        {
            foreach (var device in UsbDevice.AllLibUsbDevices.FindAll(p => p.DeviceProperties.ContainsKey("SerialNumber") && p.DeviceProperties["SerialNumber"]?.ToString()?.Length > 0))
            {
                if (device is not LegacyUsbRegistry usbDevice ||
                    (string)usbDevice.DeviceProperties["SerialNumber"] == "") continue;
                Console.WriteLine(usbDevice.Device.Info.ProductString + ";" + usbDevice.DeviceProperties["SerialNumber"] + ";" + usbDevice.Device.Info.ManufacturerString);
            }
            Environment.Exit(0);
            return;
        }

        if (args.Contains("--bless"))
        {
            
            if (args.Length < 3)
            {
                Console.WriteLine("Not enough arguments");
                return;
            }
            
            var vf = new Verifile();
            if (!Verifile.CheckVerifileTamper() || !vf.MakeAttestation().Equals("VERIFIED"))
            {
                Console.WriteLine("Bless FAIL");
                return;
            }
            
            var rootDir = args[args.IndexOf("--bless") + 1];
            var output = Path.Join(rootDir, "E_INFO", "ROOT_INF", "verify2.dat");
            var passPhrase = File.ReadAllText(Path.Join(rootDir, "E_INFO", "edition.cmd")).ToLowerInvariant();
            File.WriteAllText(output, InstallTools.StringCipher.Encrypt(args[args.IndexOf("--bless") + 2], passPhrase));
            Console.WriteLine("Bless OK");
            return;
        }

        if (args.Contains("--bless-check"))
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
            var driveInfo = stdout.Split(Environment.NewLine);
            driveInfo = [.. driveInfo.Take(driveInfo.Length - 1)];
            Console.WriteLine("Result: " + (InstallTools.CheckAuthority(driveInfo) ? "OK" : "Failed"));
            return;
        }
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}