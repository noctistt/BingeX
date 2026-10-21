using System.Runtime.CompilerServices;
namespace BingeX;

public record ProviderUpdate<T>(string Name, ProviderResult<T> Result, int Completed, int Total, IReadOnlyList<string> Pending);
public static class ProgressiveProviders
{
    public static async IAsyncEnumerable<ProviderUpdate<T>> Load<T>(IReadOnlyList<InstalledAddon> providers,
        Func<InstalledAddon,CancellationToken,Task<ProviderResult<T>>> fetch,
        [EnumeratorCancellation] CancellationToken token)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        async Task<ProviderResult<T>> Safe(InstalledAddon provider)
        {
            try { return await fetch(provider,lifetime.Token); }
            catch(OperationCanceledException) when(lifetime.IsCancellationRequested) { throw; }
            catch { return new([],1); }
        }
        var pending = providers.Select(provider=>(Provider:provider,Task:Safe(provider))).ToList();
        var completed=0;
        try
        {
            while(pending.Count>0)
            {
                var task=await Task.WhenAny(pending.Select(x=>x.Task)).WaitAsync(token);
                token.ThrowIfCancellationRequested();
                var entry=pending.First(x=>ReferenceEquals(x.Task,task)); pending.Remove(entry);
                yield return new(entry.Provider.Name,await task,++completed,providers.Count,pending.Select(x=>x.Provider.Name).ToArray());
            }
        }
        finally { lifetime.Cancel(); foreach(var entry in pending) _=entry.Task.ContinueWith(t=>{_ = t.Exception;},TaskContinuationOptions.OnlyOnFaulted); }
    }
}
