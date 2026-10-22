using Avalonia;
using Avalonia.Animation;
using Avalonia.Styling;
namespace BingeX;
public partial class MainWindow
{
    private async Task AnimateStartupBrand()
    {
        if(Program.StartupFile!=null || !preferences.StartupAnimation){StartupSplash.IsVisible=false;return;}
        StartupSplash.PointerPressed+=(_,e)=>{StartupSplash.IsVisible=false;e.Handled=true;};
        try
        {
            var reveal=new Animation{Duration=TimeSpan.FromMilliseconds(550)};
            reveal.Children.Add(new KeyFrame{Cue=new Cue(0),Setters={new Setter(OpacityProperty,0d)}});
            reveal.Children.Add(new KeyFrame{Cue=new Cue(.55),Setters={new Setter(OpacityProperty,1d)}});
            reveal.Children.Add(new KeyFrame{Cue=new Cue(1),Setters={new Setter(OpacityProperty,1d)}});
            await reveal.RunAsync(StartupBrand);
            if(localClosing)return;
            StartupBrand.Opacity=1;
            var fade=new Animation{Duration=TimeSpan.FromMilliseconds(180)};
            fade.Children.Add(new KeyFrame{Cue=new Cue(0),Setters={new Setter(OpacityProperty,1d)}});
            fade.Children.Add(new KeyFrame{Cue=new Cue(1),Setters={new Setter(OpacityProperty,0d)}});
            await fade.RunAsync(StartupSplash);
        }
        catch { /* Branding must never prevent the player from opening. */ }
        finally {StartupSplash.IsVisible=false;}
    }
}
