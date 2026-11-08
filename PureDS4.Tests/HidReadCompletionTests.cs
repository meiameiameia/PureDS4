using System.IO.Pipes;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DS4Windows;
using Microsoft.Win32.SafeHandles;

namespace DS4WindowsTests;

[TestClass]
public class HidReadCompletionTests
{
    [DataTestMethod]
    [DataRow(258u, true, 0u, HidDevice.ReadStatus.Success)]
    [DataRow(258u, false, 995u, HidDevice.ReadStatus.WaitTimedOut)]
    [DataRow(258u, false, 1167u, HidDevice.ReadStatus.ReadError)]
    [DataRow(996u, true, 0u, HidDevice.ReadStatus.Success)]
    [DataRow(996u, false, 995u, HidDevice.ReadStatus.WaitTimedOut)]
    [DataRow(1167u, false, 1167u, HidDevice.ReadStatus.ReadError)]
    public void CancellationUsesTheFinalCompletionNotJustTheWaitResult(
        uint waitError, bool completed, uint completionError,
        HidDevice.ReadStatus expected)
    {
        Assert.AreEqual(expected,
            HidDevice.ClassifyDrainedRead(waitError, completed, completionError));
    }

    [TestMethod]
    public void RealPendingReadCanTimeOutThenReceiveTheNextInput()
    {
        using var pipe = new DisposableReadPipe();
        byte[] buffer = new byte[8];
        Assert.AreEqual(HidDevice.ReadStatus.WaitTimedOut,
            pipe.Device.ReadFile(buffer, 20));

        byte[] payload = { 0x11, 0x22, 0x33, 0x44 };
        pipe.Client.Write(payload);
        Assert.AreEqual(HidDevice.ReadStatus.Success,
            pipe.Device.ReadFile(buffer, 1000));
        CollectionAssert.AreEqual(payload, buffer[..payload.Length]);
    }

    [TestMethod]
    public void ZeroTimeoutAlsoDrainsItsPendingReadBeforeTheBufferIsReused()
    {
        using var pipe = new DisposableReadPipe();
        byte[] buffer = new byte[8];
        Assert.AreEqual(HidDevice.ReadStatus.WaitTimedOut,
            pipe.Device.ReadFile(buffer, 0));
        pipe.Client.Write(new byte[] { 0x11, 0x55 });
        Assert.AreEqual(HidDevice.ReadStatus.Success,
            pipe.Device.ReadFile(buffer, 1000));
        CollectionAssert.AreEqual(new byte[] { 0x11, 0x55 }, buffer[..2]);
    }

    [TestMethod]
    public void ClosedTransportRemainsAnErrorRatherThanARecoverableTimeout()
    {
        using var pipe = new DisposableReadPipe();
        pipe.Client.Dispose();
        Assert.AreEqual(HidDevice.ReadStatus.ReadError,
            pipe.Device.ReadFile(new byte[8], 1000));
    }

    // Only this test's byte pipe is opened. No HID discovery, controller,
    // profile, driver or application startup is involved. The server handle
    // deliberately has no .NET IOCP binding: production owns its OVERLAPPED.
    private sealed class DisposableReadPipe : IDisposable
    {
        private readonly SafeFileHandle server;
        internal NamedPipeClientStream Client { get; }
        internal HidDevice Device { get; }

        internal DisposableReadPipe()
        {
            string name = "PureDS4-read-test-" + Guid.NewGuid().ToString("N");
            server = CreateNamedPipeW(@"\\.\pipe\" + name,
                0x40000001, 0, 1, 1024, 1024, 0, IntPtr.Zero);
            if (server.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                server.Dispose();
                throw new InvalidOperationException($"Cannot create disposable pipe: {error}");
            }

            Client = new NamedPipeClientStream(".", name, PipeDirection.Out);
            try
            {
                Client.Connect(1000);
                if (!ConnectNamedPipe(server, IntPtr.Zero) &&
                    Marshal.GetLastWin32Error() != 535) // ERROR_PIPE_CONNECTED
                {
                    throw new InvalidOperationException("Disposable pipe did not connect.");
                }

                Device = (HidDevice)RuntimeHelpers.GetUninitializedObject(typeof(HidDevice));
                typeof(HidDevice).GetField("safeReadHandle",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Device, server);
            }
            catch
            {
                Client.Dispose();
                server.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            Client.Dispose();
            server.Dispose();
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateNamedPipeW(string name,
        uint openMode, uint pipeMode, uint maxInstances, uint outBufferSize,
        uint inBufferSize, uint defaultTimeout, IntPtr securityAttributes);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConnectNamedPipe(SafeFileHandle pipe,
        IntPtr overlapped);
}
