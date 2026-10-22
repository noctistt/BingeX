using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
namespace BingeX;
public partial class MainWindow
{
    private readonly SolidColorBrush accentPrimaryBrush=new(Color.Parse("#8B5CF6"));
    private readonly SolidColorBrush accentLightBrush=new(Color.Parse("#B69AFF"));
    private readonly SolidColorBrush accentSoftBrush=new(Color.Parse("#302445"));
    private bool populatingAppearance;
    private static readonly string[] accentNames=["Purple","Blue","Teal","Rose","Amber"];
    private static (string Primary,string Light,string Soft) AccentPalette(string name)=>name switch{
        "Blue"=>("#2563EB","#93C5FD","#1E2C47"),"Teal"=>("#087F73","#99F6E4","#163731"),
        "Rose"=>("#BE185D","#F9A8D4","#3F2033"),"Amber"=>("#B45309","#FCD34D","#3D2C18"),
        _=>("#8B5CF6","#B69AFF","#302445")};
    private double LocalCardWidth=>preferences.CompactBrowsing?180:210;
    private void SetupAppearance()
    {
        if(Application.Current is {} app){app.Resources["AccentPrimary"]=accentPrimaryBrush;app.Resources["AccentLight"]=accentLightBrush;app.Resources["AccentSoft"]=accentSoftBrush;}
        Resources["AccentPrimary"]=accentPrimaryBrush;Resources["AccentLight"]=accentLightBrush;Resources["AccentSoft"]=accentSoftBrush;
        SettingsAccent.SelectionChanged+=(_,_)=>PreviewAppearance();
        SettingsDensity.SelectionChanged+=(_,_)=>PreviewAppearance();
        SettingsReducedMotion.IsCheckedChanged+=(_,_)=>PreviewAppearance();
        ApplyAppearance();
    }
    private void PopulateAppearance()
    {
        populatingAppearance=true;
        SettingsAccent.SelectedIndex=Math.Max(0,Array.IndexOf(accentNames,preferences.AccentColor));
        SettingsDensity.SelectedIndex=preferences.CompactBrowsing?1:0;SettingsReducedMotion.IsChecked=preferences.ReducedMotion;
        populatingAppearance=false;PreviewAppearance();
    }
    private void PreviewAppearance()
    {
        if(populatingAppearance)return;
        var palette=AccentPalette(accentNames[Math.Clamp(SettingsAccent.SelectedIndex,0,4)]);
        AppearancePreview.Background=Brush.Parse(palette.Soft);AppearancePreviewButton.Background=Brush.Parse(palette.Primary);
        AppearancePreviewTitle.Foreground=Brush.Parse(palette.Light);
        AppearancePreview.Padding=new Thickness(SettingsDensity.SelectedIndex==1?12:22);
        AppearancePreviewDescription.Text=(SettingsDensity.SelectedIndex==1?"Compact catalog and local-library cards":"Comfortable catalog and local-library cards")+(SettingsReducedMotion.IsChecked==true?" · Decorative animation off":" · Standard animation");
    }
    private void ApplyAppearance()
    {
        var palette=AccentPalette(preferences.AccentColor);
        accentPrimaryBrush.Color=Color.Parse(palette.Primary);accentLightBrush.Color=Color.Parse(palette.Light);accentSoftBrush.Color=Color.Parse(palette.Soft);
        ApplyPosterDensity();
    }
    private void ApplyPosterDensity()
    {
        foreach(var button in PosterGrid.Children.OfType<Button>().Where(x=>x.Classes.Contains("poster")))
        {
            var width=preferences.CompactBrowsing?132:158;
            button.Width=width;button.Height=preferences.CompactBrowsing?270:306;
            button.Margin=new Thickness(0,0,preferences.CompactBrowsing?10:16,preferences.CompactBrowsing?14:22);
            if(button.Content is not StackPanel column)continue;
            column.Width=width;column.Spacing=preferences.CompactBrowsing?6:9;
            if(column.Children.FirstOrDefault() is Border {Child:Grid art})
                foreach(var image in art.Children.OfType<Image>()){image.Width=width;image.Height=preferences.CompactBrowsing?194:232;}
        }
    }
}
