using System;
using System.IO;
using System.Windows.Forms;

namespace TreeScan;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string? scanPath = null;
        if (args.Length > 0)
        {
            try
            {
                if (args.Length != 2 || args[0] != "--scan" || !Path.IsPathRooted(args[1]))
                    throw new ArgumentException();
                scanPath = Path.GetFullPath(args[1]);
            }
            catch (Exception error) when (error is ArgumentException or IOException
                or NotSupportedException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                MessageBox.Show("Geef een geldig volledig mappad op met --scan.", "TreeScan",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        Application.Run(new MainForm(scanPath));
    }
}
