using Avalonia.Controls;
using Avalonia.Interactivity;

namespace BingeX;

public partial class MainWindow
{
    private CloudstreamService cloudstream = null!;
    private readonly CancellationTokenSource cloudstreamLifetime = new();
    private IReadOnlyList<CloudstreamExtension> repositoryExtensions = [];
    private bool cloudstreamBusy;
    private IReadOnlyList<InstalledAddon> ConnectedAddons() => store.Addons().Concat(cloudstream.Installed.Select(CloudstreamService.Addon)).ToArray();
    private void SetupCloudstream()
    {
        cloudstream = new(store.DataDirectory);
        addons.CloudstreamRequest = cloudstream.Request;
        var repositoryFile = Path.Combine(store.DataDirectory, "cloudstream", "repository.txt");
        try { if (File.Exists(repositoryFile)) CloudstreamRepositoryInput.Text = File.ReadAllText(repositoryFile); } catch { }
        RenderCloudstream();
    }
    private void RenderCloudstream()
    {
        CloudstreamDownload.IsVisible = !CloudstreamRuntime.Ready(store.DataDirectory);
        CloudstreamDownload.IsEnabled = !cloudstreamBusy;
        CloudstreamRepositoryButton.IsEnabled = !cloudstreamBusy;
        CloudstreamInstalledItems.Children.Clear();
        CloudstreamAvailableItems.Children.Clear();
        var installedKeys = cloudstream.Installed.Select(x => x.Key).ToHashSet();
        var availableCount = repositoryExtensions.DistinctBy(x => x.Key).Count(x => !installedKeys.Contains(x.Key));
        CloudstreamInstalledHeading.Text = $"Installed · {installedKeys.Count}";
        CloudstreamAvailableHeading.Text = $"Available · {availableCount}";
        CloudstreamInstalledEmpty.IsVisible = installedKeys.Count == 0;
        CloudstreamAvailableEmpty.IsVisible = availableCount == 0;
        CloudstreamAvailableEmpty.Text = repositoryExtensions.Count == 0 ? "Read a repository to find extensions." : "All extensions in this repository are installed.";
        foreach (var extension in cloudstream.Installed.Concat(repositoryExtensions).DistinctBy(x => x.Key).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var installed = cloudstream.Installed.Any(x => x.Key == extension.Key);
            var row = new StackPanel { Spacing = 10 };
            row.Children.Add(new TextBlock { Text = $"{extension.Name} · v{extension.Version}", TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontWeight = Avalonia.Media.FontWeight.SemiBold });
            var button = new Button { Content = installed ? "Remove" : "Install extension", IsEnabled = !cloudstreamBusy && (installed || CloudstreamRuntime.Ready(store.DataDirectory)) };
            button.Click += async (_, _) =>
            {
                cloudstreamBusy = true; RenderCloudstream();
                CloudstreamStatus.Text = installed ? "Removing extension…" : "Installing and checking extension…";
                try
                {
                    if (installed) await cloudstream.Remove(extension);
                    else await cloudstream.Install(extension, cloudstreamLifetime.Token);
                    library.Invalidate(); catalogs = [];
                    CloudstreamStatus.Text = installed ? "Extension removed." : "Installed. Find its providers under Discover on Home.";
                }
                catch (NotSupportedException) { CloudstreamStatus.Text = "This extension needs browser interaction, settings, or capabilities this experimental bridge does not support yet."; }
                catch (OperationCanceledException) { CloudstreamStatus.Text = "Extension request cancelled or timed out. Try again."; }
                catch { CloudstreamStatus.Text = "The extension could not be loaded. Check its repository, connection and compatibility."; }
                finally { cloudstreamBusy = false; RenderCloudstream(); }
            };
            row.Children.Add(button);
            var card = new Border { Width = 240, MinHeight = 150, Padding = new Avalonia.Thickness(14),
                Margin = new Avalonia.Thickness(0, 0, 12, 12), CornerRadius = new Avalonia.CornerRadius(10),
                Background = Avalonia.Media.Brush.Parse("#2B263A"), Child = row };
            (installed ? CloudstreamInstalledItems : CloudstreamAvailableItems).Children.Add(card);
            if (installed)
            {
                var browse = new Button { Content = "Browse providers", IsEnabled = !cloudstreamBusy };
                browse.Click += async (_, _) => { Page("Library"); SearchInput.Text = ""; await RefreshCatalogs(CloudstreamService.Addon(extension).Id); };
                row.Children.Add(browse);
            }
        }
    }
    private async void CloudstreamRepository_Click(object? sender, RoutedEventArgs e)
    {
        cloudstreamBusy = true; RenderCloudstream(); CloudstreamStatus.Text = "Reading repository…";
        try
        {
            var url = CloudstreamRepositoryInput.Text?.Trim() ?? "";
            repositoryExtensions = await cloudstream.Repository(url, cloudstreamLifetime.Token);
            await File.WriteAllTextAsync(Path.Combine(store.DataDirectory, "cloudstream", "repository.txt"), url, cloudstreamLifetime.Token);
            CloudstreamStatus.Text = $"{repositoryExtensions.Count} extensions available. Install only extensions you trust; they execute code on your computer.";
        }
        catch { CloudstreamStatus.Text = "Could not read repository. Use its HTTPS or cloudstreamrepo:// repo.json link, or GitHub repository page. A SHA-256 checksum is required for each extension."; }
        finally { cloudstreamBusy = false; RenderCloudstream(); }
    }
    private async void CloudstreamDownload_Click(object? sender, RoutedEventArgs e)
    {
        cloudstreamBusy = true; RenderCloudstream();
        try
        {
            await CloudstreamRuntime.Download(store.DataDirectory, new Progress<string>(text => CloudstreamStatus.Text = text), cloudstreamLifetime.Token);
            CloudstreamStatus.Text = "Bridge ready. Add a repository, then install an extension.";
        }
        catch (OperationCanceledException) { CloudstreamStatus.Text = "Bridge download cancelled."; }
        catch { CloudstreamStatus.Text = "Bridge download failed. Check your connection and retry; completed components will be reused."; }
        finally { cloudstreamBusy = false; RenderCloudstream(); }
    }
}
