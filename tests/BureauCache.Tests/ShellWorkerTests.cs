using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// R-12 : les verbes du shell sont énumérés sur un fil STA dédié, jamais sur le fil appelant
[TestClass]
public class ShellWorkerTests
{
    [TestMethod]
    public void R12_VerbsAreListedOnTheDedicatedStaThread()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, "BureauCache");
        try
        {
            int caller = Thread.CurrentThread.ManagedThreadId, worker = -1, menu = -1;
            ApartmentState apartment = ApartmentState.Unknown;
            List<string> names = null;
            using (ManualResetEvent done = new ManualResetEvent(false))
            {
                ShellWorker.LoadVerbs(path, (id, list) =>
                {
                    worker = Thread.CurrentThread.ManagedThreadId;
                    apartment = Thread.CurrentThread.GetApartmentState();
                    menu = id;
                    names = list;
                    done.Set();
                });
                Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(15)), "Les verbes ne sont jamais arrivés.");
            }
            Assert.AreNotEqual(caller, worker);
            Assert.AreEqual(ApartmentState.STA, apartment);
            Assert.IsTrue(menu > 0);
            Assert.IsNotNull(names);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void R12_EachListingGetsANewMenuNumber()
    {
        int first = Load(Path.GetTempPath()), second = Load(Path.GetTempPath());
        Assert.IsTrue(second > first, "Un ancien sous-menu ne doit pas pouvoir exécuter un verbe du nouveau.");
    }

    [TestMethod]
    public void R12_MissingPathGivesNoVerbs()
    {
        List<string> names = null;
        using (ManualResetEvent done = new ManualResetEvent(false))
        {
            ShellWorker.LoadVerbs(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "absent.txt"), (id, list) => { names = list; done.Set(); });
            Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(15)));
        }
        Assert.AreEqual(0, names.Count);
    }

    static int Load(string path)
    {
        int menu = -1;
        using (ManualResetEvent done = new ManualResetEvent(false))
        {
            ShellWorker.LoadVerbs(path, (id, list) => { menu = id; done.Set(); });
            Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(15)));
        }
        return menu;
    }
}
