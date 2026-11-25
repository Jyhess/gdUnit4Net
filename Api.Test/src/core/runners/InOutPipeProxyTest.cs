namespace GdUnit4.Tests.Core.Runners;

using System;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Api;

using GdUnit4.Core.Commands;
using GdUnit4.Core.Runners;

using Moq;

using static Assertions;

/// <summary>
///     Reads from a pipe whose other side is closed, as when the Godot runtime or its test host disappears.
/// </summary>
[TestSuite]
public class InOutPipeProxyTest
{
    private static readonly TimeSpan ReadDeadline = TimeSpan.FromSeconds(1);

    [TestCase]
    public async Task ReadInDataAnswersGoneWhenTheServerIsClosed()
    {
        var (server, client) = await ConnectedPipes();
        await using var proxy = new TestPipeProxy<NamedPipeClientStream>(client);
        await server.DisposeAsync();

        var data = await proxy.ReadData(CancellationToken.None).WaitAsync(ReadDeadline);

        AssertThat(data is Response { StatusCode: HttpStatusCode.Gone }).IsTrue();
    }

    [TestCase]
    public async Task ReadCommandThrowsIOExceptionWhenTheClientIsClosed()
    {
        var (server, client) = await ConnectedPipes();
        await using var proxy = new TestPipeProxy<NamedPipeServerStream>(server);
        await client.DisposeAsync();

        var thrown = await AssertThrown(proxy.ReadCommand(CancellationToken.None).WaitAsync(ReadDeadline));

        thrown!.IsInstanceOf<IOException>();
    }

    private static async Task<(NamedPipeServerStream Server, NamedPipeClientStream Client)> ConnectedPipes()
    {
        var name = $"gdunit4-test-{Guid.NewGuid():N}";
        var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await Task.WhenAll(server.WaitForConnectionAsync(), client.ConnectAsync(1000));
        return (server, client);
    }

    private sealed class TestPipeProxy<TPipe> : InOutPipeProxy<TPipe>
        where TPipe : PipeStream
    {
        public TestPipeProxy(TPipe pipe)
            : base(pipe, new Mock<ITestEngineLogger>().Object)
        {
        }

        public Task<object?> ReadData(CancellationToken cancellationToken) => ReadInData(cancellationToken);

        public Task<BaseCommand> ReadCommand(CancellationToken cancellationToken) => ReadCommand<BaseCommand>(cancellationToken);
    }
}
