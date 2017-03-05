using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LeaderMonitor
{
    class Program
    {
        static void Main(string[] args)
        {
            LeaderElectionService electionService = new LeaderElectionService("services/myService/leader");
            electionService.LeaderChanged += (source,arguments) => Console.WriteLine(arguments.IsLeader ? "Leader" : "Secondary");
            electionService.Start();
            Console.ReadLine();
        }
    }
}
