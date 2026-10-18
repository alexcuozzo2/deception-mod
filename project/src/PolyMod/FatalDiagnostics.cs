using System;
using System.IO;
using System.Threading;

namespace Deception.PolyModAdapter
{
    internal static class FatalDiagnostics
    {
        static int Installed,Written;
        internal static void Install(string directory)
        {
            if(Interlocked.Exchange(ref Installed,1)!=0)return;
            string path=Path.Combine(directory,"managed-fatal.txt");
            AppDomain.CurrentDomain.UnhandledException+=(_,args)=>
            {
                if(Interlocked.Exchange(ref Written,1)!=0)return;
                try
                {
                    // Preserve the prior incident. Avoid Unity, log callbacks,
                    // background tasks, or any attempt to resume execution.
                    if(File.Exists(path))File.Copy(path,path+".previous",true);
                    string detail=args.ExceptionObject is Exception e
                        ? e.GetType().FullName+"\n"+Limit(e.Message)+"\n"+Limit(e.StackTrace)
                        : "Non-Exception managed failure";
                    File.WriteAllText(path,DateTimeOffset.Now.ToString("O")+"\n"+
                        "Version="+typeof(Entry).Assembly.GetName().Version+"\n"+
                        "Thread="+Environment.CurrentManagedThreadId+"; Terminating="+args.IsTerminating+"\n"+detail);
                }
                catch { /* Best effort only; the runtime still terminates. */ }
            };
        }
        static string Limit(string? value)=>value is null?"":value.Length<=8192?value:value.Substring(0,8192);
    }
}
