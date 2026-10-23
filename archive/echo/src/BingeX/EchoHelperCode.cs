using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
namespace BingeX;

public static class EchoHelperCode
{
    // Original BingeX helper classes only; no Echo SDK or third-party classes.
    public static string? Prepare(string dataDirectory)
    {
        using var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("BingeX.Assets.echo-helper.zip");
        if(resource==null)return null;
        using var buffer=new MemoryStream();resource.CopyTo(buffer);var bytes=buffer.ToArray();
        var root=Path.Combine(dataDirectory,"echo","helper",Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());Directory.CreateDirectory(root);
        using var zip=new ZipArchive(new MemoryStream(bytes));
        foreach(var entry in zip.Entries)
        {
            if(Path.GetFileName(entry.FullName)!=entry.FullName||!entry.FullName.StartsWith("EchoMusicHelper",StringComparison.Ordinal)||!entry.FullName.EndsWith(".class",StringComparison.Ordinal)||entry.Length>1048576)throw new InvalidDataException("Invalid built-in music helper.");
            using var source=entry.Open();using var output=File.Create(Path.Combine(root,entry.FullName));source.CopyTo(output);
        }
        return root;
    }
}
