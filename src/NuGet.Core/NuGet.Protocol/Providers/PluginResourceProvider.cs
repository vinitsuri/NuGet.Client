// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol.Plugins;

namespace NuGet.Protocol.Core.Types
{
    // Unsealed for testing purposes.
    public class PluginResourceProvider : ResourceProvider, IDisposable
    {
        private const string _environmentVariable = "NUGET_PLUGIN_PATHS";

        private Lazy<PluginDiscoverer> _discoverer;
        private bool _isDisposed;
        private PluginFactory _pluginFactory;
        private ConcurrentDictionary<string, Lazy<Task<IReadOnlyList<OperationClaim>>>> _pluginOperationClaims;
        private ConcurrentDictionary<string, Lazy<IPluginMulticlientUtilities>> _pluginUtilities;
        private string _rawPluginPaths;

        private static Lazy<int> _currentProcessId = new Lazy<int>(GetCurrentProcessId);

        public static IEnvironmentVariableReader EnvironmentVariableReader { get; private set; }

        public PluginResourceProvider()
            : base(typeof(PluginResource), nameof(PluginResourceProvider))
        {
            Reinitialize(new EnvironmentVariableWrapper());
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                if (_discoverer.IsValueCreated)
                {
                    _discoverer.Value.Dispose();
                }

                _pluginFactory.Dispose();

                _isDisposed = true;
            }
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(
            SourceRepository source,
            CancellationToken cancellationToken)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            cancellationToken.ThrowIfCancellationRequested();

            PluginResource resource = null;

            // Fast path
            if (source.PackageSource.IsHttp && IsPluginPossiblyAvailable())
            {
                var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(cancellationToken);

                if (serviceIndex != null)
                {
                    var results = await _discoverer.Value.DiscoverAsync(cancellationToken);

                    var pluginCreationResults = await GetPluginsForPackageSourceAsync(
                        source.PackageSource.Source,
                        serviceIndex,
                        results,
                        cancellationToken);

                    if (pluginCreationResults.Any())
                    {
                        resource = new PluginResource(pluginCreationResults, source.PackageSource);
                    }
                }
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }

        /// <summary>
        /// Reinitializes static state.
        /// </summary>
        /// <remarks>This is non-private only to facilitate unit testing.</remarks>
        /// <param name="reader">An environment variable reader.</param>
        public void Reinitialize(IEnvironmentVariableReader reader)
        {
            EnvironmentVariableReader = reader;
            _rawPluginPaths = reader?.GetEnvironmentVariable(_environmentVariable);
            _discoverer = new Lazy<PluginDiscoverer>(InitializeDiscoverer);
            _pluginFactory = new PluginFactory(PluginConstants.IdleTimeout);
            _pluginOperationClaims = new ConcurrentDictionary<string, Lazy<Task<IReadOnlyList<OperationClaim>>>>(
                StringComparer.OrdinalIgnoreCase);
            _pluginUtilities = new ConcurrentDictionary<string, Lazy<IPluginMulticlientUtilities>>(
                StringComparer.OrdinalIgnoreCase);
        }

        private async Task<IEnumerable<PluginCreationResult>> GetPluginsForPackageSourceAsync(
            string packageSourceRepository,
            ServiceIndexResourceV3 serviceIndex,
            IEnumerable<PluginDiscoveryResult> results,
            CancellationToken cancellationToken)
        {
            var pluginCreationResults = new List<PluginCreationResult>();
            var serviceIndexJson = JObject.Parse(serviceIndex.Json);

            foreach (var result in results)
            {
                PluginCreationResult pluginCreationResult = null;

                var state = PluginUtilities.IsDebuggingPlugin() ? PluginFileState.Valid : result.PluginFile.State;

                if (state == PluginFileState.Valid)
                {
                    var plugin = await _pluginFactory.GetOrCreateAsync(
                        result.PluginFile.Path,
                        PluginConstants.PluginArguments,
                        new RequestHandlers(),
                        ConnectionOptions.CreateDefault(),
                        cancellationToken);

                    var utilities = _pluginUtilities.GetOrAdd(
                        plugin.Id,
                        path => new Lazy<IPluginMulticlientUtilities>(
                            () => new PluginMulticlientUtilities()));

                    await utilities.Value.DoOncePerPluginLifetimeAsync(
                        MessageMethod.MonitorNuGetProcessExit.ToString(),
                        () => plugin.Connection.SendRequestAndReceiveResponseAsync<MonitorNuGetProcessExitRequest, MonitorNuGetProcessExitResponse>(
                            MessageMethod.MonitorNuGetProcessExit,
                            new MonitorNuGetProcessExitRequest(_currentProcessId.Value),
                            cancellationToken),
                        cancellationToken);

                    await utilities.Value.DoOncePerPluginLifetimeAsync(
                        MessageMethod.Initialize.ToString(),
                        () => InitializePluginAsync(plugin, cancellationToken),
                        cancellationToken);

                    var lazyOperationClaims = _pluginOperationClaims.GetOrAdd(
                        result.PluginFile.Path,
                        filePath => new Lazy<Task<IReadOnlyList<OperationClaim>>>(() => GetPluginOperationClaimsAsync(
                            plugin,
                            packageSourceRepository,
                            serviceIndexJson,
                            cancellationToken)));

                    await lazyOperationClaims.Value;

                    pluginCreationResult = new PluginCreationResult(plugin, utilities.Value, lazyOperationClaims.Value.Result);
                }
                else
                {
                    pluginCreationResult = new PluginCreationResult(result.Message);
                }

                pluginCreationResults.Add(pluginCreationResult);
            }

            return pluginCreationResults;
        }

        private async Task<IReadOnlyList<OperationClaim>> GetPluginOperationClaimsAsync(
            IPlugin plugin,
            string packageSourceRepository,
            JObject serviceIndex,
            CancellationToken cancellationToken)
        {
            var payload = new GetOperationClaimsRequest(packageSourceRepository, serviceIndex);

            var response = await plugin.Connection.SendRequestAndReceiveResponseAsync<GetOperationClaimsRequest, GetOperationClaimsResponse>(
                MessageMethod.GetOperationClaims,
                payload,
                cancellationToken);

            return response.Claims;
        }

        private PluginDiscoverer InitializeDiscoverer()
        {
            var verifier = EmbeddedSignatureVerifier.Create();

            return new PluginDiscoverer(_rawPluginPaths, verifier);
        }

        private bool IsPluginPossiblyAvailable()
        {
            return !string.IsNullOrEmpty(_rawPluginPaths);
        }

        private static int GetCurrentProcessId()
        {
            using (var process = Process.GetCurrentProcess())
            {
                return process.Id;
            }
        }

        private static async Task InitializePluginAsync(
            IPlugin plugin,
            CancellationToken cancellationToken)
        {
            var clientVersion = MinClientVersionUtility.GetNuGetClientVersion().ToNormalizedString();
            var culture = CultureInfo.CurrentCulture.Name;
            var payload = new InitializeRequest(
                clientVersion,
                culture,
                Verbosity.Detailed,
                PluginConstants.RequestTimeout);

            var response = await plugin.Connection.SendRequestAndReceiveResponseAsync<InitializeRequest, InitializeResponse>(
                MessageMethod.Initialize,
                payload,
                cancellationToken);

            if (response.ResponseCode != MessageResponseCode.Success)
            {
                throw new PluginException(Strings.Plugin_InitializationFailed);
            }

            plugin.Connection.Options.SetRequestTimeout(PluginConstants.RequestTimeout);
        }
    }
}