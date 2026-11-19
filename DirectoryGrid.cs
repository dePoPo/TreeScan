using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TreeScan;

internal sealed class DirectoryGrid : DataGridView
{
    private const int PageSize = 500;
    private const int NameColumn = 0;
    private const int SizeColumn = 1;
    private const int FilesColumn = 2;
    private const int TotalPercentColumn = 3;
    private static readonly string[] SizeUnits = { "B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB" };
    private static readonly Color Accent = Color.FromArgb(0, 104, 100);
    private readonly List<Item> visibleItems = new();
    private readonly ContextMenuStrip itemMenu = new();
    private long scanBytes;

    public DirectoryGrid()
    {
        VirtualMode = true;
        ReadOnly = true;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        AllowUserToResizeRows = false;
        RowHeadersVisible = false;
        MultiSelect = false;
        SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        BackgroundColor = Color.White;
        BorderStyle = BorderStyle.None;
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        GridColor = Color.FromArgb(231, 237, 241);
        EnableHeadersVisualStyles = false;
        ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(237, 242, 246);
        ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(44, 65, 80);
        ColumnHeadersDefaultCellStyle.Padding = new Padding(7, 0, 7, 0);
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        DefaultCellStyle.Padding = new Padding(7, 0, 7, 0);
        DefaultCellStyle.SelectionBackColor = Color.FromArgb(210, 234, 233);
        DefaultCellStyle.SelectionForeColor = Color.FromArgb(22, 40, 54);
        AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        RowTemplate.Height = 29;
        DoubleBuffered = true;
        AccessibleName = "Uitklapbare mappenboom, grootste items eerst";

        var name = Column("Map of bestand", 155);
        name.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        name.MinimumWidth = 155;
        Columns.Add(name);
        Columns.Add(Column("Grootte", 106, alignRight: true));
        Columns.Add(Column("Bestanden", 100, alignRight: true));
        Columns.Add(Column("% van totaal", 150));

        itemMenu.Items.Add("Openen in &Verkenner", null, (_, _) => OpenSelectedItem());
    }

    private Item? SelectedItem
    {
        get
        {
            int index = CurrentCellAddress.Y;
            return index >= 0 && index < visibleItems.Count ? visibleItems[index] : null;
        }
    }

    public void ShowRoot(ScanEntry root)
    {
        scanBytes = root.Bytes;
        var item = new Item(root);
        visibleItems.Add(item);
        ExpandItem(0, item);
        MoveTo(0);
    }

    public void ClearItems()
    {
        scanBytes = 0;
        visibleItems.Clear();
        RowCount = 0;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            itemMenu.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnCellContextMenuStripNeeded(DataGridViewCellContextMenuStripNeededEventArgs e)
    {
        base.OnCellContextMenuStripNeeded(e);
        if (e.RowIndex >= 0 && e.RowIndex < visibleItems.Count && visibleItems[e.RowIndex].Entry is not null)
            e.ContextMenuStrip = itemMenu;
    }

    protected override void OnCellMouseDown(DataGridViewCellMouseEventArgs e)
    {
        base.OnCellMouseDown(e);
        if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && e.ColumnIndex >= 0)
            CurrentCell = this[e.ColumnIndex, e.RowIndex];
    }

    private void OpenSelectedItem()
    {
        if (SelectedItem is not { Entry: { } entry } item)
            return;

        var parts = new Stack<string>();
        for (Item? current = item; current?.Entry is { } part; current = current.Parent)
            parts.Push(part.Name);

        string path = entry.Name;
        try
        {
            path = Path.Combine(parts.ToArray());
            if (entry.Children is not null)
            {
                if (!Directory.Exists(path))
                    throw new DirectoryNotFoundException();

                using var process = Process.Start(new ProcessStartInfo(path) { Verb = "explore", UseShellExecute = true });
            }
            else
            {
                SelectFileInExplorer(path);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Win32Exception
            or COMException or ArgumentException or System.Security.SecurityException)
        {
            MessageBox.Show(this, $"Deze locatie kan niet worden geopend in Verkenner.\r\n\r\n{path}",
                "TreeScan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static void SelectFileInExplorer(string path)
    {
        Marshal.ThrowExceptionForHR(SHParseDisplayName(path, IntPtr.Zero, out var item, 0, IntPtr.Zero));
        try
        {
            // Without a relative selection list, the Shell selects this item in its parent folder.
            Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(item, 0, IntPtr.Zero, 0));
        }
        finally
        {
            Marshal.FreeCoTaskMem(item);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHParseDisplayName(string name, IntPtr bindContext, out IntPtr item, uint attributes, IntPtr foundAttributes);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHOpenFolderAndSelectItems(IntPtr item, uint count, IntPtr children, uint flags);

    protected override void OnCellValueNeeded(DataGridViewCellValueEventArgs e)
    {
        base.OnCellValueNeeded(e);
        if (e.RowIndex < 0 || e.RowIndex >= visibleItems.Count)
            return;

        var item = visibleItems[e.RowIndex];
        e.Value = e.ColumnIndex switch
        {
            NameColumn => item.Name,
            SizeColumn when item.Entry is { } entry => FormatSize(entry.Bytes),
            FilesColumn => item.Entry?.FileCount.ToString("N0"),
            TotalPercentColumn when item.Entry is not null => ScanShare(item)?.ToString("P1") ?? "—",
            _ => null
        };
    }

    protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
    {
        base.OnCellPainting(e);
        if (e.Handled || e.RowIndex < 0 || e.RowIndex >= visibleItems.Count ||
            (e.ColumnIndex != NameColumn && e.ColumnIndex != TotalPercentColumn))
            return;

        var item = visibleItems[e.RowIndex];
        e.Paint(e.ClipBounds, e.PaintParts & ~DataGridViewPaintParts.ContentForeground);
        var graphics = e.Graphics;
        var saved = graphics.Save();
        try
        {
            graphics.IntersectClip(e.CellBounds);
            bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
            Color textColor = selected ? e.CellStyle.SelectionForeColor : e.CellStyle.ForeColor;
            if (e.ColumnIndex == NameColumn)
                DrawName(e, item, textColor);
            else if (item.Entry is not null)
                DrawScanShare(e, ScanShare(item), textColor);
        }
        finally
        {
            graphics.Restore(saved);
        }
        e.Handled = true;
    }

    protected override void OnCellMouseClick(DataGridViewCellMouseEventArgs e)
    {
        base.OnCellMouseClick(e);
        if (e.Button == MouseButtons.Left && e.ColumnIndex == NameColumn && e.RowIndex >= 0)
        {
            var item = visibleItems[e.RowIndex];
            if (item.CanExpand && IsGlyphHit(e.X, item))
                Toggle(e.RowIndex, item);
        }
    }

    protected override void OnCellMouseDoubleClick(DataGridViewCellMouseEventArgs e)
    {
        base.OnCellMouseDoubleClick(e);
        if (e.Button == MouseButtons.Left && e.ColumnIndex == NameColumn && e.RowIndex >= 0)
        {
            var item = visibleItems[e.RowIndex];
            if (item.CanExpand && !IsGlyphHit(e.X, item))
                Toggle(e.RowIndex, item);
        }
    }

    protected override bool ProcessDialogKey(Keys keyData) =>
        TryNavigate(keyData) || base.ProcessDialogKey(keyData);

    protected override bool ProcessDataGridViewKey(KeyEventArgs e) =>
        TryNavigate(e.KeyData) || base.ProcessDataGridViewKey(e);

    private bool TryNavigate(Keys keyData)
    {
        if (SelectedItem is { } item)
        {
            int index = CurrentCellAddress.Y;
            switch (keyData)
            {
                case Keys.Enter when item.CanExpand:
                    Toggle(index, item);
                    return true;
                case Keys.Right when item.CanExpand:
                    if (item.Expanded)
                        MoveTo(index + 1);
                    else
                        ExpandOrLoad(index, item);
                    return true;
                case Keys.Left when item.Expanded:
                    CollapseItem(index, item);
                    return true;
                case Keys.Left when item.Parent is not null:
                    MoveTo(visibleItems.IndexOf(item.Parent));
                    return true;
            }
        }

        return false;
    }

    private void Toggle(int index, Item item)
    {
        if (item.Expanded)
            CollapseItem(index, item);
        else
            ExpandOrLoad(index, item);
    }

    private void ExpandOrLoad(int index, Item item)
    {
        if (item.Entry is not null)
        {
            ExpandItem(index, item);
            return;
        }

        var parent = item.Parent!;
        var loaded = parent.LoadedChildren!;
        loaded.RemoveAt(loaded.Count - 1);
        var page = parent.LoadNextPage();
        visibleItems.RemoveAt(index);
        visibleItems.InsertRange(index, page);
        RowCount = visibleItems.Count;
        MoveTo(index);
        Invalidate();
    }

    private void ExpandItem(int index, Item item)
    {
        item.LoadedChildren ??= new List<Item>();
        if (item.LoadedChildren.Count == 0)
            item.LoadNextPage();

        item.Expanded = item.LoadedChildren.Count > 0;
        visibleItems.InsertRange(index + 1, ExpandedDescendants(item));
        RowCount = visibleItems.Count;
        Invalidate();
    }

    private void CollapseItem(int index, Item item)
    {
        int end = index + 1;
        while (end < visibleItems.Count && visibleItems[end].Depth > item.Depth)
            end++;

        item.Expanded = false;
        visibleItems.RemoveRange(index + 1, end - index - 1);
        RowCount = visibleItems.Count;
        Invalidate();
    }

    private static IEnumerable<Item> ExpandedDescendants(Item item)
    {
        var pending = new Stack<Item>();
        PushChildren(item);
        while (pending.Count > 0)
        {
            var child = pending.Pop();
            yield return child;
            if (child.Expanded)
                PushChildren(child);
        }

        void PushChildren(Item parent)
        {
            var children = parent.LoadedChildren!;
            for (int index = children.Count - 1; index >= 0; index--)
                pending.Push(children[index]);
        }
    }

    private void MoveTo(int index) => CurrentCell = this[NameColumn, index];

    private int GlyphStart(Item item)
    {
        int indent = item.Depth * Math.Max(12, Font.Height - 3);
        return 7 + Math.Min(indent, Columns[NameColumn].Width - 90);
    }

    private bool IsGlyphHit(int x, Item item) => x >= GlyphStart(item) && x < GlyphStart(item) + 15;

    private void DrawName(DataGridViewCellPaintingEventArgs e, Item item, Color textColor)
    {
        var bounds = e.CellBounds;
        int glyph = bounds.Left + GlyphStart(item);
        int center = bounds.Top + bounds.Height / 2;
        if (item.CanExpand)
        {
            Point[] arrow = item.Expanded
                ? new[] { new Point(glyph, center - 2), new Point(glyph + 10, center - 2), new Point(glyph + 5, center + 4) }
                : new[] { new Point(glyph + 2, center - 5), new Point(glyph + 2, center + 5), new Point(glyph + 8, center) };
            using var brush = new SolidBrush(textColor);
            e.Graphics.FillPolygon(brush, arrow);
        }

        int iconLeft = glyph + 15;
        int iconSize = Math.Min(e.CellStyle.Font.Height, bounds.Height - 4);
        int left = item.Entry is null ? iconLeft : iconLeft + iconSize + 5;
        if (left < bounds.Right - 5)
            DrawText(e, item.Name, Rectangle.FromLTRB(left, bounds.Top, bounds.Right - 5, bounds.Bottom), textColor);

        if (item.Entry is { } entry)
            DrawEntryIcon(e.Graphics, iconLeft, center - iconSize / 2, iconSize, entry.Children is not null);
    }

    private static void DrawEntryIcon(Graphics graphics, int left, int top, int size, bool isDirectory)
    {
        // Draw on a 16-unit canvas inside the name cell's saved graphics state.
        graphics.TranslateTransform(left, top);
        graphics.ScaleTransform(size / 16f, size / 16f);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        if (isDirectory)
        {
            Point[] folder =
            {
                new(1, 4), new(2, 3), new(6, 3), new(8, 5), new(14, 5),
                new(15, 6), new(15, 12), new(14, 13), new(2, 13), new(1, 12)
            };
            using var fill = new SolidBrush(Color.FromArgb(249, 205, 104));
            using var outline = new Pen(Color.FromArgb(166, 109, 32));
            graphics.FillPolygon(fill, folder);
            graphics.DrawPolygon(outline, folder);
        }
        else
        {
            Point[] page = { new(3, 1), new(9, 1), new(13, 5), new(13, 14), new(3, 14) };
            using var fill = new SolidBrush(Color.FromArgb(237, 246, 251));
            using var outline = new Pen(Color.FromArgb(79, 109, 130));
            graphics.FillPolygon(fill, page);
            graphics.DrawPolygon(outline, page);
            graphics.DrawLines(outline, new Point[] { new(9, 1), new(9, 5), new(13, 5) });
            graphics.DrawLine(outline, 5, 9, 11, 9);
            graphics.DrawLine(outline, 5, 11, 9, 11);
        }
    }

    private double? ScanShare(Item item) =>
        item.Entry is { } entry && scanBytes > 0 ? (double)entry.Bytes / scanBytes : null;

    private static void DrawScanShare(DataGridViewCellPaintingEventArgs e, double? share, Color textColor)
    {
        var bounds = e.CellBounds;
        if (share is not double value)
        {
            DrawText(e, "—", bounds, textColor, TextFormatFlags.HorizontalCenter);
            return;
        }

        int height = Math.Min(e.CellStyle.Font.Height + 4, bounds.Height - 6);
        var track = new Rectangle(bounds.Left + 8, bounds.Top + (bounds.Height - height) / 2, bounds.Width - 16, height);
        using var background = new SolidBrush(Color.FromArgb(230, 238, 242));
        e.Graphics.FillRectangle(background, track);
        if (value > 0)
        {
            using var fill = new SolidBrush(Color.FromArgb(131, 199, 189));
            int fillWidth = Math.Max(1, (int)Math.Round(track.Width * value));
            e.Graphics.FillRectangle(fill, track.X, track.Y, fillWidth, track.Height);
        }

        if (e.FormattedValue is string label)
            DrawText(e, label, track, Color.FromArgb(22, 40, 54), TextFormatFlags.HorizontalCenter);
    }

    private static void DrawText(
        DataGridViewCellPaintingEventArgs e, string text, Rectangle bounds, Color color,
        TextFormatFlags alignment = TextFormatFlags.Left)
    {
        TextRenderer.DrawText(e.Graphics, text, e.CellStyle.Font, bounds, color,
            alignment | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.PreserveGraphicsClipping);
    }

    private static DataGridViewTextBoxColumn Column(string title, int width, bool alignRight = false)
    {
        var column = new DataGridViewTextBoxColumn
        {
            HeaderText = title, Width = width, MinimumWidth = width - 20,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        if (alignRight)
        {
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        return column;
    }

    private static string FormatSize(long bytes)
    {
        int unit = 0;
        double value = bytes;
        while (value >= 1024 && unit < SizeUnits.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes:N0} B" : $"{value:0.##} {SizeUnits[unit]}";
    }

    private sealed class Item
    {
        public Item(ScanEntry? entry, Item? parent = null, int remaining = 0)
        {
            Entry = entry;
            Parent = parent;
            Depth = parent is null ? 0 : parent.Depth + 1;
            Name = entry is null ? $"Meer weergeven ({remaining:N0})"
                : entry.Incomplete ? $"{entry.Name} *" : entry.Name;
        }

        public ScanEntry? Entry { get; }
        public Item? Parent { get; }
        public int Depth { get; }
        public List<Item>? LoadedChildren { get; set; }
        public bool Expanded { get; set; }
        public bool CanExpand => Entry is null || Entry.Children is { Count: > 0 };
        public string Name { get; }

        public List<Item> LoadNextPage()
        {
            var loaded = LoadedChildren!;
            var entries = Entry!.Children!;
            int end = Math.Min(loaded.Count + PageSize, entries.Count);
            var page = new List<Item>(end - loaded.Count + 1);
            for (int index = loaded.Count; index < end; index++)
                page.Add(new Item(entries[index], this));
            if (end < entries.Count)
                page.Add(new Item(null, this, entries.Count - end));

            loaded.AddRange(page);
            return page;
        }
    }
}
