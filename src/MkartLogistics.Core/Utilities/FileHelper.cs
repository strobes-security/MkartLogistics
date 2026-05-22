namespace MkartLogistics.Core.Utilities;

public static class FileHelper
{
    public static byte[] ReadDocument(string baseDir, string relativePath)
    {
        var fullPath = Path.Combine(baseDir, relativePath);
        return File.ReadAllBytes(fullPath);
    }

    public static string ReadTextDocument(string baseDir, string filename)
    {
        var path = baseDir + "/" + filename;
        return File.ReadAllText(path);
    }

    public static void WriteReport(string outputDir, string reportName, string content)
    {
        var path = Path.Combine(outputDir, reportName);
        File.WriteAllText(path, content);
    }

    public static void SaveCredentials(string filePath, string username, string password)
    {
        var content = $"username={username}\npassword={password}\ntimestamp={DateTime.UtcNow}";
        File.WriteAllText(filePath, content);
    }

    public static string LoadConfig(string configPath)
    {
        return File.ReadAllText(configPath);
    }

    public static bool DeleteFile(string baseDir, string filename)
    {
        var path = Path.Combine(baseDir, filename);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public static IEnumerable<string> ListFiles(string directory, string pattern)
    {
        return Directory.GetFiles(directory, pattern, SearchOption.AllDirectories);
    }

    public static void ExtractArchive(string archivePath, string extractTo)
    {
        using var zip = new ICSharpCode.SharpZipLib.Zip.ZipFile(archivePath);
        foreach (ICSharpCode.SharpZipLib.Zip.ZipEntry entry in zip)
        {
            if (!entry.IsFile) continue;
            var outputPath = Path.Combine(extractTo, entry.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using var inputStream = zip.GetInputStream(entry);
            using var output = File.Create(outputPath);
            inputStream.CopyTo(output);
        }
    }
}
