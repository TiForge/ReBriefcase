using System;
using System.IO;

namespace BriefcaseTool.Utils;

static class FileManager
{
    public static void CopyFileNoReserve(string destination, string source, string targetName)
    { 

        string sourceFile = Path.Combine(source, targetName);
        string destinationFile = Path.Combine(destination, targetName);

        // Check if target file exists
        if (!File.Exists(sourceFile))
        {
            BriefcaseTool.Utils.MessageBox.ShowError($"Failed to copy {sourceFile} to {destination}"); // Ideally the user will never see this error
            return;
        }

        File.Copy(sourceFile, destinationFile);
    }

    public static void CopyFileReserveMeta(string destination, string source, string targetName)
    {

        string sourceFile = Path.Combine(source, targetName);
        string destinationFile = Path.Combine(destination, targetName);

        // Check if target file exists
        if (!File.Exists(sourceFile))
        {
            BriefcaseTool.Utils.MessageBox.ShowError($"Failed to copy {sourceFile} to {destination}"); // Ideally the user will never see this error
            return;
        }

        // Create missing directories
        string? destDir = Path.GetDirectoryName(destinationFile);
        if (!string.IsNullOrEmpty(destDir))
        {
            Directory.CreateDirectory(destDir);

            var destInfo = new DirectoryInfo(destDir);

            Directory.SetCreationTime(destDir, destInfo.CreationTime);
            Directory.SetLastWriteTime(destDir, destInfo.LastAccessTime);
            Directory.SetLastAccessTime(destDir, destInfo.LastAccessTime);

            File.SetAttributes(destDir, destInfo.Attributes);
        }


        File.Copy(sourceFile, destinationFile, overwrite: true);

        var sourceInfo = new FileInfo(sourceFile);

        File.SetCreationTime(destinationFile, sourceInfo.CreationTime);
        File.SetLastWriteTime(destinationFile, sourceInfo.LastWriteTime);
        File.SetLastAccessTime(destinationFile, sourceInfo.LastAccessTime);

        File.SetAttributes(destinationFile, sourceInfo.Attributes);
    }

    public static byte[] GenerateHash(string filePath)
    {
        using var hash = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(filePath);
        return hash.ComputeHash(stream);
    }

}

public static class AppPaths
{
    public static readonly string BasePath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ReBriefcase"
        );

    public static readonly string DatabasePath =
        Path.Combine(BasePath, "Database");
}