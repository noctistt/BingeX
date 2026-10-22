namespace BingeX;
public partial class MainWindow
{
    private long restoredSubtitleGeneration=-1;
    private DateTime? subtitleRestoreUntil;
    private void RememberSubtitlePreference(SubtitlePreference preference)
    {
        restoredSubtitleGeneration=mediaGeneration;
        if(currentContext!=null) store.SaveSubtitlePreference(currentContext,preference);
    }
    private void RestoreSubtitlePreference()
    {
        if(restoredSubtitleGeneration==mediaGeneration) return;
        var preference=EffectiveSubtitlePreference();
        if(preference==null) { restoredSubtitleGeneration=mediaGeneration; return; }
        if(preference.Mode=="off")
        {
            restoredSubtitleGeneration=mediaGeneration; useTextSubtitles=false;
            _=QueuePlayerCommand(native=>native.SetSpu(-1)); return;
        }
        if(preference.Mode is "embedded" or "preferred")
        {
            var match=playbackSnapshot.Subtitles.FirstOrDefault(x=>x.Name==preference.Label)
                ?? playbackSnapshot.Subtitles.FirstOrDefault(x=>preference.Language.Length>0 && SubtitlePreference.EmbeddedLanguage(x.Name)==preference.Language);
            if(match==null)
            {
                if(preference.Mode=="preferred" && onlineEnabled && currentContext is {} extensionContext && currentStreamSubtitles.Any(preference.Matches))
                {
                    restoredSubtitleGeneration=mediaGeneration;
                    _=RestoreOnlineSubtitle(preference,mediaGeneration,extensionContext);
                    return;
                }
                subtitleRestoreUntil??=DateTime.UtcNow.AddSeconds(15);
                if(playbackSnapshot.Spu>=0)_=QueuePlayerCommand(native=>native.SetSpu(-1));
                if(DateTime.UtcNow<subtitleRestoreUntil)return;
                restoredSubtitleGeneration=mediaGeneration;
                if(preference.Mode=="preferred" && onlineEnabled && currentContext is {} context)
                    _=RestoreOnlineSubtitle(preference,mediaGeneration,context);
                else PlayerNotice("Your preferred subtitle language is unavailable. Use CC to choose another.");
                return;
            }
            restoredSubtitleGeneration=mediaGeneration; useTextSubtitles=false;
            _=QueuePlayerCommand(native=>native.SetSpu(match.Id)); return;
        }
        if(preference.Mode=="online" && onlineEnabled && currentContext!=null)
        {
            restoredSubtitleGeneration=mediaGeneration;
            _=RestoreOnlineSubtitle(preference,mediaGeneration,currentContext);
        }
    }
    private async Task RestoreOnlineSubtitle(SubtitlePreference preference,long generation,MediaContext context)
    {
        try
        {
            var choice=currentStreamSubtitles.Where(preference.Matches).OrderByDescending(x=>x.Provider==preference.Provider).FirstOrDefault();
            if(choice==null)
            {
                var token=BeginOnlineSubtitles("Finding your preferred subtitle language…");
                var providers=ConnectedAddons().ToList();
                if(!providers.Any(x=>x.Id==OnlineSubtitles.DefaultProvider.Id)) providers.Add(OnlineSubtitles.DefaultProvider);
                var result=await library.Subtitles(context.Type,context.EpisodeId ?? context.Id,providers,token);
                token.ThrowIfCancellationRequested();
                if(generation!=mediaGeneration) return;
                choice=result.Items.Where(preference.Matches).OrderByDescending(x=>x.Provider==preference.Provider).FirstOrDefault();
            }
            if(generation!=mediaGeneration || localClosing) return;
            if(choice!=null) await ApplyOnlineSubtitle(choice,true);
            else PlayerNotice("Your preferred subtitle language is unavailable for this episode. Use CC to choose another.");
        }
        catch(OperationCanceledException) { }
        catch { if(generation==mediaGeneration && !localClosing) PlayerNotice("Could not restore subtitles. Use CC to retry."); }
        finally { if(generation==mediaGeneration) OnlineSubtitleBusy.IsVisible=false; }
    }
}
