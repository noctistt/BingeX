using System.Text.RegularExpressions;
namespace BingeX;
public record CacheUsage(long Bytes,int Files,int Skipped);
public record CacheCleanup(int Removed,int Skipped);
public static class StorageCache
{
    private static bool SafeDirectory(string path)=>Directory.Exists(path) && (File.GetAttributes(path)&FileAttributes.ReparsePoint)==0;
    private static bool Thumbnail(string path)=>Regex.IsMatch(Path.GetFileName(path),@"^[A-Fa-f0-9]{64}\.png$");
    private static string Root(string data,string name)=>Path.GetFullPath(Path.Combine(data,name));
    private static IEnumerable<string> Files(string root,bool thumbnails,Action skipped)
    {
        if(!Directory.Exists(root))yield break;
        if(!SafeDirectory(root)){skipped();yield break;}
        var pending=new Stack<string>();
        if(thumbnails)pending.Push(root);
        else foreach(var folder in Directory.EnumerateDirectories(root))
            if(TorrentCache.IsOwned(Path.GetFileName(folder)))pending.Push(folder);
        while(pending.TryPop(out var directory))
        {
            if(!SafeDirectory(directory)){skipped();continue;}
            string[] entries;
            try{entries=Directory.GetFileSystemEntries(directory);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){skipped();continue;}
            foreach(var entry in entries)
            {
                FileAttributes attributes;
                try{attributes=File.GetAttributes(entry);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){skipped();continue;}
                if((attributes&FileAttributes.ReparsePoint)!=0){skipped();continue;}
                if((attributes&FileAttributes.Directory)!=0){if(!thumbnails)pending.Push(entry);}
                else if(!thumbnails || Thumbnail(entry))yield return entry;
            }
        }
    }
    public static CacheUsage Measure(string data,bool thumbnails)
    {
        long bytes=0;int files=0,skipped=0;
        foreach(var path in Files(Root(data,thumbnails?"thumbnails":"torrent-cache-v1"),thumbnails,()=>skipped++))
            try{bytes+=new FileInfo(path).Length;files++;}catch(Exception e)when(e is IOException or UnauthorizedAccessException){skipped++;}
        return new(bytes,files,skipped);
    }
    public static CacheCleanup Clear(string data,bool thumbnails,string? protectedThumbnail=null)
    {
        var root=Root(data,thumbnails?"thumbnails":"torrent-cache-v1");
        int removed=0,skipped=0;
        if(!Directory.Exists(root))return new(0,0);
        if(!SafeDirectory(root))return new(0,1);
        if(!thumbnails)
        {
            foreach(var directory in Directory.EnumerateDirectories(root).Where(x=>TorrentCache.IsOwned(Path.GetFileName(x))))
                if(TorrentCache.Delete(root,directory))removed++;else skipped++;
        }
        else foreach(var path in Files(root,true,()=>skipped++))
        {
            if(protectedThumbnail!=null && Path.GetFullPath(path).Equals(Path.GetFullPath(protectedThumbnail),StringComparison.OrdinalIgnoreCase)){skipped++;continue;}
            try{File.Delete(path);removed++;}catch(Exception e)when(e is IOException or UnauthorizedAccessException){skipped++;}
        }
        return new(removed,skipped);
    }
}
