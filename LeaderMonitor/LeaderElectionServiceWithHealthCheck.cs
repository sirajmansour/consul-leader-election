using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Consul;
using System.Threading;

namespace LeaderMonitor
{
    public class LeaderElectionServiceWithHealthCheck
    {
        public LeaderElectionServiceWithHealthCheck(string leadershipLockKey)
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
        Task sessionRenewTask;
        CancellationTokenSource sessionRenewCts;

        public void Start()
        {
            timer = new Timer(async (object state) => await TryAccquireLock((CancellationToken)state), cts.Token, 0, Timeout.Infinite);
        }

        private async Task TryAccquireLock(CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return;
            try
            {
                if (distributedLock == null)
                {
                    ConsulClient client = new ConsulClient();
                    SessionEntry se = new SessionEntry()
                    {
                        Checks = new List<string>() {
                            "serfHealth", //Default health check for the consul agent. It is very recommended to keep this.
                         // "myServiceHealthCheck" //Any additional health check.
                        },
                        Name = "myServicSession",
                        TTL = TimeSpan.FromSeconds(30) //Optional TTL check, to achieve sliding expiration. It is very recommended to use it.
                    };

                    string sessionId = (await client.Session.Create(se).ConfigureAwait(false)).Response;
                    if (se.TTL.HasValue)
                    {
                        sessionRenewCts = new CancellationTokenSource();
                        sessionRenewTask = client.Session.RenewPeriodic(se.TTL.Value, sessionId,
                        WriteOptions.Default, sessionRenewCts.Token);
                    }
                    distributedLock = await client.AcquireLock(new LockOptions(key) { Session = sessionId, LockTryOnce = true, LockWaitTime = TimeSpan.FromSeconds(3) }, token).ConfigureAwait(false);
                }
                else
                {
                    if (!distributedLock.IsHeld)
                    {
                        if (sessionRenewTask.IsCompleted)
                        {
                            Task.WaitAny(sessionRenewTask); //Awaits the task without throwing, cleaner than try catch.
                            distributedLock = null;
                        }
                        else
                        {
                            await distributedLock.Acquire(token).ConfigureAwait(false);
                        }
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
                if (distributedLock == null && sessionRenewCts != null) {                    
                        sessionRenewCts.Cancel();
                        sessionRenewCts.Dispose();                    
                }

                if (distributedLock?.IsHeld == true)
                    HandleLockStatusChange(true);
                else
                    HandleLockStatusChange(false);

                timer.Change(10000, Timeout.Infinite);//Retrigger the timer after an 10 seconds delay (in this example)
            }
        }

        protected virtual void HandleLockStatusChange(bool isHeldNew)
        {
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
