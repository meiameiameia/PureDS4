using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using DS4Windows;
using DS4Windows.DS4Control;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DS4WindowsTests;

[TestClass]
[DoNotParallelize]
public class VirtualOutputRetirementTests
{
    [TestMethod]
    public void PartialRetirementRetainsIdentityAndRetriesOnlyUnfinishedSteps()
    {
        int detach = 0, remove = 0, unregister = 0;
        bool fail = true;
        var lifetime = new ViiperVirtualDeviceLifetime(42, "9", 7,
            (bus, dev) => { remove++; if (fail) throw new IOException("backend unavailable"); },
            (_, _) => detach++, _ => unregister++);
        Assert.ThrowsException<IOException>(() => lifetime.Dispose());
        Assert.IsFalse(lifetime.IsDisposed);
        Assert.AreEqual((uint)42, lifetime.BusId);
        Assert.AreEqual("9", lifetime.DevId);
        Assert.AreEqual(7, lifetime.UsbipPort);
        Assert.AreEqual(0, unregister);
        fail = false;
        lifetime.Dispose();
        lifetime.Dispose();
        Assert.IsTrue(lifetime.IsDisposed);
        Assert.AreEqual(1, detach);
        Assert.AreEqual(2, remove);
        Assert.AreEqual(1, unregister);
    }

    [TestMethod]
    public void FailedDetachCannotMarkLifetimeDisposedOrDropRegistration()
    {
        int remove = 0, unregister = 0;
        var lifetime = new ViiperVirtualDeviceLifetime(42, "9", 7,
            (_, _) => remove++, (_, _) => throw new IOException("detach failed"),
            _ => unregister++);
        Assert.ThrowsException<IOException>(() => lifetime.Dispose());
        Assert.IsFalse(lifetime.IsDisposed);
        Assert.AreEqual(0, remove);
        Assert.AreEqual(0, unregister);
    }

    [DataTestMethod]
    [DataRow("{\"busId\":42,\"devId\":\"9\"}")]
    [DataRow("{\"status\":404,\"title\":\"Not Found\"}")]
    public void RemovalAcceptsMatchingConfirmationOrAlreadyAbsent(string response)
        => ViiperClient.VerifyRemovalResponse(response, 42, "9");

    [DataTestMethod]
    [DataRow("")]
    [DataRow("{}")]
    [DataRow("invalid JSON")]
    [DataRow("{\"busId\":43,\"devId\":\"9\"}")]
    [DataRow("{\"busId\":42,\"devId\":\"10\"}")]
    [DataRow("{\"status\":500,\"title\":\"Internal Server Error\"}")]
    public void MissingInvalidOrFailedConfirmationIsNotSuccessfulRemoval(string response)
        => Assert.ThrowsException<IOException>(() => ViiperClient.VerifyRemovalResponse(response, 42, "9"));

    [TestMethod]
    public void DetachCommandFailureOrUnchangedPortDoesNotConfirmRetirement()
    {
        const int port = 77;
        ViiperUsbipPortManager.RegisterActivePort(port, "42-9");
        try
        {
            foreach (bool commandSucceeds in new[] { false, true })
            {
                bool Run(string[] args, out string output, out string error)
                {
                    output = args[0] == "port" ? "Port 77: imported\n -> usbip://127.0.0.1:3241/42-9\n" : "";
                    error = "simulated failure";
                    return args[0] == "port" || commandSucceeds;
                }
                Assert.ThrowsException<IOException>(() => ViiperUsbipPortManager.DetachRegisteredPort(port, "test", Run));
                Assert.IsTrue(ViiperUsbipPortManager.IsActivePort(port));
            }
        }
        finally { ViiperUsbipPortManager.UnregisterActivePort(port); }
    }

    [TestMethod]
    public void ForeignPortIsNeverDetached()
    {
        bool detached = false;
        bool Run(string[] args, out string output, out string error)
        {
            detached |= args[0] == "detach";
            output = "Port 78: imported\n -> usbip://192.0.2.1:3241/42-9\n";
            error = "";
            return true;
        }
        Assert.ThrowsException<IOException>(() => ViiperUsbipPortManager.DetachRegisteredPort(78, "test", Run));
        Assert.IsFalse(detached);
    }

    [TestMethod]
    public void FailedCreationCleanupRetainsLifetimeUntilRetrySucceeds()
    {
        var registry = new ViiperCreationCleanupRegistry();
        bool fail = true;
        int removed = 0;
        var lifetime = new ViiperVirtualDeviceLifetime(42, "9", 7,
            (_, _) => { removed++; if (fail) throw new IOException("API unavailable"); },
            (_, _) => { }, _ => { });
        registry.Add(lifetime);
        Assert.ThrowsException<IOException>(() => registry.RetirePending());
        Assert.IsFalse(lifetime.IsDisposed);
        fail = false;
        registry.RetirePending();
        registry.RetirePending();
        Assert.IsTrue(lifetime.IsDisposed);
        Assert.AreEqual(2, removed);
    }

    [TestMethod]
    public async Task ClientDoesNotTreatBackendErrorAsSuccessfulRemoval()
    {
        // Disposable local API fixture; it never invokes USB/IP or a driver.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var client = new ViiperClient("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
        Task server = Task.Run(async () =>
        {
            using TcpClient connection = await listener.AcceptTcpClientAsync();
            using NetworkStream stream = connection.GetStream();
            var request = new List<byte>();
            var buffer = new byte[1];
            while (await stream.ReadAsync(buffer) == 1 && buffer[0] != 0) request.Add(buffer[0]);
            Assert.AreEqual("bus/42/remove 9", Encoding.UTF8.GetString(request.ToArray()));
            await stream.WriteAsync(Encoding.UTF8.GetBytes("{\"status\":500,\"title\":\"Failure\"}\n"));
        });
        Assert.ThrowsException<IOException>(() => client.RemoveDevice(42, "9"));
        await server.WaitAsync(TimeSpan.FromSeconds(3));
    }
}
