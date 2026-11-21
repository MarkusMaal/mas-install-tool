using Avalonia;
using System;

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
            Console.WriteLine("--deflate-image [source]\t\tAllows you to create a .NPG image from a folder");
            Console.WriteLine("--inflate-image [source] [destination]\tAllows you to unpack .NPG image");
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