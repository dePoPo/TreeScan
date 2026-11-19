using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Threading;

namespace TreeScan;

internal sealed class ScanEntry
{
    public ScanEntry(string name, bool isDirectory)
    {
        Name = name;
        Children = isDirectory ? new List<ScanEntry>() : null;
        FileCount = isDirectory ? 0 : 1;
    }

    public string Name { get; }
    public List<ScanEntry>? Children { get; }
    public long Bytes { get; set; }
    public long FileCount { get; set; }
    public long DirectoryCount { get; set; }
    public bool Incomplete { get; set; }
}

internal sealed class ScanResult
{
    public ScanResult(string rootPath)
    {
        Root = new ScanEntry(rootPath, isDirectory: true);
    }

    public ScanEntry Root { get; }
    public long Unreadable { get; set; }
    public long LinksSkipped { get; set; }
}

internal static class DiskScanner
{
    public static ScanResult Scan(
        string rootPath, IProgress<string> progress, CancellationToken cancellation)
    {
        var result = new ScanResult(rootPath);
        var pending = new Stack<DirectoryFrame>();
        var timer = Stopwatch.StartNew();
        pending.Push(new DirectoryFrame(new DirectoryInfo(rootPath), result.Root));

        while (pending.Count > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            var frame = pending.Peek();
            if (frame.Subdirectories is null)
            {
                ReportProgress(frame.Directory.FullName, progress, timer);
                frame.Subdirectories = ReadDirectory(frame, result, progress, timer, cancellation);
            }

            if (frame.NextDirectory < frame.Subdirectories.Count)
            {
                pending.Push(frame.Subdirectories[frame.NextDirectory++]);
                continue;
            }

            frame.Entry.Children!.Sort(CompareEntries);
            pending.Pop();
            if (pending.Count > 0)
            {
                var parent = pending.Peek().Entry;
                parent.Bytes += frame.Entry.Bytes;
                parent.FileCount += frame.Entry.FileCount;
                parent.DirectoryCount += 1 + frame.Entry.DirectoryCount;
                parent.Incomplete |= frame.Entry.Incomplete;
            }
        }

        cancellation.ThrowIfCancellationRequested();
        return result;
    }

    private static List<DirectoryFrame> ReadDirectory(
        DirectoryFrame frame, ScanResult result, IProgress<string> progress,
        Stopwatch timer, CancellationToken cancellation)
    {
        var directories = new List<DirectoryFrame>();
        var parent = frame.Entry;
        try
        {
            foreach (var item in frame.Directory.EnumerateFileSystemInfos())
            {
                cancellation.ThrowIfCancellationRequested();
                ReportProgress(frame.Directory.FullName, progress, timer);
                try
                {
                    if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        result.LinksSkipped++;
                        parent.Incomplete = true;
                        continue;
                    }

                    if (item is DirectoryInfo directory)
                    {
                        var child = new ScanEntry(directory.Name, isDirectory: true);
                        parent.Children!.Add(child);
                        directories.Add(new DirectoryFrame(directory, child));
                    }
                    else if (item is FileInfo file)
                    {
                        var child = new ScanEntry(file.Name, isDirectory: false) { Bytes = file.Length };
                        parent.Children!.Add(child);
                        parent.Bytes += child.Bytes;
                        parent.FileCount++;
                    }
                }
                catch (Exception error) when (IsReadError(error))
                {
                    parent.Incomplete = true;
                    result.Unreadable++;
                }
            }
        }
        catch (Exception error) when (IsReadError(error))
        {
            parent.Incomplete = true;
            result.Unreadable++;
        }

        return directories;
    }

    private static void ReportProgress(string path, IProgress<string> progress, Stopwatch timer)
    {
        if (timer.ElapsedMilliseconds < 160)
            return;
        timer.Restart();
        progress.Report(path);
    }

    private static int CompareEntries(ScanEntry left, ScanEntry right)
    {
        int bySize = right.Bytes.CompareTo(left.Bytes);
        return bySize != 0 ? bySize : StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
    }

    private static bool IsReadError(Exception error) =>
        error is IOException or UnauthorizedAccessException or SecurityException;

    private sealed class DirectoryFrame
    {
        public DirectoryFrame(DirectoryInfo directory, ScanEntry entry)
        {
            Directory = directory;
            Entry = entry;
        }

        public DirectoryInfo Directory { get; }
        public ScanEntry Entry { get; }
        public List<DirectoryFrame>? Subdirectories { get; set; }
        public int NextDirectory { get; set; }
    }
}
