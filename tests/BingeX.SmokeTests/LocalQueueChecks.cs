using BingeX;
internal static class LocalQueueChecks
{
    public static void Run(string directory)
    {
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        var first=Path.Combine(directory,"Episode 2.mkv");var missing=Path.Combine(directory,"Episode 3.mkv");var last=Path.Combine(directory,"Episode 10.mkv");
        File.WriteAllText(first,"fixture");File.WriteAllText(last,"fixture");
        var queue=new LocalPlaybackQueue();Check(queue.Add([first,missing,last,first])==3,"Queue paths deduplicate without losing selected order.");
        queue.Select(first);Check(queue.Adjacent(1,File.Exists)==last,"Queue continuation skips missing files.");
        queue.Move(first,1);Check(queue.CurrentIndex==1 && queue.IsCurrent(first),"Reordering keeps the active item identity.");
        queue.Remove(first);Check(queue.CurrentPath==null && File.Exists(first),"Removing the active queue item detaches without deleting the video.");
        queue.Select(last);Check(queue.Adjacent(1,File.Exists)==null,"Queue stops at the end instead of wrapping.");
        queue.Clear();Check(queue.Items.Count==0 && File.Exists(last),"Clear queue never deletes files.");
        Check(LocalPlaybackQueue.FolderOrder([last,first]).First()==first,"Folder loading uses natural episode-number order.");
        queue.Add(Enumerable.Range(0,1005).Select(x=>Path.Combine(directory,x+".mkv")));Check(queue.Items.Count==LocalPlaybackQueue.Limit,"Queue growth is bounded.");
        Console.WriteLine("PASS: local queue ordering, deduplication, stable current item, missing-file skipping, bounds and non-destructive editing.");
    }
}
