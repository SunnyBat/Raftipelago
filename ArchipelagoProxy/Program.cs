using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArchipelagoProxy
{
    /// <summary>
    /// We need this proxy because RaftModLoader doesn't support external DLLs at the moment.
    /// This allows us to keep using Archipelago.MultiClient.Net instead of either
    /// re-implementing Archipelago's communication structure or trying to copy the sources
    /// into Raftipelago.
    /// One limitation of this is that the mod MUST be contained all in one project, so we are
    /// forced to maintain the same models separately. Very annoying.
    /// This entry point is mainly for debugging, as Raftipelago should auto-start this when
    /// it's first injected.
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Error: Only initialization argument specified must be the port to listen on");
                return;
            }

            var proxyServer = new ProxyServer(int.Parse(args[0]));
            proxyServer.InteractUntilConnectionClosed();
        }
    }
}
