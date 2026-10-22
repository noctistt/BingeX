using BingeX;
using System.IO.Compression;
using System.Text;

static void Check(bool value,string message){if(!value)throw new Exception(message);}
static byte[] Manifest(bool utf8,bool duplicate=false,bool music=true)
{
    string[] strings=["manifest","application","uses-feature","meta-data","name","value","dev.brahmkshatriya.echo.music","id","fixture","class","example.Fixture","version","1.2","Fixture 音楽"];
    using var payload=new MemoryStream();using var data=new BinaryWriter(payload);var offsets=new List<int>();
    foreach(var s in strings){offsets.Add((int)payload.Position);var raw=(utf8?Encoding.UTF8:Encoding.Unicode).GetBytes(s);if(utf8){data.Write((byte)s.Length);data.Write((byte)raw.Length);}else data.Write((ushort)s.Length);data.Write(raw);data.Write(new byte[utf8?1:2]);}
    using var chunks=new MemoryStream();using var w=new BinaryWriter(chunks);
    w.Write((ushort)1);w.Write((ushort)28);w.Write(28+strings.Length*4+(int)payload.Length);w.Write(strings.Length);w.Write(0);w.Write(utf8?256:0);w.Write(28+strings.Length*4);w.Write(0);foreach(var n in offsets)w.Write(n);w.Write(payload.ToArray());
    void Tag(int tag,bool start,params (int Key,int Value)[] attrs)
    {
        w.Write((ushort)(start?0x102:0x103));w.Write((ushort)16);w.Write(start?36+attrs.Length*20:24);w.Write(1);w.Write(-1);w.Write(-1);w.Write(tag);
        if(!start)return;w.Write((ushort)20);w.Write((ushort)20);w.Write((ushort)attrs.Length);w.Write((ushort)0);w.Write((ushort)0);w.Write((ushort)0);
        foreach(var a in attrs){w.Write(-1);w.Write(a.Key);w.Write(a.Value);w.Write((ushort)8);w.Write((byte)0);w.Write((byte)3);w.Write(a.Value);}
    }
    Tag(0,true);if(music){Tag(2,true,(4,6));Tag(2,false);}Tag(1,true);
    foreach(var a in new[]{(7,8),(9,10),(11,12),(4,13)}){Tag(3,true,(4,a.Item1),(5,a.Item2));Tag(3,false);}
    if(duplicate){Tag(3,true,(4,7),(5,8));Tag(3,false);}Tag(1,false);Tag(0,false);
    using var output=new MemoryStream();using var writer=new BinaryWriter(output);writer.Write((ushort)3);writer.Write((ushort)8);writer.Write((int)chunks.Length+8);writer.Write(chunks.ToArray());return output.ToArray();
}
static void Package(string path,byte[] manifest,bool dex=true)
{
    using var zip=ZipFile.Open(path,ZipArchiveMode.Create);using(var stream=zip.CreateEntry("AndroidManifest.xml").Open())stream.Write(manifest);
    if(dex){using var stream=zip.CreateEntry("classes.dex").Open();stream.Write("fixture-not-executable"u8);}
}
static void Reject(Action action){try{action();}catch(InvalidDataException){return;}throw new Exception("Malformed package was accepted.");}
var root=Path.Combine(Path.GetTempPath(),"BingeX-echo-packages-"+Guid.NewGuid());Directory.CreateDirectory(root);
try
{
    var library=new EchoPackages(root);
    foreach(bool utf8 in new[]{false,true})
    {
        var path=Path.Combine(root,utf8+".eapk");Package(path,Manifest(utf8));var item=library.Import(path);
        Check(item.Name=="Fixture 音楽"&&item.Id=="fixture"&&item.Version=="1.2"&&item.EntryClass=="example.Fixture","Metadata must come from the manifest.");
        library.Import(path);Check(new EchoPackages(root).List().Count(x=>x.Sha256==item.Sha256)==1,"Reimport should be idempotent and survive reopening.");
        library.Remove(item.Sha256);Check(File.Exists(path)&&library.List().Count==0,"Remove must preserve original file.");
    }
    int index=0;foreach(var content in new[]{Manifest(false,true),Manifest(true,music:false),Manifest(false)[..40],new byte[32]})
    {var path=Path.Combine(root,"bad"+(index++)+".eapk");Package(path,content);Reject(()=>library.Import(path));}
    Check(library.List().Count==0&&!Directory.EnumerateFiles(Path.Combine(root,"echo","packages"),"*.tmp").Any(),"Failed import must leave no package or temporary file.");
    try{library.Remove("../outside");throw new Exception("Traversal accepted.");}catch(ArgumentException){}
    if(args.Length>0){var item=library.Import(Path.GetFullPath(args[0]));Check(item.Name=="OpenSubsonic"&&item.EntryClass.EndsWith("OpenSubsonicExtension"),"Actual package metadata.");Check(library.List().Count==1,"Actual import persists.");}
    if(args.Length>1)
    {
        var profile=EchoRuntimeProfile.Load(Path.GetFullPath(args[1]),root);Check(profile.SdkHash.Length==64,"Prepared runtime must verify.");
        var config=System.Text.Json.JsonSerializer.Deserialize<EchoRuntimeProfile>(File.ReadAllText(args[1]))!;var sdkKey=config.Files.Keys.Single(x=>Path.GetFullPath(x)==Path.GetFullPath(config.Sdk));config.Files[sdkKey]=new string('0',64);
        var altered=Path.Combine(root,"altered-runtime.json");File.WriteAllText(altered,System.Text.Json.JsonSerializer.Serialize(config));Reject(()=>EchoRuntimeProfile.Load(altered,root));
    }
    Console.WriteLine("PASS: UTF-8/UTF-16 manifests, package metadata, persistence, duplicate import, removal, malformed packages, non-music rejection and cleanup.");
}
finally{Directory.Delete(root,true);}
