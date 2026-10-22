using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace BingeX;

public partial class MainWindow
{
    private CancellationTokenSource localScanWork = new();
    private int localScanRevision;
    private int localDisplayLimit = 100;
    private readonly Dictionary<string, bool> localAvailability = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> thumbnailAttempts = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource thumbnailWork = new();
    private bool thumbnailRunning;
    private bool localClosing;
    private bool localLibraryReady;
    private bool changingLocalFolders;
    private readonly HashSet<string> collapsedLocalGroups=new();
    private readonly Avalonia.Threading.DispatcherTimer localSearchTimer=new(){Interval=TimeSpan.FromMilliseconds(200)};
    private record LocalFolderChoice(string? Path, string Name) { public override string ToString() => Name; }

    private void SetupLocalLibrary()
    {
        LocalSort.SelectedIndex=Math.Clamp(preferences.LocalLibrarySort,0,2);
        LocalGrouping.SelectedIndex=Math.Clamp(preferences.LocalLibraryGrouping,0,2);
        localSearchTimer.Tick+=(_,_)=>{localSearchTimer.Stop();RefreshLocalLibrary();};
        Closed+=(_,_)=>localSearchTimer.Stop();
        localLibraryReady = true;
        RefreshLocalFolders();
        RefreshLocalLibrary();
    }
    private void LocalLibrary_Click(object? sender, RoutedEventArgs e) => Page("My Library");
    private void RefreshLocalFolders(string? preferred = null)
    {
        var selected = preferred ?? (LocalFolderFilter.SelectedItem as LocalFolderChoice)?.Path;
        changingLocalFolders = true;
        var folders = store.Folders();
        var choices = new[] { new LocalFolderChoice(null, "All folders") }.Concat(folders.Select(x => new LocalFolderChoice(x, FolderName(x)))).ToArray();
        LocalFolderFilter.ItemsSource = choices;
        LocalFolderFilter.SelectedItem = choices.FirstOrDefault(x => string.Equals(x.Path, selected, StringComparison.OrdinalIgnoreCase)) ?? choices[0];
        changingLocalFolders = false;
    }
    private void RemoveLocalFolder(string folder)
    {
        CancelLocalScan();store.RemoveFolder(folder);RefreshLocalFolders();localDisplayLimit=100;RefreshLocalLibrary();
        LocalScanMessage.Text="Folder removed from My Library. Files and watch history were kept.";
    }
    private void RemoveSelectedLocalFolder_Click(object? sender,RoutedEventArgs e)
    {
        if(LocalFolderFilter.SelectedItem is LocalFolderChoice {Path:{} folder})RemoveLocalFolder(folder);
    }
    private StackPanel LocalFolderHeader(string folder,int count)
    {
        var header=new StackPanel{Orientation=Orientation.Horizontal,Spacing=12};
        var title=new TextBlock{Text=FolderName(folder)+$" · {count} video"+(count==1?"":"s"),FontSize=18,FontWeight=FontWeight.SemiBold,VerticalAlignment=VerticalAlignment.Center};
        ToolTip.SetTip(title,folder);header.Children.Add(title);
        var remove=new Button{Content="Remove folder",FontSize=11,Padding=new Thickness(10,6),Tag=folder};
        AutomationProperties.SetName(remove,"Remove library folder "+FolderName(folder));
        ToolTip.SetTip(remove,"Remove "+folder+" from My Library, including indexed subfolders. Video files and watch history are kept.");
        remove.Click+=(_,e)=>{e.Handled=true;RemoveLocalFolder(folder);};header.Children.Add(remove);return header;
    }
    private static string FolderName(string path)
    {
        var name = Path.GetFileName(path);
        return name.Length > 0 ? name : path;
    }
    private async void AddMediaFolder_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new() { Title = "Add a video folder", AllowMultiple = false });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;
        try
        {
            store.AddFolder(path);
            RefreshLocalFolders(LocalLibraryScanner.NormalizeFolder(path));
            localDisplayLimit = 100;
            await ScanMediaFolders();
        }
        catch { LocalScanMessage.Text = "This folder could not be added. Choose a readable local folder."; }
    }
    private async void RescanMediaFolders_Click(object? sender, RoutedEventArgs e) => await ScanMediaFolders();
    private void CancelLocalScan()
    {
        localScanRevision++;
        localScanWork.Cancel();
        thumbnailWork.Cancel();
        LocalScanBusy.IsVisible = false;
    }
    private async Task ScanMediaFolders()
    {
        CancelLocalScan();
        thumbnailAttempts.Clear();
        localScanWork.Dispose();
        localScanWork = new();
        var token = localScanWork.Token;
        var revision = localScanRevision;
        var folders = store.Folders();
        if (folders.Count == 0) { LocalScanMessage.Text = "Choose a folder to get started."; RefreshLocalLibrary(); return; }
        LocalScanBusy.IsVisible = true;
        var incomplete = 0;
        try
        {
            foreach (var folder in folders)
            {
                LocalScanMessage.Text = "Scanning " + FolderName(folder) + "…";
                var result = await Task.Run(() => LocalLibraryScanner.Scan(folder, token), token);
                if (token.IsCancellationRequested || revision != localScanRevision) return;
                store.ApplyFolderScan(result);
                if (!result.Complete) incomplete++;
                foreach (var item in result.Videos) localAvailability[item.Path] = true;
                RefreshLocalLibrary();
            }
            var cachedPaths = store.LocalVideos().Select(x => x.Path).ToArray();
            var availability = await Task.Run(() => cachedPaths.ToDictionary(x => x, File.Exists, StringComparer.OrdinalIgnoreCase), token);
            if (token.IsCancellationRequested || revision != localScanRevision) return;
            foreach (var item in availability) localAvailability[item.Key] = item.Value;
            RefreshLocalLibrary();
            var count = store.LocalVideos().Count;
            LocalScanMessage.Text = $"{count} video{(count == 1 ? "" : "s")} in {folders.Count} folder{(folders.Count == 1 ? "" : "s")}." +
                (incomplete > 0 ? $" {incomplete} folder scan(s) were incomplete. Previously indexed videos were kept; check drive access and rescan." : " Scan complete.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch { if (revision == localScanRevision) LocalScanMessage.Text = "The scan could not finish. Existing library entries were kept. Try rescanning."; }
        finally { if (revision == localScanRevision) LocalScanBusy.IsVisible = false; }
    }
    private void LocalVideoSearchChanged(object? sender, TextChangedEventArgs e)
    {
        if (!localLibraryReady) return;
        localDisplayLimit = 100;
        localSearchTimer.Stop(); localSearchTimer.Start();
    }
    private void ClearLocalSearch_Click(object? sender,RoutedEventArgs e)
    {
        LocalVideoSearch.Text=""; localSearchTimer.Stop(); localDisplayLimit=100; RefreshLocalLibrary();
    }
    private void LocalFolderFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!localLibraryReady || changingLocalFolders) return;
        localSearchTimer.Stop();
        if(ReferenceEquals(sender,LocalSort) || ReferenceEquals(sender,LocalGrouping))
        {
            preferences=preferences with{LocalLibrarySort=LocalSort.SelectedIndex,LocalLibraryGrouping=LocalGrouping.SelectedIndex};
            store.SavePreferences(preferences);
        }
        localDisplayLimit = 100;
        RefreshLocalLibrary();
    }
    private void LocalLoadMore_Click(object? sender, RoutedEventArgs e)
    {
        localDisplayLimit += 100;
        RefreshLocalLibrary();
    }
    private void RefreshLocalLibrary()
    {
        if (!localLibraryReady) return;
        var folder = (LocalFolderFilter.SelectedItem as LocalFolderChoice)?.Path;
        RemoveSelectedLocalFolder.IsVisible=folder!=null;
        ToolTip.SetTip(RemoveSelectedLocalFolder,folder==null?"Choose a folder to remove it from the library.":"Remove "+folder+" from My Library. Files and watch history are kept.");
        var query = LocalVideoSearch.Text?.Trim() ?? "";
        var videos = LocalVideoPresentation.Sort(store.LocalVideos(folder), LocalSort.SelectedIndex).Where(x => LocalVideoPresentation.Matches(x,query)).ToArray();
        LocalVideoCount.Text = $"{videos.Length} video{(videos.Length == 1 ? "" : "s")}" + (videos.Length > localDisplayLimit ? $" · showing {localDisplayLimit}" : "");
        LocalLibraryEmpty.IsVisible = videos.Length == 0;
        LocalLibraryEmpty.Text = query.Length > 0 ? "No matching videos. Try a different title or folder."
            : store.Folders().Count == 0 ? "Add your first folder to build your library." : "No videos indexed here yet. Add a video file and rescan.";
        LocalLoadMore.IsVisible = videos.Length > localDisplayLimit;
        LocalVideoItems.Children.Clear();
        var folderPanels=new Dictionary<string,StackPanel>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in videos.Take(localDisplayLimit).Select(x=>x.Folder).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if(LocalGrouping.SelectedIndex==2)break;
            var content=new StackPanel{Spacing=10};folderPanels[root]=content;
            var key="root:"+root;
            var section=new Expander{Header=LocalFolderHeader(root,videos.Count(x=>string.Equals(x.Folder,root,StringComparison.OrdinalIgnoreCase))),Content=content,IsExpanded=!collapsedLocalGroups.Contains(key),HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch};
            section.PropertyChanged+=(_,e)=>{if(e.Property.Name==nameof(Expander.IsExpanded)){if(section.IsExpanded)collapsedLocalGroups.Remove(key);else collapsedLocalGroups.Add(key);}};
            LocalVideoItems.Children.Add(section);
        }
        if(query.Length==0 && LocalGrouping.SelectedIndex!=2)
            foreach(var root in store.Folders().Where(x=>(folder==null || string.Equals(x,folder,StringComparison.OrdinalIgnoreCase)) && !videos.Any(v=>string.Equals(v.Folder,x,StringComparison.OrdinalIgnoreCase))))
                LocalVideoItems.Children.Add(new Expander{Header=LocalFolderHeader(root,0),Content=new TextBlock{Text="No videos indexed here yet. Reconnect the drive or rescan.",TextWrapping=TextWrapping.Wrap},HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch});
        foreach (var group in LocalVideoPresentation.Groups(videos.Take(localDisplayLimit),LocalGrouping.SelectedIndex,LocalSort.SelectedIndex))
        {
            var tiles = new WrapPanel();
            if(LocalGrouping.SelectedIndex==2)LocalVideoItems.Children.Add(tiles);
            else
            {
                var root=group.Videos[0].Folder;var host=folderPanels[root];
                if(group.Name==FolderName(root))host.Children.Add(tiles);
                else
                {
                var key=LocalGrouping.SelectedIndex+":"+group.Key;
                var section=new Expander{Header=group.Name.Replace(Path.DirectorySeparatorChar,'/')+" · "+group.Videos.Count+" videos",Content=tiles,IsExpanded=!collapsedLocalGroups.Contains(key),HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch};
                section.PropertyChanged+=(_,e)=>{if(e.Property.Name==nameof(Expander.IsExpanded)){if(section.IsExpanded)collapsedLocalGroups.Remove(key);else collapsedLocalGroups.Add(key);}};
                host.Children.Add(section);
                }
            }
            foreach (var video in group.Videos)
            {
                var column = new StackPanel { Width = LocalCardWidth, Spacing = preferences.CompactBrowsing?6:8 };
                var art = new Grid { Width = LocalCardWidth, Height = LocalCardWidth*118/210 };
                art.Children.Add(new TextBlock { Text = "▶", FontSize = 30, Foreground = accentLightBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                var thumbnail = store.ThumbnailPath(video.Path);
                if (File.Exists(thumbnail))
                {
                    var image = new Image { Stretch = Stretch.UniformToFill };
                    art.Children.Add(image);
                    _ = artwork.Fill(image, thumbnail);
                }
                column.Children.Add(new Border { Background = Brush.Parse("#2D2440"), CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = art });
                var unavailable = localAvailability.TryGetValue(video.Path, out var available) && !available;
                if (unavailable) column.Children.Add(new TextBlock { Text = "Unavailable · reconnect drive or rescan", Foreground = Brush.Parse("#E7B779"), FontSize = 11, TextWrapping = TextWrapping.Wrap });
                var history = store.FindHistory(video.Path);
                if (history is { Duration: > 0 }) column.Children.Add(new ProgressBar { Minimum = 0, Maximum = history.Duration, Value = history.Position, Height = 3, Foreground = accentLightBrush });
                column.Children.Add(new TextBlock { Text = video.Title, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2, Height = 38, FontWeight = FontWeight.SemiBold });
                var parent = Path.GetDirectoryName(video.RelativePath);
                column.Children.Add(new TextBlock { Text = (string.IsNullOrEmpty(parent) ? FolderName(video.Folder) : parent) + " · " + FormatVideoSize(video.Size), TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 11, Foreground = Brush.Parse("#AAA0BA") });
                var play = new Button { Tag=video.Path, Content = column, Padding = new Thickness(0), Background = Brushes.Transparent, Margin = new Thickness(0, 0, preferences.CompactBrowsing?10:16, preferences.CompactBrowsing?12:18), VerticalAlignment = VerticalAlignment.Top };
                ToolTip.SetTip(play, video.Path);
                AutomationProperties.SetName(play, "Play " + video.Title);
                play.Click += (_, _) =>
                {
                    if (!File.Exists(video.Path)) { localAvailability[video.Path] = false; RefreshLocalLibrary(); LocalScanMessage.Text = "This video is unavailable. Reconnect its drive or restore the file, then rescan."; return; }
                    thumbnailWork.Cancel();
                    Play(video.Path, video.Title);
                };
                tiles.Children.Add(play);
            }
        }
        if (activePage == "My Library" && !thumbnailRunning) _ = GenerateLibraryThumbnails(videos.Take(localDisplayLimit).ToArray());
    }
    private async Task GenerateLibraryThumbnails(LocalVideo[] videos)
    {
        thumbnailRunning = true;
        thumbnailWork.Dispose(); thumbnailWork = new();
        var token = thumbnailWork.Token;
        try
        {
            foreach (var video in videos)
            {
                if (token.IsCancellationRequested || activePage != "My Library" || NativePlaybackBusy || playbackSnapshot.IsPlaying) break;
                var destination = store.ThumbnailPath(video.Path);
                if (File.Exists(destination) || !thumbnailAttempts.Add(video.Path) || localAvailability.GetValueOrDefault(video.Path, true) == false) continue;
                try
                {
                    var success = await Task.Run(() => File.Exists(video.Path) && LocalThumbnail.Create(video.Path, destination, token), token);
                    if (token.IsCancellationRequested) { thumbnailAttempts.Remove(video.Path); break; }
                    if (success) RefreshLocalLibrary();
                }
                catch (OperationCanceledException) { thumbnailAttempts.Remove(video.Path); throw; }
                catch { /* Keep a placeholder for this file and continue with the next video. */ }
            }
        }
        catch (OperationCanceledException) { }
        catch { /* A damaged video must not prevent browsing or playing other files. */ }
        finally
        {
            thumbnailRunning = false;
            if (!localClosing && activePage == "My Library" && token.IsCancellationRequested) RefreshLocalLibrary();
        }
    }
    private static string FormatVideoSize(long bytes) => bytes >= 1024L * 1024 * 1024 ? $"{bytes / (1024d * 1024 * 1024):0.0} GB" : $"{bytes / (1024d * 1024):0.#} MB";
}
