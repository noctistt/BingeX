using BingeX;
internal static class StorageChecks
{
    public static void Run(string directory)
    {
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        var data=Path.Combine(directory,"storage"); var root=Path.Combine(data,"torrent-cache-v1");
        var active=Path.Combine(root,"torrent-"+new string('a',40)); var idle=Path.Combine(root,"torrent-"+new string('b',40));
        Directory.CreateDirectory(active);Directory.CreateDirectory(idle);
        File.WriteAllBytes(Path.Combine(active,"video.bin"),new byte[10]);File.WriteAllBytes(Path.Combine(idle,"video.bin"),new byte[20]);
        var foreign=Path.Combine(root,"personal-folder");Directory.CreateDirectory(foreign);File.WriteAllText(Path.Combine(foreign,"movie.mkv"),"personal");
        File.WriteAllText(Path.Combine(data,"library.db"),"private database");
        using(var lease=new FileStream(Path.Combine(active,"active.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
        {
            Check(StorageCache.Measure(data,false).Bytes==30,"Torrent sizes include owned cache files only.");
            var cleared=StorageCache.Clear(data,false);
            Check(cleared.Removed==1 && cleared.Skipped==1 && Directory.Exists(active) && !Directory.Exists(idle),"Active locks must protect downloads while unused caches are removed.");
        }
        var thumbs=Path.Combine(data,"thumbnails");Directory.CreateDirectory(thumbs);
        var playing=Path.Combine(thumbs,new string('A',64)+".png");var other=Path.Combine(thumbs,new string('B',64)+".png");
        File.WriteAllBytes(playing,new byte[11]);File.WriteAllBytes(other,new byte[17]);File.WriteAllText(Path.Combine(thumbs,"personal.png"),"personal");
        Check(StorageCache.Measure(data,true).Bytes==28,"Thumbnail totals count generated names only.");
        var result=StorageCache.Clear(data,true,playing);
        Check(result.Removed==1 && result.Skipped==1 && File.Exists(playing) && !File.Exists(other),"Thumbnail cleanup keeps the active image.");
        Check(File.Exists(Path.Combine(thumbs,"personal.png")) && File.Exists(Path.Combine(data,"library.db")) && File.Exists(Path.Combine(foreign,"movie.mkv")),"Cleanup must not remove personal files or profile data.");
        Check(StorageCache.Measure(Path.Combine(directory,"absent"),true).Bytes==0,"Missing cache folders have zero usage.");
        Console.WriteLine("PASS: scoped cache sizes, inactive cleanup, active torrent locks, protected thumbnails and profile/personal-file preservation.");
    }
}
