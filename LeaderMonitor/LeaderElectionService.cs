using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Consul;
using System.Threading;

namespace LeaderMonitor
{
    public class LeaderElectionService
    {
        public LeaderElectionService(string leadershipLockKey)
        {
            if (string.IsNullOrEmpty(leadershipLockKey))
                throw new ArgumentNullException(leadershipLockKey);

            this.key = leadershipLockKey;
        }

        public event EventHandler<LeaderChangedEventArgs> LeaderChanged;

        string key;
        CancellationTokenSource cts = new CancellationTokenSource();
        Timer timer;

        bool lastIsHeld = false;
        IDistributedLock distributedLock;        

        public void Start()
        {
            timer = new Timer(async (object state) => await TryAcquireLock((CancellationToken)state), cts.Token, 0, Timeout.Infinite);
        }

        private async Task TryAcquireLock(CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return;
            try
            {
                if (distributedLock == null)
                {
                    ConsulClient client = new ConsulClient();                   
                    distributedLock = await client.AcquireLock(new LockOptions(key) { LockTryOnce = true, LockWaitTime = TimeSpan.FromSeconds(3) }, token).ConfigureAwait(false);
                }
                else
                {
                    if (!distributedLock.IsHeld)
                    {                      
                        await distributedLock.Acquire(token).ConfigureAwait(false);
                    }
                }
            }
            catch (LockMaxAttemptsReachedException e)
            {
                //this is expected if it couldn't acquire the lock within the first attempt.                
            }
            catch (Exception ex)
            {
                //Log
            }
            finally
            {
                bool lockHeld = distributedLock?.IsHeld == true;
                HandleLockStatusChange(lockHeld);
                timer.Change(lockHeld ? 10000 : 7000, Timeout.Infinite);//Retrigger the timer after a 10 seconds delay (in this example). Delay for 7s if not held as the AcquireLock call will block for ~3s in every failed attempt.
            }
        }

        protected virtual void HandleLockStatusChange(bool isHeldNew)
        {
#if DEBUG
            var status = isHeldNew ? "Leader" : "Slave";
            Console.WriteLine($"[Debug] status is ({status}) at {DateTime.Now.ToString("hh:mm:ss")}"); //Comment out or replace with your favourite logger.
#endif

            if (lastIsHeld == isHeldNew)
                return;
            else
            {
                lastIsHeld = isHeldNew;
            }


            if (LeaderChanged != null)
            {
                LeaderChangedEventArgs args = new LeaderChangedEventArgs(lastIsHeld);
                foreach (EventHandler<LeaderChangedEventArgs> handler in LeaderChanged.GetInvocationList())
                {
                    try
                    {
                        handler(this, args);
                    }
                    catch (Exception ex)
                    {
                        //Log;
                    }
                }
            }
        }

    }
}
