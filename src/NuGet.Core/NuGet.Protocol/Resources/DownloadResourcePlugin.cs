// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Plugins;

namespace NuGet.Protocol
{
    /// <summary>
    /// A download resource for plugins.
    /// </summary>
    public sealed class DownloadResourcePlugin : DownloadResource
    {
        private PluginCredentialProvider _credentialProvider;
        private readonly IPlugin _plugin;
        private readonly PackageSource _packageSource;
        private readonly IPluginMulticlientUtilities _utilities;

        /// <summary>
        /// Instantiates a new <see cref="DownloadResourcePlugin" /> class.
        /// </summary>
        /// <param name="plugin">A plugin.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="plugin" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="utilities" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="packageSource" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="credentialProvider" />
        /// is <c>null</c>.</exception>
        public DownloadResourcePlugin(
            IPlugin plugin,
            IPluginMulticlientUtilities utilities,
            PackageSource packageSource,
            PluginCredentialProvider credentialProvider)
        {
            if (plugin == null)
            {
                throw new ArgumentNullException(nameof(plugin));
            }

            if (utilities == null)
            {
                throw new ArgumentNullException(nameof(utilities));
            }

            if (packageSource == null)
            {
                throw new ArgumentNullException(nameof(packageSource));
            }

            if (credentialProvider == null)
            {
                throw new ArgumentNullException(nameof(credentialProvider));
            }

            _plugin = plugin;
            _utilities = utilities;
            _packageSource = packageSource;
            _credentialProvider = credentialProvider;
        }

        /// <summary>
        /// Asynchronously downloads a package.
        /// </summary>
        /// <param name="identity">The package identity.</param>
        /// <param name="downloadContext">A package download context.</param>
        /// <param name="globalPackagesFolder">The path to the global packages folder.</param>
        /// <param name="logger">A logger.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns
        /// a <see cref="DownloadResourceResult" />.</returns>
        public async override Task<DownloadResourceResult> GetDownloadResourceResultAsync(
            PackageIdentity identity,
            PackageDownloadContext downloadContext,
            string globalPackagesFolder,
            ILogger logger,
            CancellationToken token)
        {
            TryAddLogger(_plugin, logger);

            _credentialProvider = TryUpdateCredentialProvider(_plugin, _credentialProvider);

            var response = await _plugin.Connection.SendRequestAndReceiveResponseAsync<PrefetchPackageRequest, PrefetchPackageResponse>(
                MessageMethod.PrefetchPackage,
                new PrefetchPackageRequest(_packageSource.Source, identity.Id, identity.Version.ToNormalizedString()),
                token);

            if (response.ResponseCode == MessageResponseCode.Success)
            {
                var packageReader = new PluginPackageReader(_plugin, identity, _packageSource.Source);

                return new DownloadResourceResult(packageReader, _packageSource.Source);
            }

            if (response.ResponseCode == MessageResponseCode.NotFound)
            {
                return new DownloadResourceResult(DownloadResourceResultStatus.NotFound);
            }

            throw new PluginException($"Plugin failed to download {identity.Id}{identity.Version.ToNormalizedString()}");
        }

        private void TryAddLogger(IPlugin plugin, ILogger logger)
        {
            plugin.Connection.MessageDispatcher.RequestHandlers.TryAdd(MessageMethod.Log, new LogRequestHandler(logger, LogLevel.Debug));
        }

        private static PluginCredentialProvider TryUpdateCredentialProvider(IPlugin plugin, PluginCredentialProvider credentialProvider)
        {
            if (plugin.Connection.MessageDispatcher.RequestHandlers.TryAdd(MessageMethod.GetCredential, credentialProvider))
            {
                return credentialProvider;
            }

            IRequestHandler handler;

            if (plugin.Connection.MessageDispatcher.RequestHandlers.TryGet(MessageMethod.GetCredential, out handler))
            {
                return (PluginCredentialProvider)handler;
            }

            throw new InvalidOperationException();
        }
    }
}