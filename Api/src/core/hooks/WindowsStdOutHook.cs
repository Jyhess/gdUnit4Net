// Copyright (c) 2025 Mike Schulze
// MIT License - See LICENSE file in the repository root for full license text

namespace GdUnit4.Core.Hooks;

using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;

[SuppressMessage(
    "StyleCop.CSharp.OrderingRules",
    "SA1201:Elements should appear in the correct order",
    Justification = "P/Invoke declarations are grouped together for clarity at the end of the class")]
[SuppressMessage(
    "Usage",
    "CA2216:Disposable types should declare finalizer",
    Justification = "The original stdout handle belongs to the process, not to this hook")]
internal sealed class WindowsStdOutHook : IStdOutHook
{
    private const int STD_OUTPUT_HANDLE = -11;

    // A process that inherited the pipe's write end keeps it open: the reader then stops at the next capture's end.
    private static readonly TimeSpan ReaderStopTimeout = TimeSpan.FromSeconds(1);

    private readonly IntPtr originalStdOutHandle;
    private readonly StdOutConsoleHook stdOutHook = new();
    private AnonymousPipeServerStream? pipe;
    private Thread? readThread;
    private bool disposed;

    public WindowsStdOutHook()
    {
        originalStdOutHandle = GetStdHandle(STD_OUTPUT_HANDLE);
        if (originalStdOutHandle == IntPtr.Zero)
            throw new InvalidOperationException("Failed to get original stdout handle.");
    }

    /// <summary>
    ///     Gets a value indicating whether the thread reading the redirected stdout still runs.
    /// </summary>
    internal bool IsReading => readThread?.IsAlive ?? false;

    public void Dispose()
    {
        if (disposed)
            return;
        StopCapture();
        stdOutHook.Dispose();
        disposed = true;
    }

    public void StartCapture()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        StopCapture();

        // Each capture has its own pipe: its reader ends when the capture closes the pipe's write end.
        pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
        if (!SetStdHandle(STD_OUTPUT_HANDLE, pipe.ClientSafePipeHandle.DangerousGetHandle()))
        {
            pipe.Dispose();
            pipe = null;
            throw new InvalidOperationException("Failed to redirect stdout to pipe.");
        }

        stdOutHook.StartCapture();
        var capturePipe = pipe;
        readThread = new Thread(() => ReadPipeOutput(capturePipe)) { IsBackground = true, Name = "GdUnit4 stdout reader" };
        readThread.Start();
    }

    public void StopCapture()
    {
        if (pipe is null)
            return;

        _ = SetStdHandle(STD_OUTPUT_HANDLE, originalStdOutHandle);

        // Closing the write end lets the reader drain what was written, then read the end of the stream.
        pipe.DisposeLocalCopyOfClientHandle();
        _ = readThread?.Join(ReaderStopTimeout);
        readThread = null;
        stdOutHook.StopCapture();

        // A reader still blocked gets an ObjectDisposedException at its next read, which ends it.
        pipe.Dispose();
        pipe = null;
    }

    public string GetCapturedOutput() => stdOutHook.GetCapturedOutput();

    private static void ReadPipeOutput(Stream capturePipe)
    {
        using var reader = new StreamReader(capturePipe, Encoding.UTF8, false, 4096, leaveOpen: true);
        var buffer = new char[4096];
        try
        {
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                Console.Write(buffer, 0, read);
        }
        catch (Exception e) when (e is ObjectDisposedException or IOException)
        {
            // The capture closed the pipe while this thread was reading: what it wrote after its end is not captured.
        }
    }

#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32 | DllImportSearchPath.UserDirectories)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32 | DllImportSearchPath.UserDirectories)]
    private static extern bool SetStdHandle(int nStdHandle, IntPtr hHandle);
#pragma warning restore SYSLIB1054
}
