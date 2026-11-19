namespace GdUnit4.Tests.Core.Hooks;

using System;
using System.IO;
using System.Text;

using GdUnit4.Core.Execution;
using GdUnit4.Core.Hooks;

using Microsoft.Win32.SafeHandles;

using static Assertions;

[TestSuite]
public class WindowsStdOutHookTest
{
    private const int STD_OUTPUT_HANDLE = -11;

    private bool savedStdOutCaptureState;

    [Before]
    public void Before()
    {
        // The test's own stdout capture would redirect stdout under the hook tested here.
        savedStdOutCaptureState = ExecutionContext.Current!.IsCaptureStdOut;
        ExecutionContext.Current!.IsCaptureStdOut = false;
    }

    [After]
    public void After() => ExecutionContext.Current!.IsCaptureStdOut = savedStdOutCaptureState;

    [TestCase]
    public void StopCaptureEndsTheReader()
    {
        if (!OperatingSystem.IsWindows())
            return;
        using var hook = new WindowsStdOutHook();

        hook.StartCapture();
        hook.StopCapture();

        AssertBool(hook.IsReading).IsFalse();
    }

    [TestCase]
    public void CapturesTheNativeStdOutOfEachCapture()
    {
        if (!OperatingSystem.IsWindows())
            return;
        using var hook = new WindowsStdOutHook();

        for (var capture = 0; capture < 20; capture++)
        {
            hook.StartCapture();
            WriteToNativeStdOut($"capture {capture}\n");
            hook.StopCapture();

            AssertBool(hook.IsReading).IsFalse();
            AssertThat(hook.GetCapturedOutput()).IsEqual($"capture {capture}\n");
        }
    }

    private static void WriteToNativeStdOut(string text)
    {
        using var stdOut = new FileStream(new SafeFileHandle(GetStdHandle(STD_OUTPUT_HANDLE), false), FileAccess.Write, 1);
        var bytes = Encoding.UTF8.GetBytes(text);
        stdOut.Write(bytes, 0, bytes.Length);
        stdOut.Flush();
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int nStdHandle);
}
