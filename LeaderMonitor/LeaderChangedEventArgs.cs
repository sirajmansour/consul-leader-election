using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LeaderMonitor
{
    public class LeaderChangedEventArgs : EventArgs
    {
        private bool isLeader;

        public LeaderChangedEventArgs(bool isHeld)
        {
            isLeader = isHeld;
        }

        public bool IsLeader { get { return isLeader; } }
    }
}
