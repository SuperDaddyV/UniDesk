using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;
using UniDesk.Hardware.Contracts;

namespace UniDesk.HardwareService;

public sealed class HardwarePipeServer
{
    public const int AcceptLoopCount = 4;

    private readonly HardwareServiceRequestHandler _handler;
    private readonly string _pipeName;
    private readonly bool _useDefaultPipeSecurity;

    public HardwarePipeServer(HardwareServiceRequestHandler handler)
        : this(handler, HardwareIpcProtocol.PipeName, useDefaultPipeSecurity: false)
    {
    }

    internal HardwarePipeServer(
        HardwareServiceRequestHandler handler,
        string pipeName)
        : this(handler, pipeName, useDefaultPipeSecurity: true)
    {
    }

    private HardwarePipeServer(
        HardwareServiceRequestHandler handler,
        string pipeName,
        bool useDefaultPipeSecurity)
    {
        _handler = handler;
        _pipeName = pipeName;
        _useDefaultPipeSecurity = useDefaultPipeSecurity;
    }

    public Task RunAsync(CancellationToken cancellationToken) => Task.WhenAll(
        Enumerable.Range(0, AcceptLoopCount)
            .Select(_ => RunAcceptLoopAsync(cancellationToken)));

    private async Task RunAcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = CreateServer(_pipeName);
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                requestTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                await HandleConnectionAsync(server, requestTimeout.Token).ConfigureAwait(false);
            }
            catch (InvalidDataException)
            {
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }
            catch (IOException) when (!cancellationToken.IsCancellationRequested)
            {
            }
            catch (IOException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task HandleConnectionAsync(
        NamedPipeServerStream server,
        CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(
            server,
            new UTF8Encoding(false),
            bufferSize: 4096,
            leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\n"
        };

        var request = await ReadRequestAsync(server, cancellationToken).ConfigureAwait(false);
        if (request == null)
        {
            return;
        }

        var response = _handler.Handle(request);
        await writer.WriteLineAsync(response.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<string?> ReadRequestAsync(
        Stream stream,
        CancellationToken cancellationToken) =>
        await HardwareIpcProtocol.ReadUtf8LineAsync(
            stream,
            HardwareIpcProtocol.MaxRequestBytes,
            cancellationToken).ConfigureAwait(false);

    private NamedPipeServerStream CreateServer(string pipeName)
    {
        if (_useDefaultPipeSecurity)
        {
            return new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                AcceptLoopCount,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough,
                4096,
                HardwareIpcProtocol.MaxResponseBytes);
        }

        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.NetworkSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Deny));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            AcceptLoopCount,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.WriteThrough,
            4096,
            HardwareIpcProtocol.MaxResponseBytes,
            security,
            HandleInheritability.None,
            (PipeAccessRights)0);
    }
}
