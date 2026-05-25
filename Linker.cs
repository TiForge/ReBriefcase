using System;
using System.IO;
using System.Text.Json;
using BriefcaseTool.Utils;
using System.Linq;
using System.Collections.Generic;
using System.Security.Policy;
using System.Windows.Forms;
using System.Runtime.InteropServices.Marshalling;
using System.Net;
using System.Configuration;
using System.Xml.Schema;

namespace BriefcaseTool;

static class LinkDatabase
{
    [STAThread]
    public static void Link(string folder)
    {
        string? peerFolder = BriefcaseTool.Utils.MessageBox.PromptFolder(null);

        // Check if peer path is valid
        if (peerFolder != null)
        {
            string briefcaseMetaDir = Path.Combine(peerFolder, ".briefcase");
            
            if (!Directory.Exists(briefcaseMetaDir))
            {
                BriefcaseTool.Utils.MessageBox.ShowError("The folder you selected is not a briefcase");
                return;
            }
        }
        else
        {
            return;
        }

        if (peerFolder == folder)
        {
            BriefcaseTool.Utils.MessageBox.ShowError("You cannot connect a briefcase to itself");
            return;
        }

        // Peer path is valid, check case
        string folderPointer = GetLinkData(folder).LinkPointer;
        string peerPointer = GetLinkData(peerFolder).LinkPointer;

        switch (folderPointer, peerPointer)
        {
            case ("", ""): // Both do not have pointer
            {
                CreateLinkFolder(folder, peerFolder);
                break;
            }
            
            case ("", var p2) when !string.IsNullOrEmpty(p2): // peer has pointer, add current
            {
                LinkData folderData = GetLinkData(folder);
                AddToLinkFolder(folderData, peerPointer);
                VerifyLinkFolder(peerPointer);
                break;
            }
            
            case (var p1, "") when !string.IsNullOrEmpty(p1): // current has pointer, add peer
            {
                LinkData peerFolderData = GetLinkData(peerFolder);
                AddToLinkFolder(peerFolderData, folderPointer);
                VerifyLinkFolder(folderPointer);
                break;
            }

            case (var p1, var p2) when !string.IsNullOrEmpty(p1) && !string.IsNullOrEmpty(p2): // both have pointers
            {
                if (folderPointer == peerPointer)
                {
                    BriefcaseTool.Utils.MessageBox.ShowError("The briefcases selected are already linked");
                    break;
                }
                else
                {
                    BriefcaseTool.Utils.MessageBox.ShowError("Both briefcases are already a part of a group");
                    break;
                }
            }
        }
    }

    public static void Unlink(string folder)
    {
        LinkData folderData = GetLinkData(folder);
        RemoveFromLinkFolder(folderData, folderData.LinkPointer);
        VerifyLinkFolder(folderData.LinkPointer);
    }

    private static void CreateLinkFolder(string folder, string peerFolder)
    {
        // Check if database folder exists (Building does not copy it due to being empty)
        string databaseDir = Path.Combine(BriefcaseTool.Utils.AppPaths.DatabasePath);

        if (!Directory.Exists(databaseDir))
        {
            Directory.CreateDirectory(databaseDir);
        }
        
        // Create the link folder
        string ID = BriefcaseTool.Utils.IDManager.GenerateHexID(20);
        
        string linkDir = Path.Combine(databaseDir, ID);

        while (Directory.Exists(linkDir)) //odds of this firing is basically 0, but just to be completely safe
        {
            ID = IDManager.GenerateHexID(20);
            linkDir = Path.Combine(BriefcaseTool.Utils.AppPaths.DatabasePath, ID);
        }

        Directory.CreateDirectory(linkDir);

        // Create the peer list file
        
        string linkFile = Path.Combine(linkDir, "peersInfo.json");

        LinkData folderData = GetLinkData(folder);
        LinkData peerData = GetLinkData(peerFolder);

        // handle duplicate IDs before writing

        if (peerData.ID == folderData.ID)
        {
            WriteNewBriefcaseID(peerFolder); // Give peer folder a new briefcase ID
            peerData = GetLinkData(peerFolder); // refresh peerData
        }

        // Write peer list file

        var linkData = new {
            PeerIDs = new string[] {folderData.ID, peerData.ID},
            PeerLocations = new string[] {folderData.Path, peerData.Path}
        };

        File.WriteAllText(linkFile, JsonSerializer.Serialize(linkData, new JsonSerializerOptions { WriteIndented = true}));

        // Write the link pointer to the briefcases metadata

        WriteLinkPointer(folder, ID);
        WriteLinkPointer(peerFolder, ID);

    }

    public static void AddToLinkFolder(LinkData newFolderData, string pointer)
    {
        string linkFolderPath = Path.Combine(BriefcaseTool.Utils.AppPaths.DatabasePath, pointer);
        string linkFile = Path.Combine(linkFolderPath, "peersInfo.json");
        string snapshotFile = Path.Combine(linkFolderPath, "snapshot.json");

        // Get current data
        string json = File.ReadAllText(linkFile);

        LinkPeerFileData oldData = JsonSerializer.Deserialize<LinkPeerFileData>(json)!;

        // Update the data
        var updatedPeerIDs = oldData.PeerIDs.ToList();
        var updatedPeerLocations = oldData.PeerLocations.ToList();

        updatedPeerIDs.Add(newFolderData.ID);
        updatedPeerLocations.Add(newFolderData.Path);

        // handle duplicate IDs before writing

        if (updatedPeerIDs.ToList().Contains(newFolderData.ID)) // This briefcase ID already exists
        {
            if (!updatedPeerLocations.ToList().Contains(newFolderData.Path)) // Briefcase is a duplicate
            {
                WriteNewBriefcaseID(newFolderData.Path);
            }
        }

        // Apply the updated data
        var newData = new LinkPeerFileData
        {
            PeerIDs = updatedPeerIDs.ToArray(),
            PeerLocations = updatedPeerLocations.ToArray()
        };

        File.WriteAllText(linkFile, JsonSerializer.Serialize(newData, new JsonSerializerOptions { WriteIndented = true}));

        // Write the link pointer to the new briefcase
        WriteLinkPointer(newFolderData.Path, pointer);

    }

    private static void RemoveFromLinkFolder(LinkData folderData, string pointer)
    {
        string linkFolderPath = Path.Combine(BriefcaseTool.Utils.AppPaths.DatabasePath, pointer);
        string linkFile = Path.Combine(linkFolderPath, "peersInfo.json");

        // Get current data
        string json = File.ReadAllText(linkFile);

        LinkPeerFileData oldData = JsonSerializer.Deserialize<LinkPeerFileData>(json)!;

        // Update the data
        var updatedPeerIDs = oldData.PeerIDs.ToList();
        var updatedPeerLocations = oldData.PeerLocations.ToList();

        int index = updatedPeerIDs.IndexOf(folderData.ID);
        if (index >= 0)
        {
            updatedPeerIDs.RemoveAt(index);
            updatedPeerLocations.RemoveAt(index);
        }

        // Apply the updated data
        var newData = new LinkPeerFileData
        {
            PeerIDs = updatedPeerIDs.ToArray(),
            PeerLocations = updatedPeerLocations.ToArray()
        };

        File.WriteAllText(linkFile, JsonSerializer.Serialize(newData, new JsonSerializerOptions { WriteIndented = true}));

        // Erase pointer from briefcase metadata
        WriteLinkPointer(folderData.Path, "");
    }

    public static LinkData GetLinkData(string folder)
    {
        
        string metaFile = Path.Combine(folder, ".briefcase", "metadata.json");

        string json = File.ReadAllText(metaFile);

        BriefcaseMetadata meta = JsonSerializer.Deserialize<BriefcaseMetadata>(json)!;

        return new LinkData {
            ID = meta.ID,
            LinkPointer = meta.LinkPointer,
            Path = folder
        };
    }

    public static string? GetBriefcasePathByID(string ID, string pointer)
    {
        LinkPeerFileData group = ReadLinkFolderPeerData(pointer);

        if (!group.PeerIDs.Contains(ID))
        {
            return null; // Couldn't find ID
        }

        int pathIndex = Array.IndexOf(group.PeerIDs, ID);

        return group.PeerLocations[pathIndex];
    }

    public static void WriteLinkPointer(string folder, string pointer)
    {
        
        string metaFile = Path.Combine(folder, ".briefcase", "metadata.json");

        string json = File.ReadAllText(metaFile);

        var meta = JsonSerializer.Deserialize<BriefcaseMetadata>(json)!;
        meta.LinkPointer = pointer;

        File.WriteAllText(metaFile, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true}));
    }

    public static void WriteNewBriefcaseID(string folder)
    {
        
        string metaFile = Path.Combine(folder, ".briefcase", "metadata.json");

        string json = File.ReadAllText(metaFile);

        var meta = JsonSerializer.Deserialize<BriefcaseMetadata>(json)!;
        meta.ID = BriefcaseTool.Utils.IDManager.GenerateHexID(10);

        File.WriteAllText(metaFile, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true}));
    }

    public static void VerifyLinkFolder(string pointer)
    {
        string linkFolderPath = Path.Combine(BriefcaseTool.Utils.AppPaths.DatabasePath, pointer);
        string linkFile = Path.Combine(linkFolderPath, "peersInfo.json");

        // Check if link file exists
        if (!File.Exists(linkFile))
        {
            if (Directory.Exists(linkFolderPath))
            {
                Directory.Delete(linkFolderPath, true);
            }

            return;
        }

        // Get current data
        LinkPeerFileData oldData = ReadLinkFolderPeerData(pointer);

        // Check data for ID/Path length missmatch
        if (oldData.PeerIDs.Length != oldData.PeerLocations.Length)
        {
            BriefcaseTool.Utils.MessageBox.ShowError("The currently selected briefcase\'s link database is corrupted. This does not affect your files, but you will need to relink this briefcase to it\'s peers");
            Directory.Delete(linkFolderPath, true);
            return;
        }
        
        // Extract the data
        var peerIDs = oldData.PeerIDs.ToList();
        var peerLocations = oldData.PeerLocations.ToList();

        // Go through the data and weed out or fix dead briefcases
        for (int i = peerLocations.Count - 1; i >= 0; i--)
        {
            if (Directory.Exists(peerLocations[i]))
            {
                continue;
            }

            string? newPath = AttemptBriefcaseRelocation(peerIDs[i], peerLocations[i], pointer);

            if (newPath != null)
            {
                peerLocations[i] = newPath;
            }
            else
            {
                peerLocations.RemoveAt(i);
                peerIDs.RemoveAt(i);
            }
        }

        // Delete the link folder if there is at most one briefcase connected
        if (peerIDs.Count <= 1)
        {
            Directory.Delete(linkFolderPath, true);
            return;
        }

        // Apply the updated data
        var newData = new LinkPeerFileData
        {
            PeerIDs = peerIDs.ToArray(),
            PeerLocations = peerLocations.ToArray()
        };

        File.WriteAllText(linkFile, JsonSerializer.Serialize(newData, new JsonSerializerOptions { WriteIndented = true}));
    }

    public static void VerifyDatabase() // Run a deep scan of the entire database (used for finding completely dead groups)
    {
        string[] groups = Directory.GetDirectories(Path.Combine(BriefcaseTool.Utils.AppPaths.DatabasePath));

        foreach(string group in groups)
        {
            string pointer = new DirectoryInfo(group).Name;
            VerifyLinkFolder(pointer);
        }
    }

    private static string? AttemptBriefcaseRelocation(string ID, string deadPath, string pointer)
    {

        string? briefcaseParent = Directory.GetParent(deadPath)?.FullName;
        string knownBriefcaseName = new DirectoryInfo(deadPath).Name;

        if (briefcaseParent == null)
        {
            return null;
        }

        string[]? parentChildren;
        
        try
        {
            parentChildren = Directory.GetDirectories(briefcaseParent);
        }
        catch
        {
            return null;
        }

        // Attempt relocation
        foreach (string child in parentChildren)
        {
            string briefcaseMetaDir = Path.Combine(child, ".briefcase");
            string briefcaseMeta = Path.Combine(briefcaseMetaDir, "metadata.json");

            string folderName = new DirectoryInfo(child).Name;

            if (Directory.Exists(briefcaseMetaDir) && File.Exists(briefcaseMeta)) // Briefcase metadata found
            {
                return SearchDirectory(child, ID, briefcaseMeta, deadPath, pointer);
            }

            else if (folderName == knownBriefcaseName) // Folder with the same name as dead briefcase found
            {
                string[]? grandparentChildren;
                
                try
                {
                    grandparentChildren = Directory.GetDirectories(child);
                }
                catch
                {
                    continue;
                }

                foreach (string grandchild in grandparentChildren)
                {
                    briefcaseMetaDir = Path.Combine(grandchild, ".briefcase");
                    briefcaseMeta = Path.Combine(briefcaseMetaDir, "metadata.json");
                    
                    if (Directory.Exists(briefcaseMetaDir) && File.Exists(briefcaseMeta)) // Briefcase metadata found
                    {
                        return SearchDirectory(grandchild, ID, briefcaseMeta, deadPath, pointer);
                    }
                }
            }
        }

        return null;
    }

    private static string? SearchDirectory(string folder, string ID, string briefcaseMeta, string deadPath, string pointer)
    {
        string json;
        BriefcaseMetadata? meta;

        static string Normalize(string p) => Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        try
        {
            json = File.ReadAllText(briefcaseMeta);
            meta = JsonSerializer.Deserialize<BriefcaseMetadata>(json)!;
        }
        catch
        {
            return null; // bad data
        }

        if (meta.ID == ID)
        {

            LinkPeerFileData data = ReadLinkFolderPeerData(pointer);
            
            int? replaceIndex = null;

            //find dead path index
            for (int i = 0; i < data.PeerLocations.Length; i++)
            {
                if (string.Equals(Normalize(data.PeerLocations[i]), Normalize(deadPath), StringComparison.OrdinalIgnoreCase) && data.PeerIDs[i] == ID)
                {
                    replaceIndex = i;
                    break;
                }
            }

            if (replaceIndex != null)
            {
                data.PeerLocations[(int)replaceIndex] = folder;
            }

            string linkFile = Path.Combine(BriefcaseTool.Utils.AppPaths.DatabasePath, pointer, "peersInfo.json");

            File.WriteAllText(linkFile, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true}));

            return folder;
        }

        return null;
    }

    public static LinkPeerFileData ReadLinkFolderPeerData(string pointer)
    {
        string linkFolderPath = Path.Combine(BriefcaseTool.Utils.AppPaths.DatabasePath, pointer);
        string linkFile = Path.Combine(linkFolderPath, "peersInfo.json");

        // Get current data
        string json = File.ReadAllText(linkFile);

        return JsonSerializer.Deserialize<LinkPeerFileData>(json)!;

    }

}

class BriefcaseMetadata
{
    public string ID { get; set; } = "";
    public string LinkPointer { get; set; } = "";
}

public sealed class LinkPeerFileData
{
    public string[] PeerIDs { get; set; } = [];
    public string[] PeerLocations { get; set; } = [];
}

public sealed class LinkData
{
    public string ID { get; set; } = "";
    public string LinkPointer { get; set; } = "";
    public string Path { get; set; } = "";
}