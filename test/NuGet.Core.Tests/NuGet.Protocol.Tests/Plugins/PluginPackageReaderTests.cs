// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class PluginPackageReaderTests
    {
        [Fact]
        public void Constructor_ThrowsForNullPlugin()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new PluginPackageReader(
                    plugin: null,
                    packageIdentity: new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                    packageSourceRepository: "b"));

            Assert.Equal("plugin", exception.ParamName);
        }
    }
}