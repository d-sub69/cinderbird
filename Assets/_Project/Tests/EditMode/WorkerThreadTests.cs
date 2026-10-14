using System;
using System.Threading;
using Cinderbird.Core;
using NUnit.Framework;

namespace Cinderbird.Tests.EditMode
{
    public sealed class WorkerThreadTests
    {
        [Test]
        public void Start_RunsTheWorkAndWaitGivesItsResult()
        {
            int runs = 0;
            using (WorkerThread worker = new WorkerThread("test", () => ++runs % 2 == 1))
            {
                worker.Start();
                bool first = worker.Wait(out Exception firstError);
                worker.Start();
                bool second = worker.Wait(out Exception secondError);

                Assert.That(first, Is.True);
                Assert.That(second, Is.False);
                Assert.That(firstError, Is.Null);
                Assert.That(secondError, Is.Null);
                Assert.That(runs, Is.EqualTo(2));
                Assert.That(worker.IsCompleted, Is.True);
            }
        }

        [Test]
        public void Wait_WorkThrew_ReportsTheErrorAndTheWorkerKeepsGoing()
        {
            bool fail = true;
            Func<bool> work = () =>
            {
                if (fail)
                {
                    throw new InvalidOperationException("boom");
                }

                return true;
            };

            using (WorkerThread worker = new WorkerThread("test", work))
            {
                worker.Start();
                worker.Wait(out Exception error);
                fail = false;
                worker.Start();
                bool result = worker.Wait(out Exception secondError);

                Assert.That(error, Is.TypeOf<InvalidOperationException>());
                Assert.That(result, Is.True);
                Assert.That(secondError, Is.Null);
            }
        }

        [Test]
        public void Start_WhileRunning_Throws()
        {
            using (ManualResetEventSlim release = new ManualResetEventSlim(false))
            using (WorkerThread worker = new WorkerThread("test", () => release.Wait(5000)))
            {
                worker.Start();

                Assert.Throws<InvalidOperationException>(worker.Start);
                release.Set();
                worker.Wait(out _);
            }
        }
    }
}
