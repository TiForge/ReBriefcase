using System.IO;
using System.Runtime.InteropServices;

namespace BriefcaseTool.Utils;

static class ExplorerRefresher
{

    const uint SHCNE_UPDATEITEM     = 0x00002000;
    const uint SHCNE_UPDATEDIR      = 0x00001000;
    const uint SHCNE_ASSOCCHANGED   = 0x08000000;

    const uint SHCNF_PATHW          = 0x0005;
    const uint SHCNF_IDLIST         = 0x0000;


    public static void RefreshExplorer(string path)
    {
        path = Path.GetFullPath(path);

        // Update the item itself
        SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW, path, null);

        // Update the directory view that contains this item
        string? parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, parent, null);
        }

        // Forces icon / overlay / desktop.ini refresh
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, null, null);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern void SHChangeNotify(
        uint wEventId,
        uint uFlags,
        string? dwItem1,
        string? dwItem2
    );
}