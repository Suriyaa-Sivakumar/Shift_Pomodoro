using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace FlowWindows.WindowsIntegration;

public class SingleInstanceManager : IDisposable
{
    private const string MutexName = "Local\\FlowWindows_SingleInstance_Mutex_2026";
    private const string PipeName = "FlowWindows_Instance_Activation_Pipe";

    private Mutex? _mutex;
    private bool _hasHandle = false;
    private CancellationTokenSource? _cts;

    public event Action? InstanceActivated;

    public bool TryAcquire(Action onActivatedCallback)
    {
        InstanceActivated += onActivatedCallback;

        try
        {
            _mutex = new Mutex(true, MutexName, out _hasHandle);
        }
        catch (AbandonedMutexException)
        {
            // Mutex was abandoned by a terminated process. Current thread now owns it!
            _hasHandle = true;
        }
        catch (Exception)
        {
            _hasHandle = false;
        }

        if (!_hasHandle)
        {
            // Another instance may be running. Attempt to send activation signal through named pipe
            bool notified = SendActivationSignal();
            if (notified)
            {
                // Active instance successfully notified
                return false;
            }

            // No active instance responded to pipe. Proceed anyway so user is never locked out.
            _hasHandle = true;
        }

        // Start pipe listener
        _cts = new CancellationTokenSource();
        Task.Run(() => StartPipeListenerAsync(_cts.Token));
        return true;
    }

    private static bool SendActivationSignal()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(500);
            using var writer = new StreamWriter(client);
            writer.WriteLine("ACTIVATE");
            writer.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task StartPipeListenerAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(token);
                using var reader = new StreamReader(server);
                var message = await reader.ReadLineAsync(token);
                if (message == "ACTIVATE")
                {
                    InstanceActivated?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Continue listening
            }
        }
    }

    public void Dispose()
    {
        try
        {
            _cts?.Cancel();
            if (_hasHandle && _mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
                _mutex = null;
                _hasHandle = false;
            }
        }
        catch { }
    }
}

