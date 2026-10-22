using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BingeX;

public record MusicTrack(string Path,string Folder,string Title,string Artist,string Album,string AlbumArtist,int Disc,int Track,double Seconds,string Format,int SampleRate,int Bits,string? Cover=null);
public record MusicScan(string Folder,IReadOnlyList<MusicTrack> Tracks,int Skipped,bool Complete);
public sealed class MusicLibrary
{
    private record State(string[] Folders,MusicTrack[] Tracks);
    private readonly string directory;
    public string[] Folders {get;private set;}=[];
    public MusicTrack[] Tracks {get;private set;}=[];
    private static readonly HashSet<string> Extensions=new(StringComparer.OrdinalIgnoreCase){".mp3",".flac",".wav",".m4a",".aac",".ogg",".oga",".opus",".wma",".aiff",".aif",".ape",".wv",".alac"};
    public static bool IsAudio(string path)=>Extensions.Contains(System.IO.Path.GetExtension(path));
    public MusicLibrary(string directory)
    {
        this.directory=directory;
        try {var state=JsonSerializer.Deserialize<State>(File.ReadAllText(System.IO.Path.Combine(directory,"music-library.json")));if(state!=null){Folders=state.Folders??[];Tracks=state.Tracks??[];}} catch { }
    }
    private void Save()
    {
        Directory.CreateDirectory(directory);var path=System.IO.Path.Combine(directory,"music-library.json");
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(new State(Folders,Tracks)));File.Move(path+".tmp",path,true);
    }
    public void Apply(MusicScan scan)
    {
        Folders=Folders.Append(scan.Folder).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        // An inaccessible subtree must not erase its previously indexed tracks.
        var kept=Tracks.Where(x=>!scan.Complete || !string.Equals(x.Folder,scan.Folder,StringComparison.OrdinalIgnoreCase));
        Tracks=scan.Tracks.Concat(kept).DistinctBy(x=>x.Path,StringComparer.OrdinalIgnoreCase).ToArray();Save();
    }
    public void Remove(string folder)
    {
        Folders=Folders.Where(x=>!string.Equals(x,folder,StringComparison.OrdinalIgnoreCase)).ToArray();
        Tracks=Tracks.Where(x=>!string.Equals(x.Folder,folder,StringComparison.OrdinalIgnoreCase)).ToArray();Save();
    }
    public MusicScan Scan(string folder,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var artDirectory=System.IO.Path.Combine(directory,"music-art");long artBytes=0;
        if(Directory.Exists(artDirectory))foreach(var image in Directory.EnumerateFiles(artDirectory,"*.img")){token.ThrowIfCancellationRequested();try{artBytes+=new FileInfo(image).Length;}catch(IOException){}}
        folder=LocalLibraryScanner.NormalizeFolder(folder);var tracks=new List<MusicTrack>();var pending=new Stack<string>();pending.Push(folder);var skipped=0;var count=0;
        while(pending.TryPop(out var current))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                foreach(var entry in Directory.EnumerateFileSystemEntries(current))
                {
                    token.ThrowIfCancellationRequested();
                    if(++count>100000)return new(folder,tracks,skipped+1,false);
                    try
                    {
                        var attributes=File.GetAttributes(entry);if((attributes&FileAttributes.ReparsePoint)!=0)continue;
                        if((attributes&FileAttributes.Directory)!=0){pending.Push(entry);continue;}
                        if(!IsAudio(entry))continue;
                        using var file=TagLib.File.Create(entry);var tag=file.Tag;var properties=file.Properties;
                        string Value(string? value,string fallback)=>string.IsNullOrWhiteSpace(value)?fallback:value.Trim();
                        var parent=System.IO.Path.GetDirectoryName(entry)!;string? cover=null;
                        var picture=tag.Pictures.FirstOrDefault(x=>x.Data.Count is >0 and <=2097152);
                        if(picture!=null)
                        {
                            var art=artDirectory;Directory.CreateDirectory(art);
                            cover=System.IO.Path.Combine(art,Convert.ToHexString(SHA256.HashData(picture.Data.Data))+".img");
                            if(!File.Exists(cover)){if(artBytes+picture.Data.Count<=256L*1024*1024){File.WriteAllBytes(cover,picture.Data.Data);artBytes+=picture.Data.Count;}else cover=null;}
                        }
                        tracks.Add(new(entry,folder,Value(tag.Title,System.IO.Path.GetFileNameWithoutExtension(entry)),Value(tag.FirstPerformer,"Unknown artist"),Value(tag.Album,System.IO.Path.GetFileName(parent)),Value(tag.FirstAlbumArtist,Value(tag.FirstPerformer,"Unknown artist")),(int)tag.Disc,(int)tag.Track,properties.Duration.TotalSeconds,System.IO.Path.GetExtension(entry).TrimStart('.').ToUpperInvariant(),properties.AudioSampleRate,properties.BitsPerSample,cover));
                    }
                    catch(Exception e) when(e is IOException or UnauthorizedAccessException or TagLib.CorruptFileException or TagLib.UnsupportedFormatException or ArgumentException){skipped++;}
                }
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException){skipped++;}
        }
        return new(folder,tracks,skipped,skipped==0);
    }
    public static string Quality(MusicTrack track)=>track.Format+(track.SampleRate>0?$" · {track.SampleRate/1000d:0.###} kHz":"")+(track.Bits>0?$" · {track.Bits}-bit":"");
}
