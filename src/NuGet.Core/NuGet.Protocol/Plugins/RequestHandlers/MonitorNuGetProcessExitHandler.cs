// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Plugins
{
    public sealed class MonitorNuGetProcessExitHandler : IRequestHandler
    {
        private readonly IPlugin _plugin;
        private readonly ConcurrentDictionary<int, Process> _processes;

        public CancellationToken CancellationToken => CancellationToken.None;

        public MonitorNuGetProcessExitHandler(IPlugin plugin)
        {
            _plugin = plugin;
            _processes = new ConcurrentDictionary<int, Process>();
        }

        public Task HandleCancelAsync(IConnection connection, Message request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task HandleProgressAsync(IConnection connection, Message request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public async Task HandleResponseAsync(
            IConnection connection,
            Message request,
            IResponseHandler responseHandler,
            CancellationToken cancellationToken)
        {
            var monitorRequest = MessageUtilities.DeserializePayload<MonitorNuGetProcessExitRequest>(request);

            Process process = null;

            try
            {
                process = _processes.GetOrAdd(monitorRequest.ProcessId, pid => Process.GetProcessById(pid));
            }
            catch (Exception)
            {
            }

            MessageResponseCode responseCode;

            if (process == null)
            {
                responseCode = MessageResponseCode.NotFound;
            }
            else
            {
                process.Exited += OnProcessExited;

                responseCode = MessageResponseCode.Success;
            }

            var response = new MonitorNuGetProcessExitResponse(responseCode);

            await responseHandler.SendResponseAsync(request, response, cancellationToken);
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            _plugin.Dispose();
        }
    }
}