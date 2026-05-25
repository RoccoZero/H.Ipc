//HintName: ActionServiceClient.IpcClient.generated.cs

#nullable enable

namespace H.Ipc.Apps.Wpf
{
    public partial class ActionServiceClient
    {
        private bool isWaitingForServerResponse = false;

        #region Properties

        private global::H.Pipes.IPipeConnection<string>? _connection;
        private global::H.Pipes.IPipeConnection<string> Connection
        {
            get
            {
                return _connection ?? throw new global::System.InvalidOperationException("You need to call Initialize() first."); 
            }
        }

        #endregion

        #region Events

        public event global::System.EventHandler<global::System.Exception>? ExceptionOccurred;

        private void OnExceptionOccurred(global::System.Exception exception)
        {
            ExceptionOccurred?.Invoke(this, exception);
        }

        #endregion


        private void ThrowIfWaitingForServer()
        {
            if (isWaitingForServerResponse)
            {
                throw new global::System.InvalidOperationException("Cannot perform this operation while waiting for server response.");
            }
        }


        public void Initialize(global::H.Pipes.IPipeConnection<string>? connection)
        {
            _connection = connection ?? throw new global::System.ArgumentNullException(nameof(connection));
        }

        public async System.Threading.Tasks.Task ShowTrayIcon()
        {
            ThrowIfWaitingForServer();
            try
            {
                await WriteAsync(CreateRunMethodRequest(nameof(ShowTrayIcon))).ConfigureAwait(false);
            }
            catch (global::System.Exception exception)
            {
                OnExceptionOccurred(exception);
            }
        }

        public async System.Threading.Tasks.Task HideTrayIcon()
        {
            ThrowIfWaitingForServer();
            try
            {
                await WriteAsync(CreateRunMethodRequest(nameof(HideTrayIcon))).ConfigureAwait(false);
            }
            catch (global::System.Exception exception)
            {
                OnExceptionOccurred(exception);
            }
        }

        public async System.Threading.Tasks.Task SendText(string text)
        {
            ThrowIfWaitingForServer();
            try
            {
                await WriteAsync(CreateRunMethodRequest(nameof(SendText), global::H.IpcGenerators.IpcSerializer.Serialize(text))).ConfigureAwait(false);
            }
            catch (global::System.Exception exception)
            {
                OnExceptionOccurred(exception);
            }
        }

        public async System.Threading.Tasks.Task<int> GetPoints()
        {
            ThrowIfWaitingForServer();

            var tcs = new global::System.Threading.Tasks.TaskCompletionSource<int>();

            void ReceiveResult(object? sender, H.Pipes.Args.ConnectionMessageEventArgs<string?> e)
            {
                var payload = e.Message ?? throw new global::System.ArgumentException("Message property of received H.Pipes.Args.ConnectionMessageEventArgs<string> object is null");
                var result = default(int);
                var resultGeneral = global::H.IpcGenerators.IpcSerializer.Deserialize<global::H.IpcGenerators.ReturnMethodResultRequest>(payload);
                if (resultGeneral?.ResultType == "Int32")
                {
                    if (string.IsNullOrWhiteSpace(resultGeneral.ResultPayload))
                    {
                        throw new global::System.InvalidOperationException("ResultPayload is empty.");
                    }

                    result = global::H.IpcGenerators.IpcSerializer.Deserialize<int>(resultGeneral.ResultPayload);
                }

                Connection.MessageReceived -= ReceiveResult;
                tcs.SetResult(result);
            }

            isWaitingForServerResponse = true;
            try
            {
                Connection.MessageReceived += ReceiveResult;
                await WriteAsync(CreateRunMethodRequest(nameof(GetPoints))).ConfigureAwait(false);

                var result = await tcs.Task;
                isWaitingForServerResponse = false;
                return result;
            }
            catch (global::System.Exception exception)
            {
                isWaitingForServerResponse = false;
                OnExceptionOccurred(exception);
                return await global::System.Threading.Tasks.Task.FromException<int>(exception);
            }
        }

        private async global::System.Threading.Tasks.Task WriteAsync(
            global::H.IpcGenerators.RunMethodRequest method,
            global::System.Threading.CancellationToken cancellationToken = default)
        {
            var payload = global::H.IpcGenerators.IpcSerializer.Serialize(method);
            await Connection.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }

        private static global::H.IpcGenerators.RunMethodRequest CreateRunMethodRequest(
            string name,
            params string[] arguments)
        {
            var request = new global::H.IpcGenerators.RunMethodRequest
            {
                Name = name,
                Arguments = arguments,
            };

            return request;
        }
    }
}