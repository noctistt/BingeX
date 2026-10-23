using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BingeX;

public sealed record EchoPackage(string Sha256,string Id,string Name,string Version,string EntryClass,string Author);

/// <summary>Reads metadata only. Import never loads extension classes or follows package URLs.</summary>
public sealed class EchoPackages(string dataDirectory)
{
    private const int Limit=32*1024*1024;
    private readonly string directory=Path.Combine(dataDirectory,"echo","packages");
    public static EchoPackage Inspect(string path)
    {
        using var file=File.OpenRead(path);
        if(file.Length is <=0 or >Limit)throw new InvalidDataException("Extension must be smaller than 32 MB.");
        var hash=Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();file.Position=0;
        using var zip=new ZipArchive(file,ZipArchiveMode.Read);
        if(zip.Entries.Count>10000 || zip.Entries.Count(x=>x.FullName=="AndroidManifest.xml")!=1 || zip.GetEntry("classes.dex")==null)
            throw new InvalidDataException("Not an Echo Android extension package.");
        var manifest=zip.GetEntry("AndroidManifest.xml")!;
        if(manifest.Length is <=0 or >1048576)throw new InvalidDataException("Invalid extension manifest.");
        using var source=manifest.Open();using var buffer=new MemoryStream();
        var block=new byte[8192];int count;
        while((count=source.Read(block))>0){if(buffer.Length+count>1048576)throw new InvalidDataException("Manifest is too large.");buffer.Write(block,0,count);}
        var metadata=ReadManifest(buffer.ToArray());
        string Required(string key){if(!metadata.TryGetValue(key,out var value)||string.IsNullOrWhiteSpace(value)||value.Length>512||value.Any(char.IsControl))throw new InvalidDataException("Missing or invalid Echo metadata: "+key);return value;}
        var id=Required("id");var entry=Required("class");
        if(!Regex.IsMatch(entry,@"\A[A-Za-z_$][A-Za-z0-9_$]*(\.[A-Za-z_$][A-Za-z0-9_$]*)+\z"))throw new InvalidDataException("Invalid extension entry point.");
        return new(hash,id,Required("name"),Required("version"),entry,metadata.GetValueOrDefault("author","") is {} author && author.Length<=512 && !author.Any(char.IsControl)?author:"");
    }
    public IReadOnlyList<EchoPackage> List()
    {
        if(!Directory.Exists(directory))return [];
        var result=new List<EchoPackage>();
        foreach(var path in Directory.EnumerateFiles(directory,"*.eapk").Take(256))
            try{var item=Inspect(path);if(Path.GetFileNameWithoutExtension(path)==item.Sha256)result.Add(item);}catch(IOException){}catch(InvalidDataException){}
        return result.OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public EchoPackage Import(string path)
    {
        Directory.CreateDirectory(directory);
        var temp=Path.Combine(directory,Guid.NewGuid()+".tmp");
        try
        {
            // Copy once, then inspect exactly the bytes retained, so changing the source cannot bypass inspection.
            using(var source=File.OpenRead(path))using(var target=new FileStream(temp,FileMode.CreateNew,FileAccess.Write))
            {var block=new byte[65536];int count,total=0;while((count=source.Read(block))>0){total+=count;if(total>Limit)throw new InvalidDataException("Extension must be smaller than 32 MB.");target.Write(block,0,count);}}
            var item=Inspect(temp);File.Move(temp,Path.Combine(directory,item.Sha256+".eapk"),true);return item;
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public void Remove(string hash)
    {
        if(!Regex.IsMatch(hash,@"\A[a-f0-9]{64}\z"))throw new ArgumentException("Invalid package fingerprint.");
        File.Delete(Path.Combine(directory,hash+".eapk"));
    }

    // Original bounded reader of Android's documented binary XML/string-pool format.
    // Format reference: androidfw/ResourceTypes.h (AOSP). No resource code is copied or executed.
    private static Dictionary<string,string> ReadManifest(byte[] bytes)
    {
        int U16(int at){if(at<0||at>bytes.Length-2)throw new InvalidDataException("Truncated manifest.");return BitConverter.ToUInt16(bytes,at);}
        int U32(int at){if(at<0||at>bytes.Length-4)throw new InvalidDataException("Truncated manifest.");var value=BitConverter.ToUInt32(bytes,at);return value==uint.MaxValue?-1:value<=int.MaxValue?(int)value:throw new InvalidDataException("Invalid manifest offset.");}
        if(U16(0)!=3||U16(2)!=8||U32(4)!=bytes.Length)throw new InvalidDataException("Unsupported manifest format.");
        string[] strings=[];var result=new Dictionary<string,string>(StringComparer.Ordinal);var stack=new Stack<string>();bool music=false;int roots=0;
        string Str(int index)=>index>=0&&index<strings.Length?strings[index]:throw new InvalidDataException("Invalid string reference.");
        for(int at=8;at<bytes.Length;)
        {
            int type=U16(at),header=U16(at+2),size=U32(at+4);
            if(header<8||size<header||size>bytes.Length-at)throw new InvalidDataException("Invalid manifest chunk.");
            int end=at+size;
            if(type==1)
            {
                if(strings.Length!=0||header<28)throw new InvalidDataException("Invalid string pool.");
                int count=U32(at+8),start=U32(at+20);bool utf8=(U32(at+16)&256)!=0;
                if(count<0||count>16384||start<header||start>size||count>(start-header)/4)throw new InvalidDataException("Invalid string table.");
                strings=new string[count];
                for(int i=0;i<count;i++)
                {
                    int offset=U32(at+header+i*4);if(offset<0||offset>=size-start)throw new InvalidDataException("Invalid string offset.");int pos=at+start+offset;
                    int Length(){if(pos>=end)throw new InvalidDataException("Truncated string.");if(utf8){int n=bytes[pos++];if((n&128)==0)return n;if(pos>=end)throw new InvalidDataException("Truncated string.");return ((n&127)<<8)|bytes[pos++];}else{if(pos>end-2)throw new InvalidDataException("Truncated string.");int n=U16(pos);pos+=2;if((n&32768)==0)return n;if(pos>end-2)throw new InvalidDataException("Truncated string.");int more=U16(pos);pos+=2;return ((n&32767)<<16)|more;}}
                    int length=Length();if(utf8)length=Length();int width=utf8?1:2;
                    if(length<0||length>16384||length>(end-pos-width)/width)throw new InvalidDataException("String exceeds manifest bounds.");
                    Encoding encoding=utf8?new UTF8Encoding(false,true):new UnicodeEncoding(false,false,true);
                    strings[i]=encoding.GetString(bytes,pos,length*width);
                    if(utf8?bytes[pos+length]!=0:U16(pos+length*2)!=0)throw new InvalidDataException("Unterminated manifest string.");
                }
            }
            else if(type==0x102)
            {
                if(header<16||size<header+20)throw new InvalidDataException("Invalid element.");
                int ext=at+header;string tag=Str(U32(ext+4));int start=U16(ext+8),stride=U16(ext+10),count=U16(ext+12);
                if(start<20||stride<20||start>size-header||count>(size-header-start)/stride)throw new InvalidDataException("Invalid attributes.");
                var attributes=new Dictionary<string,string>(StringComparer.Ordinal);
                for(int i=0;i<count;i++)
                {
                    int row=ext+start+i*stride;string name=Str(U32(row+4));int raw=U32(row+8),kind=bytes[row+15],value=U32(row+16);
                    string? text=raw>=0?Str(raw):kind==3?Str(value):kind==16?value.ToString(System.Globalization.CultureInfo.InvariantCulture):null;
                    if(text!=null&&!attributes.TryAdd(name,text))throw new InvalidDataException("Ambiguous manifest attributes.");
                }
                if(tag=="meta-data"&&stack.Count==2&&stack.Peek()=="application"&&attributes.TryGetValue("name",out var key)&&attributes.TryGetValue("value",out var data))
                    if(!result.TryAdd(key,data))throw new InvalidDataException("Duplicate Echo metadata.");
                if(tag=="uses-feature"&&stack.Count==1&&attributes.GetValueOrDefault("name")=="dev.brahmkshatriya.echo.music")music=true;
                if(stack.Count==0&&(tag!="manifest"||++roots>1)||stack.Count>64)throw new InvalidDataException("Invalid manifest nesting.");stack.Push(tag);
            }
            else if(type==0x103)
            {
                if(header<16||size<header+8||stack.Count==0||stack.Pop()!=Str(U32(at+header+4)))throw new InvalidDataException("Invalid closing element.");
            }
            at=end;
        }
        if(stack.Count!=0||roots!=1||!music)throw new InvalidDataException("Not an Echo music manifest.");return result;
    }
}
