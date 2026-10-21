using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System.Text.RegularExpressions;

namespace BingeX;

public partial class MainWindow
{
    private readonly OnlineSubtitles subtitleDownloads = new();
    private CancellationTokenSource subtitleWork = new();
    private CancellationToken activeSubtitleToken;
    private bool onlineSubtitleView;
    private bool subtitleQueryInitialized;
    private IReadOnlyList<SubtitleChoice> onlineSubtitleChoices = [];
    private IReadOnlyList<SubtitleChoice> currentStreamSubtitles = [];
    private record SubtitleLanguage(string Code, string Name) { public override string ToString() => Name; }

    private void CancelOnlineSubtitles()
    {
        activeSubtitleToken = default;
        subtitleWork.Cancel();
        OnlineSubtitleBusy.IsVisible = false;
    }
    private void ResetOnlineSubtitles()
    {
        CancelOnlineSubtitles();
        subtitleQueryInitialized = false;
        onlineSubtitleChoices = [];
        SubtitleTitleResults.Children.Clear();
        OnlineSubtitleResults.Children.Clear();
        OnlineSubtitleMessage.Text = "";
        OnlineSubtitleLanguage.IsVisible = false;
        OnlineSubtitleResultCount.IsVisible = false;
    }
    private CancellationToken BeginOnlineSubtitles(string message)
    {
        CancelOnlineSubtitles();
        subtitleWork.Dispose();
        subtitleWork = new();
        subtitleWork.CancelAfter(TimeSpan.FromSeconds(45));
        OnlineSubtitleBusy.IsVisible = true;
        OnlineSubtitleMessage.Text = message;
        activeSubtitleToken = subtitleWork.Token;
        return activeSubtitleToken;
    }
    private bool OwnsSubtitleRequest(CancellationToken token, long generation) => token == activeSubtitleToken && generation == mediaGeneration;
    private bool SubtitleRequestCurrent(CancellationToken token, long generation) => OwnsSubtitleRequest(token, generation) && !token.IsCancellationRequested;
    private void OnlineSubtitles_Click(object? sender, RoutedEventArgs e)
    {
        if (!onlineEnabled) return;
        if (currentPath == null) { PlayerNotice("Open a video before finding subtitles."); return; }
        onlineSubtitleView = true;
        subtitleSettingsOpen = true;
        PlayerSettingsPanel.IsVisible = true;
        PlaybackSettingsSection.IsVisible = false;
        SubtitleSettingsSection.IsVisible = false;
        OnlineSubtitleSection.IsVisible = true;
        SubtitleSearchBack.IsVisible = true;
        PlayerSettingsTitle.Text = "Find subtitles";
        CurrentVideoSubtitlesButton.IsVisible = currentContext?.Type is "movie" or "series" &&
            (currentContext.Type == "movie" || currentContext.EpisodeId != null);
        if (!subtitleQueryInitialized)
        {
            SubtitleQueryInput.Text = currentContext?.Name ?? OnlineSubtitles.GuessTitle(currentTitle);
            var episode = Regex.Match(currentTitle, @"(?i)\bS(\d{1,2})E(\d{1,3})\b");
            SubtitleMediaType.SelectedIndex = currentContext?.Type == "series" || episode.Success ? 1 : 0;
            SubtitleSeason.Value = episode.Success ? int.Parse(episode.Groups[1].Value) : 1;
            SubtitleEpisode.Value = episode.Success ? int.Parse(episode.Groups[2].Value) : 1;
            var parts = currentContext?.EpisodeId?.Split(':');
            if (parts?.Length >= 3 && int.TryParse(parts[^2], out var season) && int.TryParse(parts[^1], out var number))
            { SubtitleSeason.Value = season; SubtitleEpisode.Value = number; }
            SubtitleEpisodeFields.IsVisible = SubtitleMediaType.SelectedIndex == 1;
            subtitleQueryInitialized = true;
        }
        SettingsScroll.Offset = new Vector(0, 0);
        RevealPlayerControls();
    }
    private void SubtitleSearchBack_Click(object? sender, RoutedEventArgs e) => ShowPlayerSettings(true);
    private void SubtitleMediaTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!playerUiReady) return;
        SubtitleEpisodeFields.IsVisible = SubtitleMediaType.SelectedIndex == 1;
        CancelOnlineSubtitles();
        ClearOnlineSubtitleResults();
        SubtitleTitleResults.Children.Clear();
        OnlineSubtitleMessage.Text = "";
    }
    private void SubtitleSearchInputsChanged(object? sender, TextChangedEventArgs e) => InvalidateSubtitleSearch();
    private void SubtitleSearchEpisodeChanged(object? sender, NumericUpDownValueChangedEventArgs e) => InvalidateSubtitleSearch();
    private void InvalidateSubtitleSearch()
    {
        if (!playerUiReady) return;
        CancelOnlineSubtitles();
        ClearOnlineSubtitleResults();
        SubtitleTitleResults.Children.Clear();
        OnlineSubtitleMessage.Text = "";
    }
    private void ClearOnlineSubtitleResults()
    {
        onlineSubtitleChoices = [];
        OnlineSubtitleResults.Children.Clear();
        OnlineSubtitleLanguage.IsVisible = false;
        OnlineSubtitleResultCount.IsVisible = false;
    }
    private async void SubtitleQuery_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        await SearchSubtitleTitles();
    }
    private async void SearchSubtitleTitles_Click(object? sender, RoutedEventArgs e) => await SearchSubtitleTitles();
    private async Task SearchSubtitleTitles()
    {
        var query = SubtitleQueryInput.Text?.Trim() ?? "";
        if (query.Length < 2) { OnlineSubtitleMessage.Text = "Enter a movie or series title."; return; }
        var type = SubtitleMediaType.SelectedIndex == 1 ? "series" : "movie";
        var season = (int)(SubtitleSeason.Value ?? 1);
        var episode = (int)(SubtitleEpisode.Value ?? 1);
        var imdb = Regex.Match(query, @"^tt\d+(?::\d+:\d+)?$");
        if (imdb.Success)
        {
            var id = type == "series" && !query.Contains(':') ? $"{query}:{season}:{episode}" : query;
            await FindOnlineSubtitles(type, id, query);
            return;
        }
        var token = BeginOnlineSubtitles("Searching titles…");
        var generation = mediaGeneration;
        ClearOnlineSubtitleResults();
        SubtitleTitleResults.Children.Clear();
        try
        {
            var searchable = catalogs.Where(x => x.Searchable && x.Type == type).ToList();
            if (searchable.Count == 0)
                searchable.Add(new(OnlineSubtitles.DefaultCatalog, "top", type, "Cinemeta", true, true));
            var result = await library.Search(searchable, query, token);
            if (!SubtitleRequestCurrent(token, generation)) return;
            var titles = result.Items.Take(15).ToArray();
            OnlineSubtitleMessage.Text = titles.Length == 0 ? "No matching titles. Try a shorter name or an IMDb ID."
                : "Choose the matching title" + (type == "series" ? $" · Season {season}, episode {episode}" : "") + ".";
            if (result.Failures > 0) OnlineSubtitleMessage.Text += " Some catalogs could not be reached; try again.";
            foreach (var title in titles)
            {
                var button = new Button { HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Content = new TextBlock { Text = title.Name + (title.Release.Length > 0 ? " · " + title.Release : ""), TextWrapping = TextWrapping.Wrap } };
                button.Click += async (_, _) =>
                {
                    if (type == "series" && !Regex.IsMatch(title.Id, @"^tt\d+$"))
                    { OnlineSubtitleMessage.Text = "For this series, search using its IMDb ID (tt…)."; return; }
                    await FindOnlineSubtitles(type, type == "series" ? $"{title.Id}:{season}:{episode}" : title.Id,
                        title.Name + (type == "series" ? $" · S{season} E{episode}" : ""));
                };
                SubtitleTitleResults.Children.Add(button);
            }
        }
        catch (OperationCanceledException) { if (OwnsSubtitleRequest(token, generation)) OnlineSubtitleMessage.Text = "Title search timed out. Try again."; }
        catch { if (SubtitleRequestCurrent(token, generation)) OnlineSubtitleMessage.Text = "Could not search titles. Check your connection and try again."; }
        finally { if (OwnsSubtitleRequest(token, generation)) OnlineSubtitleBusy.IsVisible = false; }
    }
    private async void CurrentVideoSubtitles_Click(object? sender, RoutedEventArgs e)
    {
        if (currentContext is not { } context) return;
        await FindOnlineSubtitles(context.Type, context.EpisodeId ?? context.Id, context.DisplayName);
    }
    private async Task FindOnlineSubtitles(string type, string id, string title)
    {
        var token = BeginOnlineSubtitles("Finding subtitles…");
        var generation = mediaGeneration;
        ClearOnlineSubtitleResults();
        SubtitleTitleResults.Children.Clear();
        try
        {
            var providers = ConnectedAddons().ToList();
            if (!providers.Any(x => x.Id == OnlineSubtitles.DefaultProvider.Id)) providers.Add(OnlineSubtitles.DefaultProvider);
            var result = await library.Subtitles(type, id, providers, token);
            if (!SubtitleRequestCurrent(token, generation)) return;
            onlineSubtitleChoices = result.Items;
            OnlineSubtitleMessage.Text = title + (result.Items.Count == 0 ? " · No subtitles found. Try another title or add a subtitle provider." : " · Choose a subtitle that matches your release.");
            if (result.Failures > 0) OnlineSubtitleMessage.Text += " Some providers could not be reached; try again.";
            var languages = new[] { new SubtitleLanguage("", "All languages") }.Concat(result.Items.Select(x => x.Language).Distinct()
                .Select(x => new SubtitleLanguage(x, OnlineSubtitles.LanguageName(x))).OrderBy(x => x.Name)).ToArray();
            OnlineSubtitleLanguage.ItemsSource = languages;
            OnlineSubtitleLanguage.SelectedIndex = 0;
            OnlineSubtitleLanguage.IsVisible = result.Items.Count > 0;
            RenderOnlineSubtitles();
        }
        catch (OperationCanceledException) { if (OwnsSubtitleRequest(token, generation)) OnlineSubtitleMessage.Text = "The subtitle request timed out. Try again."; }
        catch { if (SubtitleRequestCurrent(token, generation)) OnlineSubtitleMessage.Text = "Subtitle search failed. Check your connection and try again."; }
        finally { if (OwnsSubtitleRequest(token, generation)) OnlineSubtitleBusy.IsVisible = false; }
    }
    private void OnlineSubtitleLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (playerUiReady) RenderOnlineSubtitles();
    }
    private void RenderOnlineSubtitles()
    {
        OnlineSubtitleResults.Children.Clear();
        var code = (OnlineSubtitleLanguage.SelectedItem as SubtitleLanguage)?.Code ?? "";
        var choices = onlineSubtitleChoices.Where(x => code.Length == 0 || x.Language == code).ToArray();
        OnlineSubtitleResultCount.IsVisible = choices.Length > 0;
        OnlineSubtitleResultCount.Text = choices.Length > 50 ? $"Showing 50 of {choices.Length}. Choose a language to narrow results." : $"{choices.Length} subtitles";
        foreach (var choice in choices.Take(50))
        {
            var row = new Grid { ColumnDefinitions = new("*,Auto") };
            row.Children.Add(new TextBlock { Text = (choice.Label.Length > 0 ? choice.Label : OnlineSubtitles.LanguageName(choice.Language) + " · #" + choice.Id)
                + "\n" + choice.Provider, TextWrapping = TextWrapping.Wrap, MaxLines = 4, FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            var load = new Button { Content = "Load", VerticalAlignment = VerticalAlignment.Center };
            Avalonia.Automation.AutomationProperties.SetName(load, "Load " + OnlineSubtitles.LanguageName(choice.Language) + " subtitle " + choice.Id);
            Grid.SetColumn(load, 1); row.Children.Add(load);
            load.Click += async (_, _) => await LoadOnlineSubtitle(choice);
            OnlineSubtitleResults.Children.Add(new Border { Background = Brush.Parse("#30273D"), Padding = new Thickness(10), CornerRadius = new CornerRadius(8), Child = row });
        }
    }
    private Task LoadOnlineSubtitle(SubtitleChoice choice)
    {
        restoredSubtitleGeneration=mediaGeneration;
        return ApplyOnlineSubtitle(choice,false);
    }
    private async Task ApplyOnlineSubtitle(SubtitleChoice choice,bool automatic)
    {
        var token = BeginOnlineSubtitles("Loading subtitle…");
        var generation = mediaGeneration;
        try
        {
            var document = await subtitleDownloads.Download(choice.Url, token, choice.Headers);
            if (!SubtitleRequestCurrent(token, generation) || player == null) return;
            textSubtitles = document;
            textSubtitleSource="Downloaded";
            textSubtitleName = (choice.Label.Length>0 ? choice.Label : OnlineSubtitles.LanguageName(choice.Language)) + " · " + choice.Provider;
            useTextSubtitles = true;
            lastSubtitleTrack = TextSubtitleId;
            if (!NativePlaybackBusy) player.SetSpu(-1);
            trackSignature = "";
            RefreshTracks();
            if(!automatic)
            {
                RememberSubtitlePreference(new("online",SubtitlePreference.LanguageKey(choice.Language),choice.Label,choice.Provider));
                ShowPlayerSettings(true);
                PlayerNotice("Subtitles loaded: " + textSubtitleName);
                PlayerOverlay.Focus();
            }
        }
        catch (OperationCanceledException) { if (OwnsSubtitleRequest(token, generation)) OnlineSubtitleMessage.Text = "The subtitle request timed out. Try again."; }
        catch (Exception error)
        {
            if (SubtitleRequestCurrent(token, generation)) OnlineSubtitleMessage.Text = error is FormatException or ArgumentException
                ? "This subtitle is not a readable SRT / VTT file. Try another result."
                : "Could not download this subtitle. The provider may be unavailable or require access. Try another result.";
        }
        finally { if (OwnsSubtitleRequest(token, generation)) OnlineSubtitleBusy.IsVisible = false; }
    }
}

