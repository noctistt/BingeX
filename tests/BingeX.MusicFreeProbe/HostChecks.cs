using System.Net;
using BingeX;
internal static class HostChecks
{
    public static int Run()
    {
        void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        const string provider="module.exports={platform:'Fixture',version:'1',search:async()=>({data:[],isEnd:true}),getMediaSource:async()=>({url:'https://example.org/audio'})};";
        var folder=Path.Combine(Path.GetTempPath(),"bingex-musicfree-tests-"+Guid.NewGuid().ToString("N"));
        try{
            var metadata=MusicFreeWorker.Call<MusicFreeProvider>(provider,"inspect",new{},CancellationToken.None).GetAwaiter().GetResult();Check(metadata.Name=="Fixture","Worker startup failed");
            foreach(var bad in new[]{"require('fs');"+provider,"eval('1');"+provider,"while(true){}"+provider}){
                var failed=false;try{MusicFreeWorker.Call<MusicFreeProvider>(bad,"inspect",new{},CancellationToken.None).GetAwaiter().GetResult();}catch{failed=true;}Check(failed,"Unsupported or runaway code must fail");
            }
            using(var cancel=new CancellationTokenSource()){cancel.Cancel();var failed=false;try{MusicFreeWorker.Call<MusicFreeProvider>(provider,"inspect",new{},cancel.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){failed=true;}Check(failed,"Cancellation must reach caller");}
            foreach(var url in new[]{"http://example.org/plugin.js","https://127.0.0.1/plugin.js","https://localhost/plugin.js","https://user:password@example.org/plugin.js"}){var rejected=false;try{MusicFreeHttp.PublicUri(url);}catch{rejected=true;}Check(rejected,"Unsafe import URI accepted");}
            Check(!MusicFreeHttp.PublicAddress(IPAddress.Parse("10.0.0.1"))&&!MusicFreeHttp.PublicAddress(IPAddress.IPv6Loopback),"Private addresses accepted");
            Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"keep.txt"),"keep");var store=new MusicFreeProviderStore(folder);store.Save(metadata with{Trusted=true},provider);Check(store.Read()?.Provider.Trusted==true,"Provider trust was not retained");var second=provider.Replace("Fixture","Alternate");store.Save(new MusicFreeProvider("Alternate","1","",Trusted:true),second);Check(store.ReadAll().Count==2,"Adding a provider must retain the first");store.Select("Fixture");Check(store.Read()?.Provider.Name=="Fixture","Selected provider must persist");store.Remove("Alternate");Check(store.ReadAll().Count==1&&store.Read()?.Provider.Name=="Fixture","Removing one provider must retain the other");store.Remove();Check(store.Read()==null&&File.Exists(Path.Combine(folder,"keep.txt")),"Removal affected unrelated data");
            var legacyFolder=Path.Combine(folder,"legacy");Directory.CreateDirectory(Path.Combine(legacyFolder,"musicfree"));
            var legacyHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(provider)));
            File.WriteAllText(Path.Combine(legacyFolder,"musicfree","provider.json"),System.Text.Json.JsonSerializer.Serialize(new{Provider=metadata with{Trusted=true,Sha256=legacyHash},Source=provider}));
            var legacy=new MusicFreeProviderStore(legacyFolder);Check(legacy.Read()?.Provider.Trusted==true,"Existing single provider must migrate with trust");legacy.Save(new MusicFreeProvider("Alternate","1","",Trusted:true),second);Check(legacy.ReadAll().Count==2,"Migration must preserve old provider when adding another");
            Console.WriteLine("PASS: embedded worker startup, unsupported modules/eval, runaway termination, cancellation, private/credential URI rejection, provider trust persistence and non-destructive removal.");return 0;
        }catch(Exception e){Console.WriteLine("FAIL host checks: "+e.GetType().Name+" / "+e.Message);return 1;}
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
}
