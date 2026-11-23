using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;

namespace mas_install_tool;

public abstract class InstallTools
{
    private static string _statusText = "";
    private static readonly string? AssemblyName = Assembly.GetExecutingAssembly().GetName().Name;
    public static void RunSetup(MainWindow parent)
    {
        var r = new Random();
        Setup setup;
        Dispatcher.UIThread.Post(() =>
        {
            parent.Hide();
            setup = new Setup();
            setup.Show();
            setup.Closed += (_, _) =>
            {
                if (!setup.Result)
                {
                    parent.Close();
                    Environment.Exit(0);
                }
                else
                {
                    List<string> features = [];
                    var edition = (setup.ProRadioButton.IsChecked ?? false) ? "pro" :
                        (setup.PremiumRadioButton.IsChecked ?? false) ? "premium" : "basic";
                    var maia = setup.MaiaCheck.IsChecked ?? false;
                    if (setup.RmCheck.IsChecked ?? false) features.Add("RM");
                    if (setup.WxCheck.IsChecked ?? false) features.Add("WX");
                    if (setup.IpCheck.IsChecked ?? false) features.Add("IP");
                    if (setup.ItCheck.IsChecked ?? false) features.Add("IT");
                    if (setup.GpCheck.IsChecked ?? false) features.Add("GP");
                    if (setup.LtCheck.IsChecked ?? false) features.Add("LT");
                    if (setup.CsCheck.IsChecked ?? false) features.Add("CS");
                    if (setup.RdCheck.IsChecked ?? false) features.Add("RD");
                    if (setup.InfoCheck.IsChecked ?? false) features.Add("MM");
                    parent.MidProgress.IsIndeterminate = false;
                    parent.Show();
                    parent.TopLeftText.Content = "Etapp 2: Juurutamine";
                    parent.ToggleCenter(true);
                    _statusText = "Põhifailide lahtipakkimine";
                    parent.NoticeText.IsVisible = true;
                    parent.AnimateCenter();
                    new Thread(() =>
                    {
                        var masRoot = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".mas");
                        Directory.CreateDirectory(masRoot);
                        var coreFiles = AssetLoader.Open(new Uri($"avares://{AssemblyName}/Resources/core.npg"));
                        NpgTool.Unpack(coreFiles, masRoot);
                        coreFiles.Close();
                        _statusText = "Algsertifikaadi genereerimine";
                        using var sc = File.OpenText(Path.Join(masRoot, "verifile.kf"));
                        var securityCode = sc.ReadLine() ?? "";
                        sc.Close();
                        File.Delete(Path.Join(masRoot, "verifile.kf"));
                        Verifile.GenerateDummyCertificate(securityCode);
                        _statusText = "Versiooni info lahtipakkimine";
                        var versionFiles = AssetLoader.Open(new Uri($"avares://{AssemblyName}/Resources/version.npg"));
                        NpgTool.Unpack(versionFiles, Path.Join(masRoot, "temp"));
                        versionFiles.Close();
                        _statusText = "Versiooni info töötlemine";
                        File.Move(Path.Join(masRoot, "temp", "kbd.bmp"), Path.Join(masRoot, "kbd.bmp"));
                        var ultraWide = ((IClassicDesktopStyleApplicationLifetime?)Application.Current?.ApplicationLifetime)?.MainWindow?.Screens.Primary?.Bounds.Size.AspectRatio > 2.3;
                        File.Move(Path.Join(masRoot, "temp", "ls_" + (ultraWide ? "329" : "169") + ".png"), Path.Join(masRoot, "bg_login.png"));
                        File.Move(Path.Join(masRoot, "temp", "db_" + edition + "_" + (ultraWide ? "329" : "169") + ".png"), Path.Join(masRoot, "bg_desktop.png"));
                        File.Move(Path.Join(masRoot, "temp", "common.png"), Path.Join(masRoot, "bg_common.png"));
                        File.Copy(Path.Join(masRoot, "bg_desktop.png"), Path.Join(masRoot, "bg_uncommon.png"));
                        using var versionIni = File.OpenText(Path.Join(masRoot, "temp", "version.ini"));
                        var build = "0";
                        var version = "0";
                        var name = "Tundmatu";
                        while (!versionIni.EndOfStream)
                        {
                            var line = versionIni.ReadLine();
                            if (line == null) continue;
                            if (!line.Contains('=')) continue;
                            var firstToken = line.Split('=')[0].TrimEnd(" ").ToString();
                            var secondToken = line.Split('=')[1].TrimStart(" ").Trim("\"").ToString();
                            switch (firstToken.ToLowerInvariant())
                            {
                                case "build":
                                    build = secondToken;
                                    break;
                                case "version":
                                    version = secondToken;
                                    break;
                                case "codename":
                                    name = secondToken;
                                    break;
                            }
                        }
                        versionIni.Close();
                        _statusText = "Ajutiste failide kustutamine";
                        Directory.Delete(Path.Join(masRoot, "temp"), true);
                        _statusText = "Verifile sertifikaadi rakendamine";
                        Dispatcher.UIThread.Post(() => parent.MidProgress.IsIndeterminate = true);
                        new Thread(Verifile.UnlockVerifile).Start();
                        Thread.Sleep(5000);
                        File.Delete(Path.Join(masRoot, "edition.txt"));
                        using var editionTxt = File.CreateText(Path.Join(masRoot, "edition.txt"));
                        using var editionLegacyTxt = File.CreateText(Path.Join(masRoot, "edition_1.txt"));
                        editionTxt.WriteLine("[Edition_info]");
                        editionLegacyTxt.Write("[Edition_info];");
                        editionTxt.WriteLine(edition switch
                        {
                            "pro" => "Pro",
                            "premium" => "Premium",
                            _ => "Basic+"
                        });
                        editionLegacyTxt.Write(edition switch
                        {
                            "pro" => "Pro",
                            "premium" => "Premium",
                            _ => "Basic+"
                        } + ";");
                        editionTxt.WriteLine(version);
                        editionLegacyTxt.Write(version + ";");
                        editionTxt.WriteLine(name.ToCharArray()[0] + build + "a");
                        editionLegacyTxt.Write(name.ToCharArray()[0] + build + "a" + ";");
                        editionTxt.WriteLine("Yes");
                        editionLegacyTxt.Write("Yes;");
                        editionTxt.WriteLine(Environment.UserName);
                        editionLegacyTxt.Write(Environment.UserName + ";");
                        editionTxt.WriteLine(CultureInfo.InstalledUICulture);
                        editionLegacyTxt.Write(CultureInfo.InstalledUICulture + ";");
                        editionTxt.WriteLine(Environment.OSVersion.Version.Major + "." + Environment.OSVersion.Version.Minor);
                        editionLegacyTxt.Write(Environment.OSVersion.Version.Major + "." + Environment.OSVersion.Version.Minor + ";");
                        editionTxt.WriteLine(string.Join('-', features));
                        editionLegacyTxt.Write(string.Join('-', features) + ";");
                        var pin = r.Next(0, 9999).ToString().PadLeft(4, '0');
                        editionTxt.WriteLine(pin);
                        editionLegacyTxt.Write(pin + ";");
                        editionTxt.WriteLine(name);
                        editionLegacyTxt.Write(name + ";");
                        editionTxt.WriteLine("N/A");
                        editionLegacyTxt.Write("N/A;\n[this file is for backwards compatibility with legacy programs]");
                        editionTxt.Close();
                        editionLegacyTxt.Close();
                        using var dummyTxt = File.CreateText(Path.Join(masRoot, "vf2.done"));
                        dummyTxt.WriteLine();
                        dummyTxt.Close();
                        Thread.Sleep(5000);
                        Dispatcher.UIThread.Post(() => parent.MidProgress.IsIndeterminate = false);
                        _statusText = "Graafika lahtipakkimine";
                        var graphicFiles = AssetLoader.Open(new Uri($"avares://{AssemblyName}/Resources/graphics.npg"));
                        NpgTool.Unpack(graphicFiles, Path.Join(masRoot, "integration_data"));
                        graphicFiles.Close();
                        _statusText = "Failide töötlemine";
                        File.Copy(Path.Join(masRoot, "integration_data", "mas_general.png"), Path.Join(masRoot, "mas_general.png"));
                        Directory.CreateDirectory(Path.Join(masRoot, "mas_incons"));
                        File.Copy(Path.Join(masRoot, "integration_data", "mas_general.png"), Path.Join(masRoot, "mas_incons", "mas_general.png"));
                        Directory.CreateDirectory(Path.Join(masRoot, "ms_icons"));
                        Directory.CreateDirectory(Path.Join(masRoot, "notes"));
                        if (maia)
                        {
                            var maiaFiles = AssetLoader.Open(new Uri($"avares://{AssemblyName}/Resources/maia.npg"));
                            _statusText = "Veebiserveri failide lahtipakkimine";
                            NpgTool.Unpack(maiaFiles, Path.Join(masRoot, "maia"));
                            maiaFiles.Close();
                            string[] subDirs = ["css", "html_root", "images", "js"];
                            foreach (var sd in subDirs)
                            {
                                Directory.CreateDirectory(Path.Join(masRoot, "maia", sd));   
                            }
                            foreach (var fi in new DirectoryInfo(Path.Join(masRoot, "maia")).GetFiles())
                            {
                                foreach (var sd in subDirs)
                                {
                                    if (fi.Name.StartsWith(sd + "_"))
                                        File.Move(fi.FullName, Path.Join(masRoot, "maia", sd, fi.Name[(sd + "_").Length..]));
                                }
                            }

                            Directory.CreateDirectory(Path.Join(masRoot, "mas_neoglass"));
                            foreach (var fi in new DirectoryInfo(Path.Join(masRoot, "integration_data")).GetFiles())
                            {
                                if (fi.Name.StartsWith("mas_") && fi.Extension.EndsWith("png"))
                                {
                                    File.Copy(fi.FullName, Path.Join(masRoot, "mas_neoglass", fi.Name));
                                }
                            }
                            using var whitelist = File.CreateText(Path.Join(masRoot, "maia", "whitelist.txt"));
                            whitelist.Write("");
                            whitelist.Close();
                        }

                        _statusText = "Binraaride lahtipakkimine";
                        var binFiles = AssetLoader.Open(new Uri($"avares://{AssemblyName}/Resources/bin.npg"));
                        NpgTool.Unpack(binFiles, Path.Join(masRoot, "Markuse asjad"));
                        binFiles.Close();

                        _statusText = "Ajutiste failide kustutamine";
                        Dispatcher.UIThread.Post(() => parent.MidProgress.IsIndeterminate = true);
                        if (edition.Equals("basic"))
                        {
                            File.Delete(Path.Join(masRoot, "Markuse asjad", "Markuse arvuti integratsioonitarkvara"));
                            File.Delete(Path.Join(masRoot, "Markuse asjad", "AvIntegrationSoftware"));
                            Directory.Delete(Path.Join(masRoot, "integration_data"), true);
                        }
                        if (!features.Contains("IT"))
                        {
                            File.Delete(Path.Join(masRoot, "Markuse asjad", "Interaktiivne töölaud"));
                        }
                        Dispatcher.UIThread.Post(() => parent.MidProgress.IsIndeterminate = false);
                        if (OperatingSystem.IsWindows())
                        {
                            _statusText = "Windowsi failide lahtipakkimine";
                            var winFiles = AssetLoader.Open(new Uri($"avares://{AssemblyName}/Resources/windows.npg"));
                            NpgTool.Unpack(winFiles, Path.Join(masRoot));
                            winFiles.Close();
                        }

                        if (OperatingSystem.IsLinux())
                        {
                            _statusText = "Skriptide lahtipakkimine";
                            var scriptFiles = AssetLoader.Open(new Uri($"avares://{AssemblyName}/Resources/scripts.npg"));
                            NpgTool.Unpack(scriptFiles, Path.Join(masRoot, "temp"));
                            scriptFiles.Close();
                            var scriptsRoot =
                                Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scripts");
                            if (!Directory.Exists(scriptsRoot)) Directory.CreateDirectory(scriptsRoot);
                            foreach (var fi in new DirectoryInfo(Path.Join(masRoot, "temp")).GetFiles())
                            {
                                if (fi.Extension.EndsWith("sh"))
                                {
                                    File.Move(fi.FullName, Path.Join(scriptsRoot, fi.Name), true);
                                }
                            }
                            Directory.Delete(Path.Join(masRoot, "temp"), true);
                            foreach (var fi in new DirectoryInfo(Path.Join(masRoot, "Markuse asjad")).GetFiles())
                            {
                                if (fi.Name.Contains(".so", StringComparison.InvariantCultureIgnoreCase) ||
                                    fi.Name.Contains(".xml", StringComparison.InvariantCultureIgnoreCase)) continue;
                                File.SetUnixFileMode(fi.FullName, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
                            }
                        }

                        if (OperatingSystem.IsWindows())
                        {
                            var p = new Process();
                            p.StartInfo.UseShellExecute = false;
                            p.StartInfo.CreateNoWindow = true;
                            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                            p.StartInfo.FileName = "cmd.exe";
                            p.StartInfo.Arguments = "/C mklink /j " + Path.Join(Environment.GetEnvironmentVariable("HOMEDRIVE"), "mas") + " \"" +
                                                    masRoot + "\"";
                            p.Start();
                            p.WaitForExit();
                        }
                        parent.Finish();
                    }).Start();
                    new Thread(() =>
                    {
                        var broken = false;
                        while (!broken)
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                if (!parent.MidProgress.IsVisible)
                                {
                                    broken = true;
                                    return;
                                }
                                parent.MidProgress.Maximum = NpgTool.Count;
                                parent.MidProgress.Value = NpgTool.Progress;
                                parent.CenterText.Content = _statusText;
                            });
                            Thread.Sleep(100);
                        }
                    }).Start();
                }
            };
        });
        
    }
    
    public static bool CheckAuthority(string[] usbDevices)
    {
        return (from di in DriveInfo.GetDrives()
            where Directory.Exists(Path.Join(di.RootDirectory.FullName, "E_INFO"))
            where File.Exists(Path.Join(di.RootDirectory.FullName, "E_INFO", "ROOT_INF", "verify2.dat"))
            let passPhrase = File.ReadAllText(Path.Join(di.RootDirectory.FullName, "E_INFO", "edition.cmd")).ToLowerInvariant()
            let verifyData = File.ReadAllText(Path.Join(di.RootDirectory.FullName, "E_INFO", "ROOT_INF", "verify2.dat"))
            select StringCipher.Decrypt(verifyData, passPhrase))
                .Any(claimedSerial => usbDevices.Select(usbDevice => usbDevice.Split(';'))
                .Select(split => split[1])
                .Any(deviceSerial => deviceSerial.StartsWith(claimedSerial[..deviceSerial.Length]) && claimedSerial.Length > 8));
    }
    
     // krüpti/dekrüpti
    // https://stackoverflow.com/questions/10168240/encrypting-decrypting-a-string-in-c-sharp
    public static class StringCipher
    {
        // This constant is used to determine the keysize of the encryption algorithm in bits.
        // We divide this by 8 within the code below to get the equivalent number of bytes.
        private const int Keysize = 128;

        // This constant determines the number of iterations for the password bytes generation function.
        private const int DerivationIterations = 1000;

        public static string Encrypt(string plainText, string passPhrase)
        {
            // Salt and IV is randomly generated each time, but is preprended to encrypted cipher text
            // so that the same Salt and IV values can be used when decrypting.  
            var saltStringBytes = Generate128BitsOfRandomEntropy();
            var ivStringBytes = Generate128BitsOfRandomEntropy();
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            using (var password = new Rfc2898DeriveBytes(passPhrase, saltStringBytes, DerivationIterations))
            {
                var keyBytes = password.GetBytes(Keysize / 8);
                using (var symmetricKey = new RijndaelManaged())
                {
                    symmetricKey.BlockSize = 128;
                    symmetricKey.Mode = CipherMode.CBC;
                    symmetricKey.Padding = PaddingMode.PKCS7;
                    using (var encryptor = symmetricKey.CreateEncryptor(keyBytes, ivStringBytes))
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                            {
                                cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                                cryptoStream.FlushFinalBlock();
                                // Create the final bytes as a concatenation of the random salt bytes, the random iv bytes and the cipher bytes.
                                var cipherTextBytes = saltStringBytes;
                                cipherTextBytes = cipherTextBytes.Concat(ivStringBytes).ToArray();
                                cipherTextBytes = cipherTextBytes.Concat(memoryStream.ToArray()).ToArray();
                                memoryStream.Close();
                                cryptoStream.Close();
                                return Convert.ToBase64String(cipherTextBytes);
                            }
                        }
                    }
                }
            }
        }

        public static string Decrypt(string cipherText, string passPhrase)
        {
            // Get the complete stream of bytes that represent:
            // [32 bytes of Salt] + [16 bytes of IV] + [n bytes of CipherText]
            var cipherTextBytesWithSaltAndIv = Convert.FromBase64String(cipherText);
            // Get the saltbytes by extracting the first 16 bytes from the supplied cipherText bytes.
            var saltStringBytes = cipherTextBytesWithSaltAndIv.Take(Keysize / 8).ToArray();
            // Get the IV bytes by extracting the next 16 bytes from the supplied cipherText bytes.
            var ivStringBytes = cipherTextBytesWithSaltAndIv.Skip(Keysize / 8).Take(Keysize / 8).ToArray();
            // Get the actual cipher text bytes by removing the first 64 bytes from the cipherText string.
            var cipherTextBytes = cipherTextBytesWithSaltAndIv.Skip((Keysize / 8) * 2).Take(cipherTextBytesWithSaltAndIv.Length - ((Keysize / 8) * 2)).ToArray();

            using (var password = new Rfc2898DeriveBytes(passPhrase, saltStringBytes, DerivationIterations))
            {
                var keyBytes = password.GetBytes(Keysize / 8);
                using (var symmetricKey = new RijndaelManaged())
                {
                    symmetricKey.BlockSize = 128;
                    symmetricKey.Mode = CipherMode.CBC;
                    symmetricKey.Padding = PaddingMode.PKCS7;
                    using (var decryptor = symmetricKey.CreateDecryptor(keyBytes, ivStringBytes))
                    {
                        using (var memoryStream = new MemoryStream(cipherTextBytes))
                        {
                            using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                            {
                                var plainTextBytes = new byte[cipherTextBytes.Length];
                                var decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
                                memoryStream.Close();
                                cryptoStream.Close();
                                return Encoding.UTF8.GetString(plainTextBytes, 0, decryptedByteCount);
                            }
                        }
                    }
                }
            }
        }

        private static byte[] Generate128BitsOfRandomEntropy()
        {
            var randomBytes = new byte[16]; // 16 Bytes will give us 128 bits.
            using (var rngCsp = new RNGCryptoServiceProvider())
            {
                // Fill the array with cryptographically secure random bytes.
                rngCsp.GetBytes(randomBytes);
            }
            return randomBytes;
        }
    }
}