using BingeX;

internal static class LocalLibraryChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Run(string testDirectory)
    {
        var root = Path.Combine(testDirectory, "media");
        var nested = Path.Combine(root, "A show", "Season 1");
        Directory.CreateDirectory(nested);
        var movie = Path.Combine(root, "A movie.MKV");
        var episode = Path.Combine(nested, "Episode 01.mp4");
        File.WriteAllText(movie, "video fixture");
        File.WriteAllText(episode, "episode fixture");
        File.WriteAllText(Path.Combine(root, "captions.srt"), "not a video");
        File.WriteAllText(Path.Combine(root, "notes.txt"), "not a video");
        var scan = LocalLibraryScanner.Scan(root, CancellationToken.None);
        Check(scan.Complete && scan.Videos.Count == 2, "Recursive scanning must include supported videos and ignore non-videos.");
        Check(scan.Videos.Single(x => x.Path == episode).RelativePath == Path.Combine("A show", "Season 1", "Episode 01.mp4"), "Subfolder names must be retained.");
        using (var store = new LibraryStore(testDirectory))
        {
            store.AddFolder(root);
            store.AddFolder(root + Path.DirectorySeparatorChar);
            Check(store.Folders().Count == 1, "Repeated/trailing-separator folder additions must be deduplicated.");
            store.ApplyFolderScan(scan);
            store.AddFolder(nested);
            store.ApplyFolderScan(LocalLibraryScanner.Scan(nested, CancellationToken.None));
            Check(store.LocalVideos().Count == 2 && store.LocalVideos(nested).Count == 1, "Overlapping folders must not duplicate videos; folder filtering must still work.");
        }
        using (var reopened = new LibraryStore(testDirectory))
        {
            Check(reopened.Folders().Count == 2 && reopened.LocalVideos().Count == 2, "Folder registrations and indexed files must survive reopening.");
            var newMovie = Path.Combine(root, "New movie.webm");
            File.WriteAllText(newMovie, "new fixture");
            File.Delete(movie);
            reopened.ApplyFolderScan(LocalLibraryScanner.Scan(root, CancellationToken.None));
            Check(reopened.LocalVideos().Count == 2 && reopened.LocalVideos().Any(x => x.Path == newMovie) && reopened.LocalVideos().All(x => x.Path != movie), "Rescanning must discover added files and prune removed files.");
            reopened.ApplyFolderScan(new(LocalLibraryScanner.NormalizeFolder(root), [], false, 1));
            Check(reopened.LocalVideos(root).Count == 2, "Incomplete scans must preserve cached entries.");
            reopened.Save(new(episode, "Episode", 15000, 60000));
            reopened.Save(new("another-entry", "Another", 5000, 60000));
            reopened.SaveAudioSelection(episode, new(2, "Hindi"));
            reopened.RemoveHistory(episode);
            Check(reopened.FindHistory(episode) == null && reopened.FindHistory("another-entry") != null && File.Exists(episode), "Removing Continue Watching must remove only that history entry, never its video file.");
            Check(reopened.AudioSelection(episode)?.Name == "Hindi", "History dismissal must preserve saved audio choices.");
            reopened.RemoveFolder(root);
            Check(reopened.Folders().Count == 1 && reopened.LocalVideos().Single().Path == episode && File.Exists(newMovie), "Removing one overlapping library folder must keep the other registration and every physical video.");
            reopened.ApplyFolderScan(scan);
            Check(reopened.LocalVideos().Count == 1, "A late scan must not reinsert a removed folder.");
        }
        var missing = LocalLibraryScanner.Scan(Path.Combine(testDirectory, "disconnected-drive"), CancellationToken.None);
        Check(!missing.Complete && missing.Videos.Count == 0, "Missing roots must report an incomplete scan.");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var observed = false;
        try { LocalLibraryScanner.Scan(root, canceled.Token); } catch (OperationCanceledException) { observed = true; }
        Check(observed, "Cancellation must stop a folder scan.");
        Console.WriteLine("PASS: recursive video scanning, persistence, rescanning, overlapping folders, missing roots, cancellation, non-destructive removal, and history dismissal.");
    }
}
