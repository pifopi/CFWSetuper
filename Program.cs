using Octokit;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

class Program
{
    async static Task<string> GetGitHubRelease(string repositoryOwner, string repositoryName, string regexAsset)
    {
        HttpClient httpClient = new();
        GitHubClient gitHubClient = new(new ProductHeaderValue("CFWSetuper"));

        Release release = await gitHubClient.Repository.Release.GetLatest(repositoryOwner, repositoryName);
        foreach (ReleaseAsset releaseAsset in release.Assets)
        {
            if (Regex.IsMatch(releaseAsset.Name, regexAsset))
            {
                byte[] content = await httpClient.GetByteArrayAsync($"https://github.com/{repositoryOwner}/{repositoryName}/releases/download/{release.TagName}/{releaseAsset.Name}");
                string outputFile = Path.Combine("temp", releaseAsset.Name);
                File.WriteAllBytes(outputFile, content);
                Console.WriteLine($"Downloaded {outputFile} from https://github.com/{repositoryOwner}/{repositoryName}");
                return outputFile;
            }
        }
        Debug.Assert(false);
        return "";
    }

    async static Task<string> GetFile(string link, string filename)
    {
        HttpClient httpClient = new();
        byte[] content = await httpClient.GetByteArrayAsync(link);
        string outputFile = Path.Combine("temp", filename);
        File.WriteAllBytes(outputFile, content);
        Console.WriteLine($"Downloaded {outputFile} from {link}");
        return outputFile;
    }

    async static Task<string> GetVpsRelease(string link, string filename)
    {
        HttpClient httpClient = new();
        HttpResponseMessage response = await httpClient.GetAsync(link, HttpCompletionOption.ResponseHeadersRead);
        string redirectedURL = response.RequestMessage.RequestUri.AbsoluteUri;
        int lastSlashIndex = redirectedURL.LastIndexOf('/');
        string latestVersion = redirectedURL.Substring(lastSlashIndex + 1);
        byte[] content = await httpClient.GetByteArrayAsync($"https://vps.suchmeme.nl/git/mudkip/Lockpick_RCM/releases/download/{latestVersion}/{filename}");
        string outputFile = Path.Combine("temp", filename);
        File.WriteAllBytes(outputFile, content);
        Console.WriteLine($"Downloaded {outputFile} from {link}");
        return outputFile;
    }

    static string UnzipFile(string filename)
    {
        string nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
        FileInfo file = new FileInfo(filename);
        string unzipDestination = Path.Combine(file.DirectoryName, nameWithoutExtension);
        ZipFile.ExtractToDirectory(filename, unzipDestination);
        Console.WriteLine($"Unziped {filename} to {unzipDestination}");
        return unzipDestination;
    }

    static void CopyDirectory(string sourceDir, string destinationDir, bool recursive = true)
    {
        var dir = new DirectoryInfo(sourceDir);
        DirectoryInfo[] dirs = dir.GetDirectories();
        Directory.CreateDirectory(destinationDir);
        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        foreach (DirectoryInfo subDir in dirs)
        {
            string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir, true);
        }
    }

    static void CopyFile(string sourceFile, string destinationDir)
    {
        FileInfo file = new FileInfo(sourceFile);
        string destinationFile = Path.Combine(destinationDir, file.Name);
        File.Copy(sourceFile, destinationFile);
        Console.WriteLine($"Copied {sourceFile} to {destinationFile}");
    }

    async static Task Main()
    {
        string temp = "temp";
        if (Directory.Exists(temp))
        {
            Directory.Delete(temp, true);
        }
        Directory.CreateDirectory(temp);

        string root = "CFW Switch";
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
        Directory.CreateDirectory(root);

        //1. Insert your Switch's SD card into your PC

        //2. Copy the contents of the Atmosphere .zip file to the root of your SD card
        {
            string zipPath = await GetGitHubRelease("Atmosphere-NX", "Atmosphere", @"atmosphere-.*-master-.*\+hbl-.*\+hbmenu-.*.zip");
            string unzipPath = UnzipFile(zipPath);
            CopyDirectory(unzipPath, root);
        }

        //3. Copy the bootloader folder from the Hekate .zip file to the root of your SD card
        {
            string zipPath = await GetGitHubRelease("CTCaer", "Hekate", @"hekate_ctcaer_.*.zip");
            string unzipPath = UnzipFile(zipPath);
            CopyDirectory(Path.Combine(unzipPath, "bootloader"), Path.Combine(root, "bootloader"));
        }

        //4. Copy the bootloader folder from the bootlogos.zip file to the root of your SD card
        {
            string zipPath = await GetFile("https://nh-server.github.io/switch-guide/files/bootlogos.zip", "bootlogos.zip");
            string unzipPath = UnzipFile(zipPath);
            CopyDirectory(Path.Combine(unzipPath, "bootloader"), Path.Combine(root, "bootloader"));
        }

        //5. Copy hekate_ipl.ini to the bootloader folder on your SD card
        {
            string hekateConfigFilename = await GetFile("https://nh-server.github.io/switch-guide/files/sys/hekate_ipl.ini", "hekate_ipl.ini");
            CopyFile(hekateConfigFilename, Path.Combine(root, "bootloader"));
        }

        //6. Copy Lockpick_RCM.bin to the /bootloader/payloads folder on your SD card
        {
            string lockPickFilename = await GetVpsRelease("https://vps.suchmeme.nl/git/mudkip/Lockpick_RCM/releases/latest", "Lockpick_RCM.bin");
            CopyFile(lockPickFilename, Path.Combine(root, "bootloader", "payloads"));
        }

        //7. Create a folder named appstore inside the switch folder on your SD card, and put appstore.nro in it
        {
            string appStoreFilename = await GetGitHubRelease("fortheusers", "hb-appstore", "appstore.nro");
            Directory.CreateDirectory(Path.Combine(root, "switch", "appstore"));
            CopyFile(appStoreFilename, Path.Combine(root, "switch", "appstore"));
        }

        //8. Copy JKSV.nro, ftpd.nro, NX-Shell.nro and NxThemesInstaller.nro to the switch folder on your SD card
        {
            string jksvFilename = await GetGitHubRelease("J-D-K", "JKSV", "JKSV.nro");
            string ftdpFilename = await GetGitHubRelease("mtheall", "ftpd", "ftpd.nro");
            string themesInstallerFilename = await GetGitHubRelease("exelix11", "SwitchThemeInjector", "NXThemesInstaller.nro");
            string shellFilename = await GetGitHubRelease("joel16", "NX-Shell", "NX-Shell.nro");
            CopyFile(jksvFilename, Path.Combine(root, "switch"));
            CopyFile(ftdpFilename, Path.Combine(root, "switch"));
            CopyFile(themesInstallerFilename, Path.Combine(root, "switch"));
            CopyFile(shellFilename, Path.Combine(root, "switch"));
        }

        // Custom step add usb bot base
        {
            string zipPath = await GetGitHubRelease("zyro670", "usb-botbase", "usb-botbaseZ.zip");
            string unzipPath = UnzipFile(zipPath);
            CopyDirectory(Path.Combine(unzipPath, "usb-botbaseZ"), Path.Combine(root, "atmosphere", "contents"));
        }

        //9. Reinsert your SD card back into your Switch
    }
}