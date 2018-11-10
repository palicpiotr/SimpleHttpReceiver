using Microsoft.Net.Http.Server;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HttpReceiver
{
    class HttpReceiver
    {
        private const int STATUS_CODE_OK = 200;
        private const int STATUS_CLIENT_ERR = 400;
        private const string HTTP_POST = "POST";

        private readonly int _port;
        private readonly string _address;
        private readonly WebListener _webListener;
        private readonly Dictionary<Guid, Task> _actualTasks = new Dictionary<Guid, Task>();

        private readonly AsyncLock _lock = new AsyncLock();
        private readonly AsyncManualResetEvent _taskListEvent = new AsyncManualResetEvent();
        private readonly object _syncLock = new object();

        private CancellationTokenSource tokenSource = new CancellationTokenSource();

        public HttpReceiver(string httpAddress, int port)
        {
            _port = port;
            _address = httpAddress;
            var settings = new WebListenerSettings();
            settings.UrlPrefixes.Add($"{_address}:{_port}");
            _webListener = new WebListener(settings);
        }

        public void Stop()
        {
            tokenSource.Cancel();

            Task.WaitAll(_actualTasks.Select(x => x.Value).ToArray());
        }

        private static readonly Random rnd = new Random(Guid.NewGuid().GetHashCode());

        public async Task ProcessMessage(Guid taskId, string value, int msgNumber, CancellationToken ct)
        {
            try
            {
                using (await _lock.LockAsync(ct))
                {
                    Console.WriteLine($"Run {msgNumber} save operation");
                    await _taskListEvent.WaitAsync();
                    await Task.Delay(rnd.Next(100, 5000), ct);
                    Console.WriteLine($"{msgNumber} : {value}");

                    lock (_syncLock)
                    {
                        Console.WriteLine($"REMOVE MSG {msgNumber} TASK TO TASK QUEUE");
                        _actualTasks.Remove(taskId);
                    }
                }

            }
            catch (OperationCanceledException)
            {
                //Correct stop by cancel
            }
        }

        public async Task Run()
        {
            int msgNumber = 1;
            _webListener.Start();
            while (!tokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    tokenSource.Token.ThrowIfCancellationRequested();
                    var context = await _webListener.AcceptAsync();
                    var request = context.Request;
                    if (request.Method == HTTP_POST)
                    {
                        string value = string.Empty;

                        try
                        {
                            using (var reader = new StreamReader(request.Body, Encoding.UTF8))
                                value = reader.ReadToEnd();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error read {ex.Message}");
                            context.Response.StatusCode = STATUS_CLIENT_ERR;
                            context.Dispose();
                            continue;
                        }

                        var taskId = Guid.NewGuid();
                        var t = ProcessMessage(taskId, value, msgNumber, tokenSource.Token);
                        lock (_syncLock)
                        {
                            Console.WriteLine($"Add MSG {msgNumber} TASK TO TASK QUEUE");
                            _actualTasks.Add(taskId, t);
                        }
                        _taskListEvent.Set();
                        context.Response.StatusCode = STATUS_CODE_OK;
                        msgNumber++;
                    }
                    else
                    {
                        context.Response.StatusCode = STATUS_CLIENT_ERR;
                    }
                    context.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error {ex.Message}");
                }
            }
        }
    }
}
