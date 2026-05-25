using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BriefcaseTool.Utils;

namespace BriefcaseTool;

static partial class Briefcase
{
    public static void Init(string folder)
    {
        // Make metadata directory
        string metaDir = Path.Combine(folder, ".briefcase");

        // Check if metadirectory exists already
        if (Directory.Exists(metaDir))
        {
            BriefcaseTool.Utils.MessageBox.ShowError("The selected folder is already a briefcase!");
            return;
        }

        Directory.CreateDirectory(metaDir);
        File.SetAttributes(metaDir, File.GetAttributes(metaDir) | FileAttributes.Hidden | FileAttributes.System);

        // Create metadata file
        string metaFile = Path.Combine(metaDir, "metadata.json");
        
        var metaJson = new {
            ID = BriefcaseTool.Utils.IDManager.GenerateHexID(10),
            LinkPointer = ""
        };

        File.WriteAllText(metaFile, JsonSerializer.Serialize(metaJson, new JsonSerializerOptions { WriteIndented = true }));


        // Copy over briefcase icon from resources
        string resourcePath = Path.Combine(AppContext.BaseDirectory, "Resources");
        
        BriefcaseTool.Utils.FileManager.CopyFileNoReserve(folder, resourcePath, "icon.ico");

        string localIcon = Path.Combine(folder, "icon.ico");
        
        File.SetAttributes(localIcon, FileAttributes.Hidden | FileAttributes.System);

        // Apply icon
        string desktopIni = Path.Combine(folder, "desktop.ini");

        File.WriteAllText(desktopIni,
            $"""

            [.ShellClassInfo]
            IconResource=icon.ico,0
            """
        );

        File.SetAttributes(desktopIni, FileAttributes.Hidden | FileAttributes.System);

        // Mark folder as custom
        File.SetAttributes(folder, File.GetAttributes(folder) | FileAttributes.ReadOnly);

        RenameFolderToBriefcase(folder);
        
    }

    private static void RenameFolderToBriefcase(string folder)
    {
        string folderParent = Path.GetDirectoryName(folder)!;
        string folderName = Path.GetFileName(folder); 
        string newName = MyRegex().Replace(folderName, "briefcase");
        
        if (newName == folderName) 
        { 
            return; 
        } 
        
        //mimic Windows duplicate handling
        
        string candidateName = newName;
        
        string newPath = Path.Combine(folderParent, candidateName); 
        
        int i = 2; 
        
        while (Directory.Exists(newPath)) 
        { 
            candidateName = $"{newName} ({i})"; 
            newPath = Path.Combine(folderParent, candidateName); 
            i++; 
        } 
        
        Directory.Move(folder, newPath);
    }

    [GeneratedRegexAttribute("folder", RegexOptions.IgnoreCase, "en-US")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
}