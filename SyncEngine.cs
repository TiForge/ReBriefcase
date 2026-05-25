using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.PeerToPeer.Collaboration;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Security.Policy;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Xml.Schema;
using Microsoft.VisualBasic.ApplicationServices;

namespace BriefcaseTool;

static class SyncEngine 
{
    public static void Sync(string folder)
    {
        BriefcaseTool.LinkData folderData = BriefcaseTool.LinkDatabase.GetLinkData(folder);

        // Check if a sync is possible
        if (folderData.LinkPointer == "")
        {
            BriefcaseTool.Utils.MessageBox.ShowError("The briefcase you selected is not part of a group");
            return;
        }
        else
        {
            string groupPath = Path.Combine(BriefcaseTool.Utils.AppPaths.DatabasePath, folderData.LinkPointer);

            if (!Directory.Exists(groupPath)) // Group does not exist
            {
                BriefcaseTool.Utils.MessageBox.ShowError("The group that this briefcase is connected to no longer exists in the briefcase database"); // Shouldn't see this at all, but on the off chance
                BriefcaseTool.LinkDatabase.WriteLinkPointer(folder, ""); // Fix the dead pointer
                return;
            }
            else // Group exists, check if this briefcase needs re-added to the group
            {
                string[] peerCountBeforeVerify = BriefcaseTool.LinkDatabase.ReadLinkFolderPeerData(folderData.LinkPointer).PeerIDs;
                BriefcaseTool.LinkDatabase.VerifyLinkFolder(folderData.LinkPointer); // Remove dead briefcases before attempting to check if this one needs re-added

                // Check if the verification deleted the group
                if (!Directory.Exists(groupPath))
                {
                    BriefcaseTool.Utils.MessageBox.ShowError("The group that this briefcase is connected to no longer exists in the briefcase database"); // Shouldn't see this at all, but on the off chance
                    BriefcaseTool.LinkDatabase.WriteLinkPointer(folder, ""); // Fix the dead pointer
                    return;
                }

                var linkData = BriefcaseTool.LinkDatabase.ReadLinkFolderPeerData(folderData.LinkPointer);

                if (!linkData.PeerIDs.ToList().Contains(folderData.ID)) // This briefcase does not exist in the group
                {
                    BriefcaseTool.LinkDatabase.AddToLinkFolder(folderData, folderData.LinkPointer);
                }
                else // This ID already exists, check if this briefcase is a duplicate
                {
                    if (!linkData.PeerLocations.ToList().Contains(folderData.Path)) // Briefcase is a duplicate
                    {
                        BriefcaseTool.LinkDatabase.WriteNewBriefcaseID(folder);
                        folderData = BriefcaseTool.LinkDatabase.GetLinkData(folder); // Update folderData
                    }
                }

                string[] peerCountAfterVerify = BriefcaseTool.LinkDatabase.ReadLinkFolderPeerData(folderData.LinkPointer).PeerIDs;

                if (peerCountBeforeVerify.Length > peerCountAfterVerify.Length)
                {
                    bool continueSync = BriefcaseTool.Utils.MessageBox.PromptYesNo("Some briefcases in this group were not found. The missing briefcases will not be included in the synchronization. Do you wish to sync anyways?");

                    if (!continueSync) // User said not to continue
                    {
                        return;
                    }
                }
            }
        }
    
        // Check if snapshot file exists
        string snapshotFile = Path.Combine(BriefcaseTool.Utils.AppPaths.DatabasePath, folderData.LinkPointer, "snapshot.json");
        
        if (File.Exists(snapshotFile))
        {
            RunSafeSync(folderData.Path, folderData.LinkPointer);
        }
        else
        {
            bool unsafeConfirmation = BriefcaseTool.Utils.MessageBox.PromptYesNo("There is no cached filesystem present in this brefcase\'s group. The sync engine will use a less safe sync method that can result in data loss. Do you wish to sync anyway?");
            if (unsafeConfirmation)
            {
                RunUnsafeSync(folderData.Path, folderData.LinkPointer);
            }
            else
            {
                return;
            }
        }

    }


    public static void RunUnsafeSync(string folder, string pointer) // No snapshot file present, prioritize larger files, data will most likely be lost
    {
        //      file name            peer ID      filedata
        Dictionary<string, Dictionary<string, FileSnapshot>> snapshotTable = BuildFilesystemTable(GetGroupSnapshots(pointer, folder)); // Should probably optimize this later, it takes a good few seconds to run

        foreach (var entry in snapshotTable) // Go through each file
        {
            string relativePath = entry.Key;
            Dictionary<string, FileSnapshot> versions = entry.Value;

            List<FileVersion> files = [];

            foreach (var peer in versions) // Go through each version
            {
                string peerID = peer.Key;
                FileSnapshot snapshot = peer.Value;

                files.Add(new FileVersion(peerID, snapshot));
            }

            var orderedFiles = files.OrderByDescending(i => i.Snapshot.Size).ToList();

            var prioritizedFile = orderedFiles[0].Snapshot.RelativePath;
            string? prioritizedPeer = BriefcaseTool.LinkDatabase.GetBriefcasePathByID(orderedFiles[0].PeerID, pointer);

            if (prioritizedPeer == null || prioritizedFile == null) // should never run due to the validation done before hand, and the snapshots being just made
            {
                BriefcaseTool.Utils.MessageBox.ShowError($"Failed to sync due to bad data being present in the group database");
                return;
            }

            string[] peers = BriefcaseTool.LinkDatabase.ReadLinkFolderPeerData(pointer).PeerLocations;

            DistributeFileToPeers(peers, prioritizedPeer, prioritizedFile);
        }

        WriteSnapshot(pointer, BuildSnapshot(folder)); // Write the synced snapshot to the database for future reference
        BriefcaseTool.Utils.MessageBox.ShowInfo("Sync completed");
    }

    public static void RunSafeSync(string folder, string pointer) // Snapshot file present. Compare file hashes to database and prompt when conflicts are found
    {

        // Get database snapshot
        string databaseSnapshotPath = Path.Combine(BriefcaseTool.Utils.AppPaths.DatabasePath, pointer, "snapshot.json");
        
        BriefcaseSnapshot databaseSnapshot = JsonSerializer.Deserialize<BriefcaseSnapshot>(File.ReadAllText(databaseSnapshotPath))!;
        
        // Generate snapshots and append database snapshot
        var snapshots = GetGroupSnapshots(pointer, folder);
        snapshots.Add(("-1", databaseSnapshot));

        // generate filesystem table
        Dictionary<string, Dictionary<string, FileSnapshot>> snapshotTable = BuildFilesystemTable(snapshots);

        bool Success = true;

        foreach (var entry in snapshotTable)
        {
            string path = entry.Key;
            Dictionary<string, FileSnapshot> versions = entry.Value;

            List<FileVersion> conflictingFiles = [];

            foreach (var peer in versions) // Go through each version
            {
                string peerID = peer.Key;
                FileSnapshot snapshot = peer.Value;
                
                versions.TryGetValue("-1", out var databaseVersion);

                if (databaseSnapshot == null) // database snapshot can't be found
                {
                    bool choice = BriefcaseTool.Utils.MessageBox.PromptYesNo("Failed to find the filesystem snapshot in the database. You can use a less safe sync method instead, but it can result in data loss. Do you wish to do so?");
                    
                    if (choice)
                    {
                        RunUnsafeSync(folder, pointer);
                    }

                    break;
                }

                if (peerID == "-1") // currently looking at the database snapshot
                {
                    break; // This is fine considering that the database snapshot will always be at the end of the list
                }

                if (Convert.ToBase64String(databaseVersion!.Hash!) != Convert.ToBase64String(snapshot.Hash!))
                {
                    
                    conflictingFiles.Add(new FileVersion(peerID, snapshot));
                }
            }

            switch (conflictingFiles.Count)
            {
                case 1: // Only one change
                {
                    string prioritizedFile = conflictingFiles[0].Snapshot.RelativePath!;
                    string? prioritizedPeer = BriefcaseTool.LinkDatabase.GetBriefcasePathByID(conflictingFiles[0].PeerID, pointer);

                    if (prioritizedPeer == null || prioritizedFile == null) // should never run due to the validation done before hand, and the snapshots being just made
                    {
                        BriefcaseTool.Utils.MessageBox.ShowError($"Failed to sync due to bad data being present in the group database");
                        return;
                    }

                    string[] peers = BriefcaseTool.LinkDatabase.ReadLinkFolderPeerData(pointer).PeerLocations;

                    DistributeFileToPeers(peers, prioritizedPeer, prioritizedFile);

                    break;
                }

                case >=2: // Two or more changes (the big scary case)
                {
                    string prioritizedFile = conflictingFiles[0].Snapshot.RelativePath!; // Doesn't matter where the path is gotten from since it's the same in each one

                    List<string> conflictingPeers = [];

                    foreach (var file in conflictingFiles)
                    {
                        if (file.PeerID == "-1")
                        {
                            continue;
                        }

                        var peerPath = BriefcaseTool.LinkDatabase.GetBriefcasePathByID(file.PeerID, pointer);

                        if (peerPath == null)
                        {
                            continue;
                        }

                        conflictingPeers.Add(peerPath);
                    }

                    string? prioritizedPeer = ThrowConflict(Path.Combine(folder, conflictingFiles[0].Snapshot.RelativePath!), conflictingPeers.ToArray());

                    if (String.IsNullOrEmpty(prioritizedPeer))
                    {
                        var orderedFiles = conflictingFiles.OrderByDescending(i => i.Snapshot.Size).ToList();
                        prioritizedPeer = BriefcaseTool.LinkDatabase.GetBriefcasePathByID(orderedFiles[0].PeerID, pointer); // Prioritize biggest file if no choice is made
                    }

                    if (prioritizedPeer == null || prioritizedFile == null) // should never run due to the validation done before hand, and the snapshots being just made
                    {
                        BriefcaseTool.Utils.MessageBox.ShowError($"Failed to sync due to bad data being present in the group database");
                        return;
                    }

                    string[] peers = BriefcaseTool.LinkDatabase.ReadLinkFolderPeerData(pointer).PeerLocations;

                    DistributeFileToPeers(peers, prioritizedPeer, prioritizedFile);

                    break;
                }

                default: // Skip the file if there are no changes
                {
                    break;
                }
            }
        }

        if (Success)
        {
            WriteSnapshot(pointer, BuildSnapshot(folder)); // Write the synced snapshot to the database for future reference
            BriefcaseTool.Utils.MessageBox.ShowInfo("Sync completed");
        }
        else
        {
            BriefcaseTool.Utils.MessageBox.ShowError("Sync failed");
        }
    }

    private static string Normalize(string p) => Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static void DistributeFileToPeers(string[] peers, string prioritizedPeer, string prioritizedFile)
    {
        foreach (string peer in peers) // For each peer
        {
            if (peer == null || peers.Length == 0) // if the peer doesn't exist
            {
                continue;
            }

            if (string.Equals(Normalize(peer), Normalize(prioritizedPeer), StringComparison.OrdinalIgnoreCase)) // If the peer is the prioritized peer
            {
                continue;
            }
            
            BriefcaseTool.Utils.FileManager.CopyFileReserveMeta(peer, prioritizedPeer, prioritizedFile);
        }
    }

    private static void WriteSnapshot(string pointer, BriefcaseSnapshot snapshot) // Needs snapshot data added as an argument, but I'll add this later
    {
        string snapshotFile = Path.Combine(BriefcaseTool.Utils.AppPaths.DatabasePath, pointer, "snapshot.json");

        File.WriteAllText(snapshotFile, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
    }
    
    private record FileVersion(string PeerID, FileSnapshot Snapshot);

    private static BriefcaseSnapshot BuildSnapshot(string folder)
    {
        BriefcaseSnapshot snapshot = new();
        int rootDepth = folder.Length + 1;

        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            var fileProperties = File.GetAttributes(file);
            var fileParentProperties = File.GetAttributes(Path.GetDirectoryName(file)!);

            if (fileProperties.HasFlag(FileAttributes.System) || fileParentProperties.HasFlag(FileAttributes.System)) // Don't sync items marked as system files (i.e. Briefcase data & icon)
            {
                continue;
            }

            snapshot.Files.Add(new FileSnapshot {
                RelativePath = file[rootDepth..],
                Hash = BriefcaseTool.Utils.FileManager.GenerateHash(file),
                Size = new FileInfo(file).Length
            });
        }

        return snapshot;
    }

    private static List<(string PeerID, BriefcaseSnapshot Snapshot)> GetGroupSnapshots(string pointer, string currentFolder) // Will return the selected briefcase snapshot in index 0
    {
        var result = new List<(string, BriefcaseSnapshot)>();

        var peers = BriefcaseTool.LinkDatabase.ReadLinkFolderPeerData(pointer);

        static string NormalizePath(string p) => Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        for (int i = 0; i < peers.PeerLocations.Length; i++)
        {

            if (!Directory.Exists(peers.PeerLocations[i]))
            {
                continue;
            }

            var entry = (
                peers.PeerIDs[i],
                BuildSnapshot(peers.PeerLocations[i])
            );

            if (string.Equals(NormalizePath(peers.PeerLocations[i]), NormalizePath(currentFolder), StringComparison.OrdinalIgnoreCase))
            {
                result.Insert(0, entry);
            }  
            else
            {
                result.Add(entry);
            }
        }

        return result;
    }

    private static Dictionary<string, Dictionary<string, FileSnapshot>> BuildFilesystemTable(List<(string PeerID, BriefcaseSnapshot Snapshot)> snapshots)
    {
        var filesystemTable = new Dictionary<string, Dictionary<string, FileSnapshot>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (peerID, snapshot) in snapshots)
        {
            foreach (var file in snapshot.Files)
            {
                if (!filesystemTable.TryGetValue(file.RelativePath!, out var row))
                {
                    row = new Dictionary<string, FileSnapshot>();
                    filesystemTable[file.RelativePath!] = row;
                }

                row[peerID] = file;
            }
        }

        return filesystemTable;
    }

    private static string? ThrowConflict(string filePath, string[] relevantPeers)
    {
        string choice = BriefcaseTool.Utils.MessageBox.PromptConflict(
            "A Conflict has been found between two or more peers",
            relevantPeers,
            filePath
        );

        return choice;
    }
}

class FileSnapshot
{
    public string? RelativePath { get; set; } = "";
    public byte[]? Hash { get; set; } = [];
    public long? Size { get; set; } = 0;
    
}

class BriefcaseSnapshot
{
    public List<FileSnapshot> Files { get; set; } = [];
}