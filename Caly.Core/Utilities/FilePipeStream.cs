using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Caly.Core.Utilities
{
    /// <summary>
    /// Pipe stream to communicate between application instances on the machine.
    /// </summary>
    public sealed class FilePipeStream : IDisposable, IAsyncDisposable
    {
        // https://googleprojectzero.blogspot.com/2019/09/windows-exploitation-tricks-spoofing.html

        private static readonly string _pipeName = $"caly-files-pipe-{Environment.MachineName}-{Environment.UserName}";

        private static readonly Memory<byte> _keyPhrase = "ca1y k3y pa$$"u8.ToArray();

        private readonly NamedPipeServerStream pipeServer = new(_pipeName, PipeDirection.In, 1,
            PipeTransmissionMode.Byte, PipeOptions.CurrentUserOnly);

        public async IAsyncEnumerable<string> ReceivePathAsync([EnumeratorCancellation] CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();

                Memory<byte> pathBuffer = Memory<byte>.Empty;
                try
                {
                    // https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-use-named-pipes-for-network-interprocess-communication
                    // Wait for a client to connect
                    await pipeServer.WaitForConnectionAsync(token);

                    Memory<byte> lengthBuffer = new byte[2];
                    if (await pipeServer.ReadAsync(lengthBuffer, token) != 2)
                    {
                        // TODO - Log
                        continue;
                    }

                    var len = BitConverter.ToUInt16(lengthBuffer.Span);

                    // Read key phrase
                    Memory<byte> keyBuffer = new byte[_keyPhrase.Length];
                    if (await pipeServer.ReadAsync(keyBuffer, token) != _keyPhrase.Length)
                    {
                        // TODO - Log
                        continue;
                    }

                    // Check key phrase
                    if (!keyBuffer.Span.SequenceEqual(_keyPhrase.Span))
                    {
                        // TODO - Log
                        continue;
                    }

                    // Read file path
                    pathBuffer = new byte[len];

                    if (await pipeServer.ReadAsync(pathBuffer, token) != len)
                    {
                        // TODO - Log
                        continue;
                    }
                }
                catch (OperationCanceledException)
                {
                    // No op
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
                finally
                {
                    // We are not connected if operation was canceled
                    if (pipeServer.IsConnected)
                    {
                        pipeServer.Disconnect();
                    }
                }

                if (pathBuffer.Length > 0)
                {
                    yield return Encoding.UTF8.GetString(pathBuffer.Span);
                }
            }
        }

        public void Dispose()
        {
            pipeServer.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await pipeServer.DisposeAsync();
        }

        public static void SendPath(string filePath)
        {
            try
            {
                using (var pipeClient = new NamedPipeClientStream(".", _pipeName,
                           PipeDirection.Out, PipeOptions.CurrentUserOnly,
                           TokenImpersonationLevel.Identification))
                {
                    pipeClient.Connect();

                    Memory<byte> pathBytes = Encoding.UTF8.GetBytes(filePath);
                    if (pathBytes.Length > ushort.MaxValue)
                    {
                        // TODO - Log
                        return;
                    }

                    Memory<byte> lengthBytes = BitConverter.GetBytes((ushort)pathBytes.Length);
                    pipeClient.Write(lengthBytes.Span);
                    pipeClient.Write(_keyPhrase.Span);
                    pipeClient.Write(pathBytes.Span);

                    pipeClient.Flush();
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Server must be running in admin, but not the client
                // Handle the case and display error message
                throw;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}
