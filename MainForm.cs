using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TreeScan;

internal sealed class MainForm : Form
{
    private static readonly Color Ink = Color.FromArgb(22, 40, 54);
    private static readonly Color Muted = Color.FromArgb(76, 95, 110);
    private static readonly Color Accent = Color.FromArgb(0, 104, 100);
    private static readonly Color Canvas = Color.FromArgb(237, 242, 246);

    private readonly ComboBox drives = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly Button scan = new() { Text = "&Scannen", AutoSize = true };
    private readonly Button cancel = new() { Text = "&Stoppen", AutoSize = true, Enabled = false };
    private readonly DirectoryGrid tree = new();
    private readonly Label emptyTree = new() { TextAlign = ContentAlignment.MiddleCenter, ForeColor = Muted };
    private readonly Label status = new() { AutoEllipsis = true, ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ProgressBar activity = new() { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30, Visible = false };
    private readonly string? launchPath;
    private CancellationTokenSource? activeScan;
    private bool closing;

    public MainForm(string? launchPath)
    {
        this.launchPath = launchPath;
        Text = "TreeScan";
        Font = new Font("Segoe UI", 10F);
        ForeColor = Ink;
        BackColor = Canvas;
        ClientSize = new Size(1020, 670);
        MinimumSize = new Size(820, 420);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        BuildLayout();

        drives.SelectionChangeCommitted += async (_, _) => await ScanSelectedDrive();
        scan.Click += async (_, _) => await ScanSelectedDrive();
        cancel.Click += (_, _) => CancelScan();
        RefreshDrives();
        if (launchPath is not null)
        {
            drives.SelectedIndex = drives.FindStringExact(launchPath);
            Shown += async (_, _) => await ScanLocation(launchPath);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (!e.Cancel)
        {
            closing = true;
            activeScan?.Cancel();
        }
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        Controls.Add(layout);

        var header = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = Ink };
        header.Controls.Add(new Label
        {
            Text = "TreeScan", ForeColor = Color.White, AutoSize = true,
            Font = new Font(Font.FontFamily, 24, FontStyle.Bold), Location = new Point(20, 9)
        });
        header.Controls.Add(new Label
        {
            Text = "Schijfruimte per map en bestand", ForeColor = Color.FromArgb(193, 218, 225),
            AutoSize = true, Location = new Point(24, 57)
        });
        layout.Controls.Add(header, 0, 0);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(20, 12, 20, 0), WrapContents = false
        };
        var driveLabel = new Label { Text = "&Locatie", AutoSize = true, Margin = new Padding(0, 8, 9, 0) };
        var refresh = new Button { Text = "&Vernieuwen", AutoSize = true };
        refresh.Click += (_, _) => RefreshDrives();
        drives.AccessibleName = "Beschikbare scanlocaties";
        drives.Margin = new Padding(0, 5, 10, 0);
        StyleButton(scan, primary: true);
        StyleButton(cancel, primary: false);
        StyleButton(refresh, primary: false);
        driveLabel.TabIndex = 0;
        drives.TabIndex = 1;
        toolbar.Controls.AddRange(new Control[] { driveLabel, drives, scan, cancel, refresh });
        layout.Controls.Add(toolbar, 0, 1);

        var treeCard = new Panel
        {
            Dock = DockStyle.Fill, Margin = new Padding(20, 0, 20, 0),
            Padding = new Padding(13, 12, 13, 12), BackColor = Color.White
        };
        tree.Dock = emptyTree.Dock = DockStyle.Fill;
        treeCard.Controls.Add(tree);
        treeCard.Controls.Add(emptyTree);
        layout.Controls.Add(treeCard, 0, 2);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(21, 0, 21, 0) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        status.Dock = DockStyle.Fill;
        status.AccessibleName = "Scanstatus";
        activity.Anchor = AnchorStyles.Right;
        activity.Size = new Size(105, 8);
        footer.Controls.Add(status, 0, 0);
        footer.Controls.Add(activity, 1, 0);
        layout.Controls.Add(footer, 0, 3);
    }

    private static void StyleButton(Button button, bool primary)
    {
        button.MinimumSize = new Size(90, 32);
        button.Margin = new Padding(0, 2, 8, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(190, 204, 214);
        button.BackColor = primary ? Accent : Color.White;
        button.ForeColor = primary ? Color.White : Ink;
    }

    private void RefreshDrives()
    {
        string? previous = drives.SelectedItem as string;
        string? driveError = null;
        try
        {
            var names = DriveInfo.GetDrives().Select(drive => drive.Name).ToArray();
            drives.Items.Clear();
            drives.Items.AddRange(names);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            driveError = error.Message;
        }

        if (launchPath is not null && drives.FindStringExact(launchPath) < 0)
            drives.Items.Add(launchPath);
        drives.SelectedIndex = previous is null ? -1 : drives.FindStringExact(previous);
        scan.Enabled = drives.Items.Count > 0;

        if (activeScan is not null || tree.RowCount > 0)
            return;

        bool hasLocations = drives.Items.Count > 0;
        SetEmpty(hasLocations ? "Kies een locatie om de mappenboom te bekijken." : "Er zijn geen schijven beschikbaar.");
        status.Text = driveError is not null ? $"Schijven ophalen is mislukt. {driveError}"
            : hasLocations ? "Grootste items verschijnen bovenaan." : "Sluit een schijf aan en klik op Vernieuwen.";
    }

    private async Task ScanSelectedDrive()
    {
        if (drives.SelectedItem is not string rootPath)
        {
            drives.Focus();
            drives.DroppedDown = true;
            return;
        }

        await ScanLocation(rootPath);
    }

    private async Task ScanLocation(string rootPath)
    {
        activeScan?.Cancel();
        using var request = new CancellationTokenSource();
        activeScan = request;
        tree.ClearItems();
        SetEmpty($"Locatie {rootPath} wordt gescand…");
        status.Text = $"Scannen · {rootPath}";
        cancel.Enabled = activity.Visible = true;

        var progress = new Progress<string>(path =>
        {
            if (!closing && activeScan == request && !request.IsCancellationRequested)
                status.Text = $"Scannen · {path}";
        });

        try
        {
            var result = await Task.Run(() => DiskScanner.Scan(rootPath, progress, request.Token));
            if (closing || activeScan != request)
                return;

            request.Token.ThrowIfCancellationRequested();
            if (result.Root.Children!.Count == 0 && result.Unreadable > 0)
            {
                SetEmpty($"Locatie {rootPath} kon niet worden gelezen.");
                status.Text = ResultStatus(result);
                return;
            }

            emptyTree.Visible = false;
            tree.Visible = true;
            tree.ShowRoot(result.Root);

            status.Text = ResultStatus(result);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
            if (!closing && activeScan == request)
            {
                SetEmpty("De scan is gestopt.");
                status.Text = "Kies een locatie of klik op Scannen om opnieuw te beginnen.";
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
        {
            if (!closing && activeScan == request)
            {
                SetEmpty("De scan is mislukt.");
                status.Text = error.Message;
            }
        }
        finally
        {
            if (activeScan == request)
            {
                activeScan = null;
                if (!closing)
                    cancel.Enabled = activity.Visible = false;
            }
        }
    }

    private void CancelScan()
    {
        activeScan?.Cancel();
        cancel.Enabled = false;
        status.Text = "Scan wordt gestopt…";
    }

    private void SetEmpty(string message)
    {
        tree.Visible = false;
        emptyTree.Text = message;
        emptyTree.Visible = true;
    }

    private static string ResultStatus(ScanResult result)
    {
        if (result.Root.Incomplete)
            return $"Overgeslagen · {result.Unreadable:N0} onleesbaar, {result.LinksSkipped:N0} speciale bestandssysteemitems. * = onvolledig";
        return $"Scan voltooid · {result.Root.DirectoryCount:N0} mappen · {result.Root.FileCount:N0} bestanden";
    }
}
