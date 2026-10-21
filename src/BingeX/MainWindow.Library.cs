using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System.Text.Json;

namespace BingeX;

public partial class MainWindow
{
    private readonly MediaLibrary library;
    private readonly ArtworkLoader artwork = new();
    private CancellationTokenSource browseWork = new();
    private CancellationTokenSource catalogWork = new();
    private CancellationTokenSource detailWork = new();
    private CancellationTokenSource streamWork = new();
    private CancellationTokenSource posterWork = new();
    private readonly List<(Image Image, string Location)> posterImages = [];
    private IReadOnlyList<CatalogInfo> catalogs = [];
    private TitleInfo? selectedTitle;
    private MediaContext? selectedContext;
    private MediaContext? currentContext;
    private string? currentHistoryKey;
    private string activePage = "Library";
    private bool changingCatalog;
    private bool thumbnailAttempted;

    private CancellationToken BeginBrowse()
    {
        browseWork.Cancel(); browseWork.Dispose(); browseWork = new();
        BrowseRetry.IsVisible = false;
        return browseWork.Token;
    }
    private void BrowseLoading(string text)
    {
        posterWork.Cancel();
        posterImages.Clear();
        BrowseBusy.IsVisible = true;
        BrowseLoadingText.Text = text;
        BrowseMessage.Text = "";
        PosterGrid.Children.Clear();
    }
    private void BrowseNotice(string text, bool retry = false)
    {
        BrowseBusy.IsVisible = false;
        BrowseMessage.Text = text;
        BrowseRetry.IsVisible = retry;
    }
    private async Task RefreshCatalogs(string? preferredAddonId = null)
    {
        if (!onlineEnabled) return;
        catalogWork.Cancel(); catalogWork.Dispose(); catalogWork=new();
        var token=catalogWork.Token;
        var browseToken=BeginBrowse();
        BrowseLoading("Connecting to your catalogs…");
        var collected=new List<CatalogInfo>(); var failures=0;
        Task? initialCatalog=null;
        try
        {
            var installed=ConnectedAddons();
            WelcomeHero.IsVisible=installed.Count==0;
            CatalogProviderStatus.Text="Connecting to providers…";
            await foreach(var update in ProgressiveProviders.Load<CatalogInfo>(installed,(addon,ct)=>library.Catalogs([addon],ct),token))
            {
                token.ThrowIfCancellationRequested();
                collected.AddRange(update.Result.Items); failures+=update.Result.Failures;
                catalogs=collected.ToArray();
                RenderDiscoverNavigation(initialCatalog==null ? preferredAddonId : null,CatalogPicker.SelectedItem as CatalogInfo);
                CatalogProviderStatus.Text=$"{update.Completed}/{update.Total} providers responded"+(update.Pending.Count>0 ? " · Waiting for: "+string.Join(", ",update.Pending) : failures>0 ? $" · {failures} unavailable" : "");
                if(initialCatalog==null && CatalogPicker.SelectedItem is CatalogInfo catalog)
                    initialCatalog=ShowCatalog(catalog,browseToken);
            }
            if(initialCatalog!=null) await initialCatalog;
            else if(!browseToken.IsCancellationRequested) BrowseNotice(failures>0 ? "Some providers could not be reached. Try again." : catalogs.Any(x=>x.Searchable) ? "Enter a title to search these catalogs." : "Connect a catalog provider to browse here.",failures>0);
            if(installed.Count==0) CatalogProviderStatus.Text="No catalog providers connected.";
        }
        catch(OperationCanceledException) { }
        catch { if(!token.IsCancellationRequested) BrowseNotice("Catalogs couldn’t be loaded. Try again.",true); }
    }
    private async Task ShowCatalog(CatalogInfo catalog, CancellationToken token)
    {
        BrowseLoading("Loading " + catalog.Name + "…");
        try
        {
            var titles = await library.Catalog(catalog, null, token);
            token.ThrowIfCancellationRequested();
            RenderPosters(titles, token);
            BrowseNotice(titles.Count == 0 ? "This catalog has no titles to show right now." : $"{catalog.Name} · {titles.Count} titles");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch { if (!token.IsCancellationRequested) BrowseNotice("This catalog is unavailable. Try again or choose another catalog.", true); }
    }
    private async void CatalogChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!onlineEnabled || changingCatalog || CatalogPicker.SelectedItem is not CatalogInfo catalog) return;
        SearchInput.Text = "";
        await ShowCatalog(catalog, BeginBrowse());
    }
    private async void RefreshCatalogs_Click(object? sender, RoutedEventArgs e)
    {
        SearchInput.Text = "";
        library.Invalidate();
        await RefreshCatalogs();
    }
    private async void Search_Click(object? sender, RoutedEventArgs e) => await SearchCatalogs();
    private async void Search_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        await SearchCatalogs();
    }
    private async Task SearchCatalogs()
    {
        var query = SearchInput.Text?.Trim() ?? "";
        if (query.Length == 0) { if (catalogs.Count > 0) await LoadDiscoverSelection(); return; }
        var token = BeginBrowse();
        BrowseLoading("Searching your catalogs…");
        try
        {
            if (catalogs.Count == 0) { catalogs = (await library.Catalogs(ConnectedAddons(), token)).Items; RenderDiscoverNavigation(); }
            token.ThrowIfCancellationRequested();
            if (!DiscoverCatalogs.Any(x => x.Searchable)) { BrowseNotice("This add-on does not support search in the selected category. Choose another add-on or category."); return; }
            var result = await library.Search(DiscoverCatalogs, query, token);
            token.ThrowIfCancellationRequested();
            RenderPosters(result.Items, token);
            BrowseNotice(result.Items.Count == 0 ? result.Failures > 0 ? "Search providers were unavailable. Please try again." : $"No matches for “{query}”. Try a different title." : $"{result.Items.Count} matches for “{query}”" + (result.Failures > 0 ? $" · {result.Failures} catalog(s) unavailable" : ""), result.Failures > 0);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch { if (!token.IsCancellationRequested) BrowseNotice("Search couldn’t finish. Try again.", true); }
    }
    private async void ClearSearch_Click(object? sender, RoutedEventArgs e)
    {
        SearchInput.Text = "";
        if (CatalogPicker.SelectedItem is CatalogInfo catalog) await ShowCatalog(catalog, BeginBrowse());
        else if (catalogs.Count > 0) await LoadDiscoverSelection();
    }
    private async void RetryBrowse_Click(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(SearchInput.Text)) await SearchCatalogs();
        else await RefreshCatalogs();
    }
    private void RenderPosters(IReadOnlyList<TitleInfo> titles, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        posterWork.Cancel(); posterWork.Dispose(); posterWork = new();
        posterImages.Clear();
        PosterGrid.Children.Clear();
        foreach (var title in titles)
        {
            var card = new Button { Width = 158, Height = 306, Padding = new Thickness(0), Margin = new Thickness(0, 0, 16, 22), Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Top, VerticalContentAlignment = VerticalAlignment.Top };
            card.Classes.Add("poster");
            var column = new StackPanel { Spacing = 9, Width = 158 };
            var image = new Image { Stretch = Stretch.UniformToFill, Width = 158, Height = 232 };
            var art = new Grid();
            art.Children.Add(new TextBlock { Text = "B X", FontSize = 32, Foreground = Brush.Parse("#B69AFF"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            art.Children.Add(image);
            column.Children.Add(new Border { Background = Brush.Parse("#282338"), CornerRadius = new CornerRadius(12), ClipToBounds = true, Child = art });
            column.Children.Add(new TextBlock { Text = title.Name, Height = 36, FontWeight = FontWeight.SemiBold, MaxLines = 2, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis });
            column.Children.Add(new TextBlock { Text = (title.Release.Length > 0 ? title.Release + " · " : "") + title.Type, FontSize = 11, Foreground = Brush.Parse("#9E9BAE") });
            card.Content = column;
            ToolTip.SetTip(card, title.Name);
            AutomationProperties.SetName(card, "Open " + title.Name);
            card.Click += async (_, _) => await ShowTitle(title);
            PosterGrid.Children.Add(card);
            posterImages.Add((image, title.Poster));
            _ = artwork.Fill(image, title.Poster, posterWork.Token);
        }
    }
    private void ResumePosterArtwork()
    {
        if (!posterWork.IsCancellationRequested) return;
        posterWork.Dispose(); posterWork = new();
        foreach (var (image, location) in posterImages.Where(x => x.Image.Source == null)) _ = artwork.Fill(image, location, posterWork.Token);
    }
    private async Task ShowTitle(TitleInfo title, string? resumeEpisodeId = null)
    {
        detailWork.Cancel(); detailWork.Dispose(); detailWork = new();
        streamWork.Cancel();
        var token = detailWork.Token;
        Page("Details");
        selectedTitle = title;
        selectedContext = ContextFor(title);
        ClearSourceChoices();
        RenderTitle(title, token);
        EpisodeItems.Children.Clear(); SetEpisodeLayout(false);
        DetailBusy.IsVisible = true; DetailLoadingText.Text = "Loading title details…"; DetailMessage.Text = "";
        MovieSourcesButton.IsEnabled = false;
        try
        {
            var full = await library.Details(title, ConnectedAddons(), token);
            token.ThrowIfCancellationRequested();
            selectedTitle = full;
            selectedContext = ContextFor(full);
            RenderTitle(full, token);
            DetailBusy.IsVisible = false;
            if (full.Type == "series" || full.Episodes.Count > 0)
            {
                SetEpisodeLayout(true);
                SeasonPicker.ItemsSource = full.Episodes.Select(x => x.Season).Distinct().Select(x => new SeasonOption(x)).ToArray();
                var season = full.Episodes.FirstOrDefault(x => x.Id == resumeEpisodeId)?.Season ?? full.Episodes.FirstOrDefault()?.Season;
                SeasonPicker.SelectedItem = (SeasonPicker.ItemsSource as SeasonOption[])?.FirstOrDefault(x => x.Number == season);
                DetailMessage.Text = full.Episodes.Count == 0 ? "Episode information is unavailable from your metadata providers." : "Choose an episode to find streams.";
                if (resumeEpisodeId != null && full.Episodes.FirstOrDefault(x => x.Id == resumeEpisodeId) is { } episode) await SelectEpisode(episode);
            }
            else DetailMessage.Text = "Find streams from your installed providers, then choose a source.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch { if (!token.IsCancellationRequested) { DetailBusy.IsVisible = false; DetailMessage.Text = "Title details are unavailable right now. Return to the library and try again."; } }
        finally { if (!token.IsCancellationRequested) MovieSourcesButton.IsEnabled = true; }
    }
    private static MediaContext ContextFor(TitleInfo title) => new(title.Id, title.Type, title.Name, title.Poster, title.AddonId, DurationSeconds: title.DurationSeconds);
    private void RenderTitle(TitleInfo title, CancellationToken token)
    {
        StreamScopeSection.IsVisible=title.Id.StartsWith("cs:");
        DetailTitle.Text = title.Name;
        DetailKind.Text = title.Type.ToUpperInvariant();
        DetailFacts.Text = string.Join("   ·   ", new[] { title.Release, title.Rating.Length > 0 ? "★ " + title.Rating : "" }.Where(x => x.Length > 0));
        DetailGenres.Text = title.Genres;
        DetailDescription.Text = title.Description.Length > 0 ? title.Description : "No synopsis provided by this catalog.";
        DetailPoster.Source = null;
        _ = artwork.Fill(DetailPoster, title.Poster, token);
        MovieSourcesButton.IsVisible = title.Type != "series" && title.Episodes.Count == 0;
    }
    private void SetEpisodeLayout(bool visible)
    {
        EpisodeSection.IsVisible = visible;
        TitleContentGrid.ColumnDefinitions[0].Width = visible ? new GridLength(2, GridUnitType.Star) : new GridLength(0);
        EpisodeScroller.Offset = default;
        StreamScroller.Offset = default;
    }
    private record SeasonOption(int Number) { public override string ToString() => Number == 0 ? "Specials" : "Season " + Number; }
    private void SeasonChanged(object? sender, SelectionChangedEventArgs e)
    {
        streamWork.Cancel();
        ClearSourceChoices();
        EpisodeItems.Children.Clear();
        EpisodeScroller.Offset = default;
        StreamScroller.Offset = default;
        if (selectedTitle == null || SeasonPicker.SelectedItem is not SeasonOption season) return;
        selectedContext = ContextFor(selectedTitle);
        DetailBusy.IsVisible = false;
        DetailMessage.Text = "Choose an episode to find streams.";
        foreach (var episode in EpisodeNavigation.Entries(selectedTitle).Where(x => x.Season == season.Number))
        {
            var text = new StackPanel { Spacing = 5 };
            text.Children.Add(new TextBlock { Text = $"E{episode.Number:00}  {episode.Name}", FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            if (episode.Overview.Length > 0) text.Children.Add(new TextBlock { Text = episode.Overview, FontSize = 12, Foreground = Brush.Parse("#ABA6BA"), TextWrapping = TextWrapping.Wrap });
            var button = new Button { Tag = episode.Id, Content = text, Width = 240, MinHeight = 82, Margin = new Thickness(0, 0, 12, 12), HorizontalContentAlignment = HorizontalAlignment.Left };
            button.Click += async (_, _) => await SelectEpisode(episode);
            EpisodeItems.Children.Add(button);
        }
    }
    private void SetSelectedEpisode(EpisodeInfo episode)
    {
        if (selectedTitle == null) return;
        var ids = EpisodeNavigation.Variants(selectedTitle,episode.Id).Select(x=>x.Id).ToHashSet();
        foreach (var button in EpisodeItems.Children.OfType<Button>()) button.Classes.Set("primary", button.Tag is string id && ids.Contains(id));
        selectedContext = ContextFor(selectedTitle) with { EpisodeId = episode.Id, EpisodeName = $"S{episode.Season:00} E{episode.Number:00} · {episode.Name}" };
    }
    private async Task SelectEpisode(EpisodeInfo episode) { SetSelectedEpisode(episode); await FindStreams(); }
    private async void FindStreams_Click(object? sender, RoutedEventArgs e) => await FindStreams();
    private Task FindStreams() => FindStreamsProgressive(null);
    private async Task FindStreamsProgressive(Action<MediaContext>? ready)
    {
        if (selectedContext == null) return;
        var context = selectedContext;
        ClearSourceChoices();
        streamWork.Cancel(); streamWork.Dispose(); streamWork = new();
        var token = streamWork.Token;
        var revision=sourceViewRevision;
        SuggestedSource.IsVisible = false;
        DetailBusy.IsVisible = false;
        StreamScroller.Offset = default;
        StreamItems.Children.Clear(); StreamHeading.IsVisible = true;
        StreamHeading.Text = context.EpisodeName ?? "Available streams";
        DetailBusy.IsVisible = true; DetailLoadingText.Text = "Checking your stream providers…"; DetailMessage.Text = ""; DetailRetry.IsVisible = false;
        try
        {
            var ids = selectedTitle?.Id == context.Id && context.EpisodeId != null
                ? EpisodeNavigation.Variants(selectedTitle,context.EpisodeId).Select(x=>x.Id).ToArray() : [];
            var all=new List<StreamChoice>(); var failures=0;
            await foreach(var update in ProgressiveProviders.Load<StreamChoice>(StreamProviderScope.Select(ConnectedAddons(),context,preferences.SearchAllStreamProviders),
                (addon,ct)=>ids.Length>0 ? library.EpisodeStreams(context.Type,ids,[addon],ct) : library.Streams(context.Type,context.EpisodeId ?? context.Id,[addon],ct),token))
            {
                token.ThrowIfCancellationRequested();
                if(revision!=sourceViewRevision)return;
                all.AddRange(update.Result.Items); failures+=update.Result.Failures;
                foreach(var check in update.Result.Diagnostics ?? []) addonChecks[check.ManifestUrl]=check;
                RenderSourceChoicesCore(all,context,true);
                DetailLoadingText.Text=$"{update.Completed}/{update.Total} providers responded"+(update.Pending.Count>0 ? " · Waiting for: "+string.Join(", ",update.Pending) : "");
                DetailMessage.Text=$"{availableSources.Count(x=>x.UnsupportedReason==null)} playable source(s) so far. You can play now.";
                if(availableSources.Any(x=>x.UnsupportedReason==null)) ready?.Invoke(context);
                token.ThrowIfCancellationRequested();
            }
            token.ThrowIfCancellationRequested();
            if(revision!=sourceViewRevision)return;
            RefreshAddons();
            var result=new ProviderResult<StreamChoice>(all,failures);
            if (preferences.SearchAllStreamProviders && context.Id.StartsWith("cs:") && !library.HasSharedId(context.Type,context.EpisodeId ?? context.Id)) AddCatalogMatchAction(context);
            var playable = FilteredSources.Count(x => x.UnsupportedReason == null);
            DetailMessage.Text = playable == 0 && result.Items.Any(x=>x.UnsupportedReason==null) ? "No playable sources match this audio filter. Choose All or another audio type." : playable > 0 ? $"{playable} playable source(s). Choose one to start watching." : result.Items.Count > 0 ? "Sources were found, but they need playback features BingeX does not support yet." : "No streams found. Add a compatible stream provider in Add-ons. Catalog providers only supply title information.";
            if (result.Failures > 0) DetailMessage.Text += $" {result.Failures} provider(s) failed. See Add-ons for connection details.";
            DetailRetry.IsVisible = result.Failures > 0;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch { if (!token.IsCancellationRequested) { DetailMessage.Text = "Stream lookup failed. Please try again."; DetailRetry.IsVisible = true; } }
        finally { if (streamWork.Token == token) DetailBusy.IsVisible = false; }
    }
    private void RenderSourceChoices(IReadOnlyList<StreamChoice> options, MediaContext context) => RenderSourceChoicesCore(options,context,false);
    private void RenderSourceChoicesCore(IReadOnlyList<StreamChoice> options, MediaContext context, bool append)
    {
        if (!append) StreamItems.Children.Clear();
        var revision=sourceViewRevision;
        SetSourceAudioContext(context);
        availableSources = SourceSelection.Rank(options, sourcePerformance);
        availableSourceContext = context;
        SuggestedSource.IsVisible = PlaybackSources.Any(x => x.UnsupportedReason == null);
        RenderSourceAudioFilters(context);
        var existing = StreamItems.Children.OfType<Button>().Select(x=>x.Tag).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var stream in FilteredSources)
        {
            if (!existing.Add(SourceSelection.Key(stream))) continue;
            var column = new StackPanel { Spacing = 5 };
            column.Children.Add(new TextBlock { Text = stream.Provider + " · " + stream.Name, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            var audioInfo=SourceAudioInfo.Parse(stream);
            column.Children.Add(new TextBlock{Text=audioInfo.Label+(audioInfo.Languages.Count>0 ? " · Audio: "+string.Join(", ",audioInfo.Languages.Select(x=>System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(x))) : ""),FontSize=11,Foreground=Brush.Parse("#C7B8E5"),TextWrapping=TextWrapping.Wrap});
            var summary = SourceSelection.Summary(stream);
            if (summary.Length > 0) column.Children.Add(new TextBlock { Text = summary, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Brush.Parse("#B69AFF") });
            if (stream.Description.Length > 0) column.Children.Add(new TextBlock { Text = stream.Description, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#BBB4CE") });
            column.Children.Add(new TextBlock { Text = stream.UnsupportedReason ?? (stream.Torrent != null ? "▶ Stream torrent" : "▶ Play this source"), FontSize = 12, Foreground = Brush.Parse(stream.UnsupportedReason == null ? "#B69AFF" : "#D2AB7E") });
            var button = new Button { Tag = SourceSelection.Key(stream), Content = column, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, IsEnabled = stream.UnsupportedReason == null };
            button.Click += (_, _) => { if(!IsCurrentSourceView(revision,context))return; RememberSourceAudioChoice(stream,context); BeginSource(stream, context); };
            StreamItems.Children.Add(button);
        }
    }
    private void HistoryPrevious_Click(object? sender, RoutedEventArgs e) => ScrollHistory(-1);
    private void HistoryNext_Click(object? sender, RoutedEventArgs e) => ScrollHistory(1);
    private void ScrollHistory(int direction)
    {
        var step = Math.Max(264, Math.Floor(HistoryScroller.Viewport.Width / 264) * 264);
        HistoryScroller.Offset = new Vector(Math.Clamp(HistoryScroller.Offset.X + direction * step, 0,
            Math.Max(0, HistoryScroller.Extent.Width - HistoryScroller.Viewport.Width)), 0);
    }
    private void HistoryScrolled(object? sender, ScrollChangedEventArgs e) => UpdateHistoryArrows();
    private void UpdateHistoryArrows()
    {
        HistoryPrevious.IsEnabled = HistoryScroller.Offset.X > 1;
        HistoryNext.IsEnabled = HistoryScroller.Offset.X + HistoryScroller.Viewport.Width < HistoryScroller.Extent.Width - 1;
    }
    private void RefreshHistory()
    {
        HistoryItems.Children.Clear();
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdateHistoryArrows, Avalonia.Threading.DispatcherPriority.Loaded);
        var history = store.History().Where(x => PlaybackPolicy.IsIncomplete(x.Position, x.Duration) &&
            (onlineEnabled || IsLocalHistory(x, store.Context(x.Path)))).ToArray();
        EmptyHistory.IsVisible = history.Length == 0;
        HistorySection.IsVisible = !onlineEnabled || history.Length > 0 || ConnectedAddons().Count == 0;
        foreach (var media in history)
        {
            var context = store.Context(media.Path);
            var column = new StackPanel { Width = 246, Spacing = 8 };
            var image = new Image { Width = 246, Height = 130, Stretch = Stretch.UniformToFill };
            var art = new Grid();
            art.Children.Add(new TextBlock { Text = "▶", FontSize = 28, Foreground = Brush.Parse("#B69AFF"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            art.Children.Add(image);
            column.Children.Add(new Border { Background = Brush.Parse("#282338"), CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = art });
            column.Children.Add(new ProgressBar { Minimum = 0, Maximum = Math.Max(1, media.Duration), Value = media.Position, Height = 3, Foreground = Brush.Parse("#A78BFA") });
            column.Children.Add(new TextBlock { Text = media.Title, MaxLines = 2, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, FontWeight = FontWeight.SemiBold });
            column.Children.Add(new TextBlock { Text = Clock(media.Position) + " / " + Clock(media.Duration), FontSize = 11, Foreground = Brush.Parse("#9E9BAE") });
            var button = new Button { Content = column, Padding = new Thickness(0), Background = Brushes.Transparent };
            button.Click += async (_, _) =>
            {
                if (context?.Type == "torrent-file") StartTorrentFile(context.Id);
                else if (context == null) Play(media.Path, media.Title);
                else await ShowTitle(new(context.Id, context.Type, context.Name, context.Poster, "", "", "", "", "", context.AddonId, []), context.EpisodeId);
            };
            var card = new Grid { Width = 246, Margin = new Thickness(0, 0, 18, 16) };
            card.Children.Add(button);
            var remove = new Button { Content = "×", Width = 28, Height = 28, Padding = new Thickness(0), FontSize = 21,
                CornerRadius = new CornerRadius(14), Background = Brush.Parse("#E5101018"), Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(6) };
            ToolTip.SetTip(remove, "Remove from Continue Watching");
            AutomationProperties.SetName(remove, "Remove " + media.Title + " from Continue Watching");
            remove.Click += (_, e) =>
            {
                e.Handled = true;
                if (currentHistoryKey == media.Path) suppressCurrentHistory = true;
                store.RemoveHistory(media.Path);
                RefreshHistory();
                Status("Removed from Continue Watching. The video file was kept.");
            };
            card.Children.Add(remove);
            HistoryItems.Children.Add(card);
            var artLocation = context?.Poster ?? store.ThumbnailPath(media.Path);
            if (context != null || File.Exists(artLocation)) _ = artwork.Fill(image, artLocation);
        }
    }
    private async void ConnectCatalog_Click(object? sender, RoutedEventArgs e)
    {
        ConnectCatalogButton.IsEnabled = false;
        try
        {
            var addon = await InstallAddon("https://v3-cinemeta.strem.io/manifest.json");
            Page("Library");
            await RefreshCatalogs(addon.Id);
        }
        catch { Status("Could not connect Cinemeta. Check your connection and try again."); }
        finally { ConnectCatalogButton.IsEnabled = true; }
    }
    private async Task<InstalledAddon> InstallAddon(string url)
    {
        var manifest = await addons.Get(url);
        var id = AddonClient.Text(manifest, "id"); var name = AddonClient.Text(manifest, "name");
        if (id.Length == 0 || name.Length == 0 || !manifest.TryGetProperty("resources", out var resources) || resources.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("Invalid manifest");
        if (manifest.TryGetProperty("behaviorHints", out var hints) && hints.ValueKind == JsonValueKind.Object && hints.TryGetProperty("configurationRequired", out var required) && required.ValueKind == JsonValueKind.True) throw new InvalidOperationException("Configure this add-on first");
        var addon = new InstalledAddon(id, name, url);
        store.Install(addon); library.Invalidate(); RefreshAddons();
        return addon;
    }
    private async void Install_Click(object? sender, RoutedEventArgs e)
    {
        InstallButton.IsEnabled = false;
        try
        {
            var addon = await InstallAddon(ManifestInput.Text?.Trim() ?? "");
            ManifestInput.Text = ""; Status("Installed " + addon.Name + ". Browse it from Home.");
            catalogs = [];
        }
        catch { Status("Could not install this add-on. Use its configured HTTPS manifest URL."); }
        finally { InstallButton.IsEnabled = true; }
    }
    private void RefreshAddons() => RenderInstalledAddons();
}
