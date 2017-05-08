// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// A message dispatcher that maintains state for outstanding requests
    /// and routes messages to configured request handlers.
    /// </summary>
    public sealed class MessageDispatcher : IMessageDispatcher, IResponseHandler
    {
        private IConnection _connection;
        private readonly IIdGenerator _idGenerator;
        private bool _isDisposed;
        private readonly ConcurrentDictionary<string, InboundRequestContext> _inboundRequestContexts;
        private readonly ConcurrentDictionary<string, OutboundRequestContext> _outboundRequestContexts;

        /// <summary>
        /// Gets the request handlers for use by the dispatcher.
        /// </summary>
        public IRequestHandlers RequestHandlers { get; }

        /// <summary>
        /// Instantiates a new <see cref="MessageDispatcher" /> class.
        /// </summary>
        /// <param name="requestHandlers">Request handlers.</param>
        /// <param name="idGenerator">A unique identifier generator.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="requestHandlers" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="idGenerator" />
        /// is <c>null</c>.</exception>
        public MessageDispatcher(IRequestHandlers requestHandlers, IIdGenerator idGenerator)
        {
            if (requestHandlers == null)
            {
                throw new ArgumentNullException(nameof(requestHandlers));
            }

            if (idGenerator == null)
            {
                throw new ArgumentNullException(nameof(idGenerator));
            }

            RequestHandlers = requestHandlers;
            _idGenerator = idGenerator;

            _inboundRequestContexts = new ConcurrentDictionary<string, InboundRequestContext>();
            _outboundRequestContexts = new ConcurrentDictionary<string, OutboundRequestContext>();
        }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            SetConnection(connection: null);

            GC.SuppressFinalize(this);

            _isDisposed = true;
        }

        public Message CreateMessage(MessageType type, MessageMethod method)
        {
            var requestId = _idGenerator.GenerateUniqueId();

            return MessageUtilities.Create(requestId, type, method);
        }

        public Message CreateMessage<TPayload>(MessageType type, MessageMethod method, TPayload payload)
            where TPayload : class
        {
            var requestId = _idGenerator.GenerateUniqueId();

            return MessageUtilities.Create(requestId, type, method, payload);
        }

        /// <summary>
        /// Asynchronously dispatches a cancellation request for the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task DispatchCancelAsync(Message request, CancellationToken cancellationToken)
        {
            // Capture _connection as SetConnection(...) could null it out later.
            var connection = _connection;

            if (connection == null)
            {
                return Task.FromResult(0);
            }

            return DispatchCancelAsync(connection, request, cancellationToken);
        }

        /// <summary>
        /// Asynchronously dispatches a fault notification for the specified request.
        /// </summary>
        /// <param name="request">The cancel request.</param>
        /// <param name="fault">The fault payload.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task DispatchFaultAsync(Message request, Fault fault, CancellationToken cancellationToken)
        {
            // Capture _connection as SetConnection(...) could null it out later.
            var connection = _connection;

            if (connection == null)
            {
                return Task.FromResult(0);
            }

            return DispatchFaultAsync(connection, request, fault, cancellationToken);
        }

        /// <summary>
        /// Asynchronously dispatches a progress notification for the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="progress">The progress payload.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task DispatchProgressAsync(Message request, Progress progress, CancellationToken cancellationToken)
        {
            // Capture _connection as SetConnection(...) could null it out later.
            var connection = _connection;

            if (connection == null)
            {
                return Task.FromResult(0);
            }

            return DispatchProgressAsync(connection, request, progress, cancellationToken);
        }

        /// <summary>
        /// Asynchronously dispatches a request.
        /// </summary>
        /// <typeparam name="TOutbound">The request payload type.</typeparam>
        /// <typeparam name="TInbound">The expected response payload type.</typeparam>
        /// <param name="method">The request method.</param>
        /// <param name="payload">The request payload.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns a <typeparamref name="TInbound" />
        /// from the target.</returns>
        public Task<TInbound> DispatchRequestAsync<TOutbound, TInbound>(
            MessageMethod method,
            TOutbound payload,
            CancellationToken cancellationToken)
            where TOutbound : class
            where TInbound : class
        {
            // Capture _connection as SetConnection(...) could null it out later.
            var connection = _connection;

            if (connection == null)
            {
                return Task.FromResult<TInbound>(null);
            }

            return DispatchWithNewContextAsync<TOutbound, TInbound>(
                connection,
                MessageType.Request,
                method,
                payload,
                cancellationToken);
        }

        /// <summary>
        /// Asynchronously dispatches a response.
        /// </summary>
        /// <typeparam name="TOutbound">The request payload type.</typeparam>
        /// <param name="request">The associated request.</param>
        /// <param name="responsePayload">The response payload.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task DispatchResponseAsync<TOutbound>(
            Message request,
            TOutbound responsePayload,
            CancellationToken cancellationToken)
            where TOutbound : class
        {
            // Capture _connection as SetConnection(...) could null it out later.
            var connection = _connection;

            if (connection == null)
            {
                return Task.FromResult(0);
            }

            return DispatchAsync(connection, MessageType.Response, request, responsePayload, cancellationToken);
        }

        /// <summary>
        /// Sets the connection to be used for dispatching messages.
        /// </summary>
        /// <param name="connection">A connection instance.  Can be <c>null</c>.</param>
        public void SetConnection(IConnection connection)
        {
            if (_connection == connection)
            {
                return;
            }

            if (_connection != null)
            {
                _connection.MessageReceived -= OnMessageReceived;
            }

            _connection = connection;

            if (_connection != null)
            {
                _connection.MessageReceived += OnMessageReceived;
            }
        }

        Task IResponseHandler.SendResponseAsync<TPayload>(
            Message request,
            TPayload payload,
            CancellationToken cancellationToken)
        {
            return DispatchResponseAsync(request, payload, cancellationToken);
        }

        private async Task DispatchAsync<TOutgoing>(
            IConnection connection,
            MessageType type,
            Message request,
            TOutgoing payload,
            CancellationToken cancellationToken)
            where TOutgoing : class
        {
            InboundRequestContext requestContext;

            if (!_inboundRequestContexts.TryGetValue(request.RequestId, out requestContext))
            {
                return;
            }

            var message = MessageUtilities.Create(request.RequestId, type, request.Method, payload);

            try
            {
                await connection.SendAsync(message, cancellationToken);
            }
            finally
            {
                RemoveInboundRequestContext(request.RequestId);
            }
        }

        private async Task DispatchCancelAsync(
            IConnection connection,
            Message request,
            CancellationToken cancellationToken)
        {
            var message = new Message(request.RequestId, MessageType.Cancel, request.Method);

            await DispatchWithExistingContextAsync(connection, message, cancellationToken);
        }

        private async Task DispatchFaultAsync(
            IConnection connection,
            Message request,
            Fault fault,
            CancellationToken cancellationToken)
        {
            Message message;

            var jsonPayload = JsonSerializationUtilities.FromObject(fault);

            if (request == null)
            {
                var requestId = _idGenerator.GenerateUniqueId();

                message = new Message(requestId, MessageType.Fault, MessageMethod.None, jsonPayload);

                await connection.SendAsync(message, cancellationToken);
            }
            else
            {
                message = new Message(request.RequestId, MessageType.Fault, request.Method, jsonPayload);

                await DispatchWithExistingContextAsync(connection, message, cancellationToken);
            }
        }

        private async Task DispatchProgressAsync(
            IConnection connection,
            Message request,
            Progress progress,
            CancellationToken cancellationToken)
        {
            var message = MessageUtilities.Create(request.RequestId, MessageType.Progress, request.Method, progress);

            await DispatchWithExistingContextAsync(connection, message, cancellationToken);
        }

        private async Task DispatchWithoutContextAsync<TOutgoing>(
            IConnection connection,
            MessageType type,
            MessageMethod method,
            TOutgoing payload,
            CancellationToken cancellationToken)
            where TOutgoing : class
        {
            var message = CreateMessage(type, method, payload);

            await connection.SendAsync(message, cancellationToken);
        }

        private async Task DispatchWithExistingContextAsync(
            IConnection connection,
            Message response,
            CancellationToken cancellationToken)
        {
            OutboundRequestContext requestContext;

            if (!_outboundRequestContexts.TryGetValue(response.RequestId, out requestContext))
            {
                throw new ProtocolException(
                    string.Format(CultureInfo.CurrentCulture, Strings.Plugin_RequestContextDoesNotExist, response.RequestId));
            }

            await connection.SendAsync(response, cancellationToken);
        }

        private async Task<TIncoming> DispatchWithNewContextAsync<TIncoming>(
            IConnection connection,
            MessageType type,
            MessageMethod method,
            CancellationToken cancellationToken)
            where TIncoming : class
        {
            var message = CreateMessage(type, method);
            var timeout = GetRequestTimeout(connection, type, method);
            var isKeepAlive = GetIsKeepAlive(connection, type, method);
            var requestContext = CreateOutboundRequestContext<TIncoming>(
                message,
                timeout,
                isKeepAlive,
                cancellationToken);

            _outboundRequestContexts.TryAdd(message.RequestId, requestContext);

            switch (type)
            {
                case MessageType.Request:
                case MessageType.Response:
                case MessageType.Fault:
                    try
                    {
                        await connection.SendAsync(message, cancellationToken);

                        return await requestContext.CompletionTask;
                    }
                    finally
                    {
                        RemoveOutboundRequestContext(message.RequestId);
                    }

                default:
                    break;
            }

            return null;
        }

        private async Task<TIncoming> DispatchWithNewContextAsync<TOutgoing, TIncoming>(
            IConnection connection,
            MessageType type,
            MessageMethod method,
            TOutgoing payload,
            CancellationToken cancellationToken)
            where TOutgoing : class
            where TIncoming : class
        {
            var message = CreateMessage(type, method, payload);
            var timeout = GetRequestTimeout(connection, type, method);
            var isKeepAlive = GetIsKeepAlive(connection, type, method);
            var requestContext = CreateOutboundRequestContext<TIncoming>(
                message,
                timeout,
                isKeepAlive,
                cancellationToken);

            _outboundRequestContexts.TryAdd(message.RequestId, requestContext);

            switch (type)
            {
                case MessageType.Request:
                case MessageType.Response:
                case MessageType.Fault:
                    try
                    {
                        await connection.SendAsync(message, cancellationToken);

                        return await requestContext.CompletionTask;
                    }
                    finally
                    {
                        RemoveOutboundRequestContext(message.RequestId);
                    }

                default:
                    break;
            }

            return null;
        }

        private void OnMessageReceived(object sender, MessageEventArgs e)
        {
            // Capture _connection as SetConnection(...) could null it out later.
            var connection = _connection;

            if (connection == null)
            {
                return;
            }

            OutboundRequestContext requestContext;

            if (_outboundRequestContexts.TryGetValue(e.Message.RequestId, out requestContext))
            {
                switch (e.Message.Type)
                {
                    case MessageType.Response:
                        requestContext.HandleResponse(e.Message);
                        break;

                    case MessageType.Progress:
                        requestContext.HandleProgress(e.Message);
                        break;

                    case MessageType.Fault:
                        requestContext.HandleFault(e.Message);
                        break;

                    case MessageType.Cancel:
                        requestContext.HandleCancel();
                        break;

                    default:
                        throw new ProtocolException(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.Plugin_UnrecognizedMessageType,
                                e.Message.Type));
                }

                return;
            }

            switch (e.Message.Type)
            {
                case MessageType.Request:
                    HandleInboundRequest(connection, e.Message);
                    break;

                case MessageType.Fault:
                    break;

                default:
                    throw new ProtocolException(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Plugin_UnrecognizedMessageType,
                            e.Message.Type));
            }
        }

        private void HandleInboundRequest(IConnection connection, Message message)
        {
            var cancellationToken = CancellationToken.None;
            IRequestHandler requestHandler = null;
            ProtocolException exception = null;

            try
            {
                requestHandler = GetInboundRequestHandler(message.Method);
                cancellationToken = requestHandler.CancellationToken;
            }
            catch (ProtocolException ex)
            {
                exception = ex;
            }

            InboundRequestContext requestContext = null;

            switch (message.Method)
            {
                case MessageMethod.CopyNupkgFile:
                    requestContext = CreateInboundRequestContext<CopyNupkgFileResponse>(message, cancellationToken);
                    break;
                case MessageMethod.CopyPackageFiles:
                    requestContext = CreateInboundRequestContext<CopyPackageFilesResponse>(message, cancellationToken);
                    break;
                case MessageMethod.DownloadPackage:
                    requestContext = CreateInboundRequestContext<CopyPackageFilesResponse>(message, cancellationToken);
                    break;
                case MessageMethod.GetCredential:
                    requestContext = CreateInboundRequestContext<GetCredentialsResponse>(message, cancellationToken);
                    break;
                case MessageMethod.GetFileInPackage:
                    requestContext = CreateInboundRequestContext<GetFileInPackageResponse>(message, cancellationToken);
                    break;
                case MessageMethod.GetFilesInPackage:
                    requestContext = CreateInboundRequestContext<GetFilesInPackageResponse>(message, cancellationToken);
                    break;
                case MessageMethod.GetOperationClaims:
                    requestContext = CreateInboundRequestContext<GetOperationClaimsResponse>(message, cancellationToken);
                    break;
                case MessageMethod.GetPackageVersions:
                    requestContext = CreateInboundRequestContext<GetPackageVersionsResponse>(message, cancellationToken);
                    break;
                case MessageMethod.Handshake:
                    requestContext = CreateInboundRequestContext<HandshakeResponse>(message, cancellationToken);
                    break;
                case MessageMethod.Initialize:
                    requestContext = CreateInboundRequestContext<InitializeResponse>(message, cancellationToken);
                    break;
                case MessageMethod.Log:
                    requestContext = CreateInboundRequestContext<LogResponse>(message, cancellationToken);
                    break;
                case MessageMethod.MonitorNuGetProcessExit:
                    requestContext = CreateInboundRequestContext<MonitorNuGetProcessExitResponse>(message, cancellationToken);
                    break;
                case MessageMethod.PrefetchPackage:
                    requestContext = CreateInboundRequestContext<PrefetchPackageResponse>(message, cancellationToken);
                    break;
                case MessageMethod.SetPackageSourceCredentials:
                    requestContext = CreateInboundRequestContext<SetCredentialsResponse>(message, cancellationToken);
                    break;
                case MessageMethod.Shutdown:
                    requestContext = CreateInboundRequestContext<NullPayload>(message, cancellationToken);
                    break;
                default:
                    throw new ProtocolException("Unexpected message method.");
            }

            if (exception == null && requestHandler != null)
            {
                _inboundRequestContexts.TryAdd(message.RequestId, requestContext);

                requestContext.BeginResponseAsync(message, requestHandler, this);
            }
            else
            {
                requestContext.BeginFaultAsync(connection, message, exception);
            }
        }

        private IRequestHandler GetInboundRequestHandler(MessageMethod method)
        {
            IRequestHandler handler;

            if (!RequestHandlers.TryGet(method, out handler))
            {
                throw new ProtocolException(
                    string.Format(CultureInfo.CurrentCulture, Strings.Plugin_RequestHandlerDoesNotExist, method));
            }

            return handler;
        }

        private OutboundRequestContext GetOutboundRequestContext(string requestId)
        {
            OutboundRequestContext requestContext;

            if (!_outboundRequestContexts.TryGetValue(requestId, out requestContext))
            {
                throw new ProtocolException(
                    string.Format(CultureInfo.CurrentCulture, Strings.Plugin_RequestContextDoesNotExist, requestId));
            }

            return requestContext;
        }

        private void RemoveInboundRequestContext(string requestId)
        {
            InboundRequestContext requestContext;

            if (_inboundRequestContexts.TryRemove(requestId, out requestContext))
            {
                requestContext.Dispose();
            }
        }

        private void RemoveOutboundRequestContext(string requestId)
        {
            OutboundRequestContext requestContext;

            if (_outboundRequestContexts.TryRemove(requestId, out requestContext))
            {
                requestContext.Dispose();
            }
        }

        private InboundRequestContext<TOutgoing> CreateInboundRequestContext<TOutgoing>(
            Message message,
            CancellationToken cancellationToken)
        {
            return new InboundRequestContext<TOutgoing>(
                _connection,
                message.RequestId,
                cancellationToken);
        }

        private OutboundRequestContext<TIncoming> CreateOutboundRequestContext<TIncoming>(
            Message message,
            TimeSpan? timeout,
            bool isKeepAlive,
            CancellationToken cancellationToken)
        {
            return new OutboundRequestContext<TIncoming>(
                _connection,
                message,
                timeout,
                isKeepAlive,
                cancellationToken);
        }

        private static bool GetIsKeepAlive(IConnection connection, MessageType type, MessageMethod method)
        {
            if (type == MessageType.Request && method == MessageMethod.Handshake)
            {
                return false;
            }

            return true;
        }

        private static TimeSpan GetRequestTimeout(IConnection connection, MessageType type, MessageMethod method)
        {
            if (type == MessageType.Request && method == MessageMethod.Handshake)
            {
                return connection.Options.HandshakeTimeout;
            }

            return connection.Options.RequestTimeout;
        }

        private abstract class InboundRequestContext : IDisposable
        {
            internal string RequestId { get; }

            internal InboundRequestContext(string requestId)
            {
                RequestId = requestId;
            }

            public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }

            protected abstract void Dispose(bool disposing);

            internal abstract void BeginFaultAsync(IConnection connection, Message message, Exception ex);
            internal abstract void BeginProgressAsync(Message message, IRequestHandler requestHandler);
            internal abstract void BeginResponseAsync(
                Message message,
                IRequestHandler requestHandler,
                IResponseHandler responseHandler);
        }

        private sealed class InboundRequestContext<TResult> : InboundRequestContext
        {
            private readonly CancellationToken _cancellationToken;
            private readonly CancellationTokenSource _cancellationTokenSource;
            private readonly IConnection _connection;
            private bool _isDisposed;
            private Task _responseTask;

            internal InboundRequestContext(
                IConnection connection,
                string requestId,
                CancellationToken cancellationToken)
                : base(requestId)
            {
                _connection = connection;

                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                // Capture the cancellation token now because if the cancellation token source
                // is disposed race conditions may cause an exception acccessing its Token property.
                _cancellationToken = _cancellationTokenSource.Token;
            }

            protected override void Dispose(bool disposing)
            {
                if (_isDisposed)
                {
                    return;
                }

                if (disposing)
                {
                    using (_cancellationTokenSource)
                    {
                        _cancellationTokenSource.Cancel();
                    }
                }

                _isDisposed = true;
            }

            internal override void BeginFaultAsync(IConnection connection, Message message, Exception ex)
            {
                var responsePayload = new Fault(ex.Message);
                var response = new Message(
                    message.RequestId,
                    MessageType.Fault,
                    message.Method,
                    JsonSerializationUtilities.FromObject(responsePayload));

                _responseTask = Task.Run(() => connection.SendAsync(
                        response,
                        _cancellationToken),
                    _cancellationToken);
            }

            internal override void BeginProgressAsync(Message message, IRequestHandler requestHandler)
            {
                Task.Run(() => requestHandler.HandleProgressAsync(
                        _connection,
                        message,
                        _cancellationToken),
                    _cancellationToken);
            }

            internal override void BeginResponseAsync(
                Message message,
                IRequestHandler requestHandler,
                IResponseHandler responseHandler)
            {
                _responseTask = Task.Run(async () =>
                    {
                        try
                        {
                            await requestHandler.HandleResponseAsync(
                                _connection,
                                message,
                                responseHandler,
                                _cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            BeginFaultAsync(_connection, message, ex);
                        }
                    },
                    _cancellationToken);
            }
        }

        private abstract class OutboundRequestContext : IDisposable
        {
            internal string RequestId { get; }

            internal OutboundRequestContext(string requestId)
            {
                RequestId = requestId;
            }

            public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }

            protected abstract void Dispose(bool disposing);

            internal abstract void HandleProgress(Message message);
            internal abstract void HandleResponse(Message message);
            internal abstract void BeginFaultAsync(IConnection connection, Message message, Exception ex);
            internal abstract void HandleFault(Message message);
            internal abstract void HandleCancel();
        }

        private sealed class OutboundRequestContext<TResult> : OutboundRequestContext
        {
            private readonly CancellationToken _cancellationToken;
            private readonly CancellationTokenSource _cancellationTokenSource;
            private readonly IConnection _connection;
            private bool _isDisposed;
            private bool _isKeepAlive;
            private readonly Message _request;
            private Task _responseTask;
            private readonly TaskCompletionSource<TResult> _taskCompletionSource;
            private readonly TimeSpan? _timeout;
            private readonly Timer _timer;

            internal Task<TResult> CompletionTask => _taskCompletionSource.Task;

            internal OutboundRequestContext(
                IConnection connection,
                Message request,
                TimeSpan? timeout,
                bool isKeepAlive,
                CancellationToken cancellationToken)
                : base(request.RequestId)
            {
                _connection = connection;
                _request = request;
                _taskCompletionSource = new TaskCompletionSource<TResult>();
                _timeout = timeout;
                _isKeepAlive = isKeepAlive;

                if (timeout.HasValue)
                {
                    _timer = new Timer(
                        OnTimeout,
                        state: null,
                        dueTime: timeout.Value,
                        period: Timeout.InfiniteTimeSpan);
                }

                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                _cancellationTokenSource.Token.Register(Close);

                // Capture the cancellation token now because if the cancellation token source
                // is disposed race conditions may cause an exception acccessing its Token property.
                _cancellationToken = _cancellationTokenSource.Token;
            }

            private void OnTimeout(object state)
            {
                Console.WriteLine($"Request {_request.RequestId} timed out.");

                _taskCompletionSource.TrySetCanceled();
            }

            internal override void HandleProgress(Message message)
            {
                var payload = MessageUtilities.DeserializePayload<Progress>(message);

                if (_timeout.HasValue && _isKeepAlive)
                {
                    _timer.Change(_timeout.Value, Timeout.InfiniteTimeSpan);
                }
            }

            internal override void HandleResponse(Message message)
            {
                var payload = MessageUtilities.DeserializePayload<TResult>(message);

                try
                {
                    _taskCompletionSource.SetResult(payload);
                }
                catch (Exception ex)
                {
                    _taskCompletionSource.TrySetException(ex);
                }
            }

            internal override void BeginFaultAsync(IConnection connection, Message message, Exception ex)
            {
                var responsePayload = new Fault(ex.Message);
                var response = new Message(
                    message.RequestId,
                    MessageType.Fault,
                    message.Method,
                    JsonSerializationUtilities.FromObject(responsePayload));

                _responseTask = Task.Run(() => connection.SendAsync(
                        response,
                        _cancellationToken),
                    _cancellationToken);
            }

            internal override void HandleFault(Message message)
            {
                var fault = MessageUtilities.DeserializePayload<Fault>(message);

                throw new ProtocolException(fault.Message);
            }

            internal override void HandleCancel()
            {
                _taskCompletionSource.TrySetCanceled();
            }

            protected override void Dispose(bool disposing)
            {
                if (_isDisposed)
                {
                    return;
                }

                if (disposing)
                {
                    Close();
                }

                _isDisposed = true;
            }

            private void Cancel()
            {

            }

            private void Close()
            {
                _taskCompletionSource.TrySetCanceled();

                if (_timer != null)
                {
                    _timer.Dispose();
                }

                using (_cancellationTokenSource)
                {
                    _cancellationTokenSource.Cancel();
                }
            }
        }

        private sealed class NullPayload
        {
        }
    }
}