using System;
using System.IO;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BriefcaseTool;
using BriefcaseTool.Utils;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length >= 2)
        {

            ApplicationConfiguration.Initialize();

            string command = args[0];
            string TargetFolder = args[1].TrimEnd(Path.DirectorySeparatorChar);

            try
            {
                switch (command.ToLower())
                {
                    case "--init":
                    {
                        Briefcase.Init(TargetFolder);
                        ExplorerRefresher.RefreshExplorer(TargetFolder);
                        break;
                    }

                    case "--link":
                    {
                        LinkDatabase.Link(TargetFolder);
                        break;
                    }

                    case "--unlink":
                    {
                        LinkDatabase.Unlink(TargetFolder);
                        break;
                    }

                    case "--sync":
                    {
                        SyncEngine.Sync(TargetFolder);
                        LinkDatabase.VerifyDatabase();
                        break;
                    }

                    default:
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                BriefcaseTool.Utils.MessageBox.ShowError("Error: " + ex.Message);
            }
        }
    }
}