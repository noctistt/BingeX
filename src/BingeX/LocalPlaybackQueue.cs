namespace BingeX;
public sealed class LocalPlaybackQueue
{
    public const int Limit=1000;
    private readonly List<string> items=[];
    public IReadOnlyList<string> Items=>items;
    public string? CurrentPath{get;private set;}
    private static readonly StringComparer paths=OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
    public bool IsCurrent(string? path)=>CurrentPath!=null && paths.Equals(CurrentPath,path);
    public int CurrentIndex=>items.FindIndex(x=>paths.Equals(x,CurrentPath));
    public int Add(IEnumerable<string> pathsToAdd)
    {
        var count=0;
        foreach(var value in pathsToAdd)
        {
            if(items.Count>=Limit)break;
            string path;try{path=Path.GetFullPath(value);}catch(ArgumentException){continue;}
            if(items.Contains(path,paths))continue;
            items.Add(path);count++;
        }
        return count;
    }
    public void Select(string? path)=>CurrentPath=path==null?null:items.FirstOrDefault(x=>paths.Equals(x,path));
    public void Remove(string path){items.RemoveAll(x=>paths.Equals(x,path));if(paths.Equals(CurrentPath,path))CurrentPath=null;}
    public void Clear(){items.Clear();CurrentPath=null;}
    public void Move(string path,int direction)
    {
        var index=items.FindIndex(x=>paths.Equals(x,path));var target=index+direction;
        if(index<0 || target<0 || target>=items.Count || Math.Abs(direction)!=1)return;
        (items[index],items[target])=(items[target],items[index]);
    }
    public string? Adjacent(int direction,Func<string,bool> exists)
    {
        if(CurrentIndex<0 || Math.Abs(direction)!=1)return null;
        for(var i=CurrentIndex+direction;i>=0 && i<items.Count;i+=direction)if(exists(items[i]))return items[i];
        return null;
    }
    public static IEnumerable<string> FolderOrder(IEnumerable<string> files)=>files.OrderBy(x=>System.Text.RegularExpressions.Regex.Replace(x,@"\d+",m=>m.Value.TrimStart('0').PadLeft(20,'0')),StringComparer.OrdinalIgnoreCase);
}
