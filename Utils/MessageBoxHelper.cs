using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Reflection;
using System.Runtime.InteropServices;
using System.DirectoryServices;
using System.Security.Policy;
using System.Drawing;
using System.Linq;

namespace BriefcaseTool.Utils;

static class MessageBox
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBoxW(
        IntPtr hWnd,
        string text,
        string caption,
        uint type
    );

    public static void ShowError(string message, string title = "Briefcase")
    {
        _ = MessageBoxW(
            IntPtr.Zero,
            message,
            title,
            0x00000010
        );
    }

    
    public static bool PromptYesNo(string message, string title = "Briefcase")
    {
        int result = MessageBoxW(
            IntPtr.Zero,
            message,
            title,
            0x00000004 | 0x00000020
        );

        return result == 6; // Yes
    }

    public static void ShowInfo(string message, string title = "Briefcase")
    {
        _ = MessageBoxW(
            IntPtr.Zero,
            message,
            title,
            0x00000040
        );
    }

    [STAThread]
    public static string? PromptFolder(IWin32Window? owner)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the briefcase to link to",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog(owner) != DialogResult.OK)
        {
            return null;
        }

        string selected = dialog.SelectedPath;

        return selected;
    }

    public static string PromptConflict(string message, string[] buttons, string filePath, string title = "Briefcase")
    {
        string result = "";
        Form form = new()
        {
            Text = title,
            BackColor = Color.White,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false
        };

        Label messageLabel = new() {
            Text = message,
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            Padding = new Padding(10),
        };
        form.Controls.Add(messageLabel);

        PictureBox iconBox = new();

        using (Icon icon = Icon.ExtractAssociatedIcon(filePath)!)
        {
            iconBox.Image = icon.ToBitmap();
        }

        iconBox.SizeMode = PictureBoxSizeMode.AutoSize;

        Label fileNameLabel = new()
        {
            Text = Path.GetFileName(filePath),
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Regular)
        };

        FlowLayoutPanel panel = new()
        {
            FlowDirection = FlowDirection.LeftToRight,
            Top = form.Height / 2,
            Height = 0,
            AutoSize = true,
            Padding = new Padding(10),
        };

        panel.Controls.Add(iconBox);
        panel.Controls.Add(fileNameLabel);

        form.Controls.Add(panel);

        int btnHeight = 35;
        int btnWidth = form.Width;
        int spacing = 5;
        int btnVPadding = (int)(form.Height / 1.25);

        Label questionLabel = new()
        {
            Text = "What would you like to do?",
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            Top = (form.Height / 3) * 2,
            Padding = new Padding(10),
        };
        form.Controls.Add(questionLabel);

        FlowLayoutPanel buttonsPanel = new()
        {
            FlowDirection = FlowDirection.TopDown,
            BackColor = Color.FromArgb(240, 240, 240),
            Top = btnVPadding + 20,
            Width = btnWidth,
            Height = 0,
            AutoSize = true,
            Padding = new Padding(10),
        };
        

        for (int i = 0; i < buttons.Length; i++)
        {
            Button btn = new()
            {
                Text = buttons[i],
                Height = btnHeight,
                Width = btnWidth,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
                Left = 3,
                Top = btnVPadding + i * (btnHeight + spacing),
                DialogResult = DialogResult.OK
            };
            btn.Click += (sender, e) => { result = btn.Text; form.Close(); };
            buttonsPanel.Controls.Add(btn);
        }

        form.Controls.Add(buttonsPanel);
        form.ShowDialog();

        iconBox.Image?.Dispose();
        form.Dispose();

        return result;
    }
}