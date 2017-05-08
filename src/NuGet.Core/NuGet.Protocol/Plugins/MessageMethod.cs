// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol.Plugins
{
    /// <summary>
    /// Message methods.
    /// </summary>
    public enum MessageMethod
    {
        /// <summary>
        /// None
        /// </summary>
        None,

        /// <summary>
        /// Copy a .nupkg file
        /// </summary>
        CopyNupkgFile,

        /// <summary>
        /// Copy package files
        /// </summary>
        CopyPackageFiles,

        /// <summary>
        /// Download a package
        /// </summary>
        DownloadPackage,

        /// <summary>
        /// Get a credential
        /// </summary>
        GetCredential,

        /// <summary>
        /// Get credentials
        /// </summary>
        GetCredentials,

        /// <summary>
        /// Get a file in a package
        /// </summary>
        GetFileInPackage,

        /// <summary>
        /// Get files in a package
        /// </summary>
        GetFilesInPackage,

        /// <summary>
        /// Get operation claims
        /// </summary>
        GetOperationClaims,

        /// <summary>
        /// Get package versions
        /// </summary>
        GetPackageVersions,

        /// <summary>
        /// Handshake
        /// </summary>
        Handshake,

        /// <summary>
        /// Initialize
        /// </summary>
        Initialize,

        /// <summary>
        /// Log
        /// </summary>
        Log,

        /// <summary>
        /// Monitor NuGet process exit
        /// </summary>
        MonitorNuGetProcessExit,

        /// <summary>
        /// Prefetch a package
        /// </summary>
        PrefetchPackage,

        /// <summary>
        /// Set package source credentials
        /// </summary>
        SetPackageSourceCredentials,

        /// <summary>
        /// Shutdown
        /// </summary>
        Shutdown
    }
}