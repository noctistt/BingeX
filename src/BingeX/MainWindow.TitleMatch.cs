using Avalonia.Controls;
using Avalonia.Interactivity;

namespace BingeX;

public partial class MainWindow
{
    private void AddCatalogMatchAction(MediaContext context)
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Avalonia.Thickness(0,0,0,14) };
        panel.Children.Add(new TextBlock { Text = "This extension has no shared title ID. Match its catalog title to include Stremio add-on streams and subtitle searches.", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        if (context.EpisodeId != null && selectedTitle?.Episodes.FirstOrDefault(x => x.Id == context.EpisodeId) is { NumberingKnown: false })
        {
            panel.Children.Add(new TextBlock { Text = "This episode has no reliable season/episode numbers, so it cannot be safely matched to other add-on sources.", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            StreamItems.Children.Insert(0,panel); return;
        }
        var query = new TextBox { Text = selectedTitle?.Name ?? context.Name, Watermark = "Catalog title to match" };
        panel.Children.Add(query);
        var button = new Button { Content = "Match title for other add-ons" };
        var matches = new StackPanel { Spacing = 6 };
        panel.Children.Add(button); panel.Children.Add(matches); StreamItems.Children.Insert(0,panel);
        button.Click += async (_,_) =>
        {
            var title = selectedTitle;
            if (title == null) return;
            var token = streamWork.Token;
            matches.Children.Clear();
            button.IsEnabled = false;
            try
            {
                var catalogs = await library.Catalogs([OnlineSubtitles.DefaultCatalog],token);
                var candidates = await library.Search(catalogs.Items,query.Text?.Trim() ?? title.Name,token);
                token.ThrowIfCancellationRequested();
                if (selectedTitle?.Id != title.Id) return;
                foreach(var candidate in candidates.Items.Where(x => x.Type == title.Type).Take(10))
                {
                    var choose = new Button { Content = $"Use {candidate.Name} · {candidate.Release} · {candidate.Type}", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
                    choose.Click += async (_,_) =>
                    {
                        choose.IsEnabled = false;
                        try
                        {
                            var full = await library.Details(candidate,[OnlineSubtitles.DefaultCatalog],token);
                            token.ThrowIfCancellationRequested();
                            if (selectedTitle?.Id != title.Id) return;
                            library.LinkTitle(title,full);
                            await FindStreams();
                        }
                        catch (OperationCanceledException) { }
                        catch { DetailMessage.Text = "The catalog title could not be linked. Try again."; choose.IsEnabled = true; }
                    };
                    matches.Children.Add(choose);
                }
                if (matches.Children.Count == 0) matches.Children.Add(new TextBlock { Text = "No matching catalog results. Try editing the title above.", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            }
            catch (OperationCanceledException) { }
            catch { DetailMessage.Text = "Catalog search failed. Check your connection and retry."; }
            finally { button.IsEnabled = true; }
        };
    }
}
