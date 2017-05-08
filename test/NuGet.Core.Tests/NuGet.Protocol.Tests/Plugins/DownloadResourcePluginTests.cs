// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Moq;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class DownloadResourcePluginTests
    {
        private readonly PluginCredentialProvider _credentialProvider;
        private readonly Mock<ICredentialService> _credentialService;
        private readonly Mock<HttpHandlerResource> _httpHandlerResource;
        private readonly PackageSource _packageSource;
        private readonly Mock<IPlugin> _plugin;
        private readonly Mock<IPluginMulticlientUtilities> _utilities;

        public DownloadResourcePluginTests()
        {
            _packageSource = new PackageSource(source: "");
            _httpHandlerResource = new Mock<HttpHandlerResource>();
            _credentialService = new Mock<ICredentialService>();
            _credentialProvider = new PluginCredentialProvider(
                _packageSource,
                _httpHandlerResource.Object,
                _credentialService.Object);
            _plugin = new Mock<IPlugin>();
            _utilities = new Mock<IPluginMulticlientUtilities>();
        }

        [Fact]
        public void Constructor_ThrowsForNullPlugin()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new DownloadResourcePlugin(
                    plugin: null,
                    utilities: _utilities.Object,
                    packageSource: _packageSource,
                    credentialProvider: _credentialProvider));

            Assert.Equal("plugin", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForNullPluginMulticlientUtilities()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new DownloadResourcePlugin(
                    _plugin.Object,
                    utilities: null,
                    packageSource: _packageSource,
                    credentialProvider: _credentialProvider));

            Assert.Equal("utilities", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForNullPackageSource()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new DownloadResourcePlugin(
                    _plugin.Object,
                    _utilities.Object,
                    packageSource: null,
                    credentialProvider: _credentialProvider));

            Assert.Equal("packageSource", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForNullCredentialProvider()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new DownloadResourcePlugin(
                    _plugin.Object,
                    _utilities.Object,
                    _packageSource,
                    credentialProvider: null));

            Assert.Equal("credentialProvider", exception.ParamName);
        }
    }
}