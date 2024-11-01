﻿using Octokit;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;

class Program
{
    static string WipeDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
        Directory.CreateDirectory(path);
        return path;
    }

    async static Task<string> GetGitHubRelease(string repositoryOwner, string repositoryName, string folderName, string regexAsset)
    {
        Stopwatch watch = Stopwatch.StartNew();

        GitHubClient gitHubClient = new(new ProductHeaderValue("CFWSetuper"));

        Release release = await gitHubClient.Repository.Release.GetLatest(repositoryOwner, repositoryName);
        foreach (ReleaseAsset releaseAsset in release.Assets)
        {
            if (Regex.IsMatch(releaseAsset.Name, regexAsset))
            {
                HttpClient httpClient = new();
                byte[] content = await httpClient.GetByteArrayAsync($"https://github.com/{repositoryOwner}/{repositoryName}/releases/download/{release.TagName}/{releaseAsset.Name}");
                string outputFile = Path.Combine(folderName, releaseAsset.Name);
                File.WriteAllBytes(outputFile, content);
                Console.WriteLine($"Downloaded {releaseAsset.Name} in {watch.ElapsedMilliseconds}ms");
                return outputFile;
            }
        }
        Debug.Assert(false);
        return "";
    }

    async static Task<string> GetFile(string link, string folderName, string filename)
    {
        Stopwatch watch = Stopwatch.StartNew();
        HttpClient httpClient = new();
        byte[] content = await httpClient.GetByteArrayAsync(link);
        string outputFile = Path.Combine(folderName, filename);
        File.WriteAllBytes(outputFile, content);
        Console.WriteLine($"Downloaded {filename} from {link} in {watch.ElapsedMilliseconds}ms");
        return outputFile;
    }

    static string UnzipFile(string filename)
    {
        Stopwatch watch = Stopwatch.StartNew();
        string filenameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
        string directory = Path.GetDirectoryName(filename)!;
        string unzipDestination = Path.Combine(directory, filenameWithoutExtension);
        ZipFile.ExtractToDirectory(filename, unzipDestination);
        Console.WriteLine($"Unziped {filename} in {watch.ElapsedMilliseconds}ms");
        return unzipDestination;
    }

    static void CopyDirectoryInternal(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        Directory.CreateDirectory(destinationDir);
        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        foreach (DirectoryInfo subDir in dir.GetDirectories())
        {
            string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectoryInternal(subDir.FullName, newDestinationDir);
        }
    }

    static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Stopwatch watch = Stopwatch.StartNew();
        CopyDirectoryInternal(sourceDir, destinationDir);
        Console.WriteLine($"Copied {sourceDir} to {destinationDir} in {watch.ElapsedMilliseconds}ms");
    }

    static void CopyFile(string sourceFilePath, string destinationDirectory)
    {
        Stopwatch watch = Stopwatch.StartNew();
        string filename = Path.GetFileName(sourceFilePath);
        string destinationFilePath = Path.Combine(destinationDirectory, filename);
        File.Copy(sourceFilePath, destinationFilePath);
        Console.WriteLine($"Copied {sourceFilePath} to {destinationDirectory} in {watch.ElapsedMilliseconds}ms");
    }

    async static Task Main()
    {
        string temp = WipeDirectory("temp");
        string root = WipeDirectory("CFW Switch");
        string hekateBin = WipeDirectory("hekate bin");

        //Using instructions from the link below. Beware, this is working only for full release of Atmosphère and *NOT* pre-release
        //https://switch.hacks.guide/user_guide/all/sd_preparation/
        //1. Navigate to the accessible drive.

        //2. Copy the contents of the Atmosphère.zip file to the root of your microSD card.
        {
            string zipPath = await GetGitHubRelease("Atmosphere-NX", "Atmosphere", temp, @"atmosphere-.*-master-.*\+hbl-.*\+hbmenu-.*.zip");
            string unzipPath = UnzipFile(zipPath);
            CopyDirectory(unzipPath, root);
        }

        //3. Copy the bootloader folder from the Hekate .zip file to the root of your microSD card.
        {
            string zipPath = await GetGitHubRelease("CTCaer", "Hekate", temp, @"hekate_ctcaer_.*.zip");
            string unzipPath = UnzipFile(zipPath);
            CopyDirectory(Path.Combine(unzipPath, "bootloader"), Path.Combine(root, "bootloader"));

            CopyFile(Path.Combine(unzipPath, "hekate_ctcaer_6.2.2.bin"), hekateBin);
        }

        //4. Copy the bootloader folder from the bootlogos.zip file to the root of your microSD card.
        {
            string zipPath = await GetFile("https://nh-server.github.io/switch-guide/files/bootlogos.zip", temp, "bootlogos.zip");
            string unzipPath = UnzipFile(zipPath);
            CopyDirectory(Path.Combine(unzipPath, "bootloader"), Path.Combine(root, "bootloader"));
        }

        //5. Copy hekate_ipl.ini to the bootloader folder on your microSD card.
        {
            string hekateConfigFilename = await GetFile("https://switch.hacks.guide/files/emu/hekate_ipl.ini", temp, "hekate_ipl.ini");
            CopyFile(hekateConfigFilename, Path.Combine(root, "bootloader"));
        }

        //6. Create a folder named hosts inside the atmosphere folder on your microSD card, and put emummc.txt inside of the hosts folder.
        // Skipped because I'm not doing emuMMC

        //7. Copy JKSV.nro, ftpd.nro, NxThemesInstaller.nro, NX-Shell.nro and Goldleaf.nro to the switch folder on your microSD card.
        {
            string jksvFilename = await GetGitHubRelease("J-D-K", "JKSV", temp, "JKSV.nro");
            string ftdpFilename = await GetGitHubRelease("mtheall", "ftpd", temp, "ftpd.nro");
            string themesInstallerFilename = await GetGitHubRelease("exelix11", "SwitchThemeInjector", temp, "NXThemesInstaller.nro");
            string shellFilename = await GetGitHubRelease("joel16", "NX-Shell", temp, "NX-Shell.nro");
            string goldLeafFilename = await GetGitHubRelease("XorTroll", "Goldleaf", temp, "Goldleaf.nro");
            CopyFile(jksvFilename, Path.Combine(root, "switch"));
            CopyFile(ftdpFilename, Path.Combine(root, "switch"));
            CopyFile(themesInstallerFilename, Path.Combine(root, "switch"));
            CopyFile(shellFilename, Path.Combine(root, "switch"));
            CopyFile(goldLeafFilename, Path.Combine(root, "switch"));
        }

        // Custom step add usb bot base
        {
            string zipPath = await GetGitHubRelease("zyro670", "usb-botbase", temp, "usb-botbaseZ.zip");
            string unzipPath = UnzipFile(zipPath);
            CopyDirectory(Path.Combine(unzipPath, "usb-botbaseZ"), Path.Combine(root, "atmosphere", "contents"));
        }

        //9. Reinsert your SD card back into your Switch
    }
}
