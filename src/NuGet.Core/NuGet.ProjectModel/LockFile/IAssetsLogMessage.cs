// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.ProjectModel
{
    public interface IAssetsLogMessage
    {
        /// <summary>
        /// Warning level.
        /// </summary>
        LogLevel Level { get; }

        /// <summary>
        /// Warning severity.
        /// </summary>
        int WarningLevel { get; }

        /// <summary>
        /// NU warning or error code.
        /// </summary>
        NuGetLogCode Code { get; }

        /// <summary>
        /// Message text.
        /// </summary>
        string Message { get; }

        /// <summary>
        /// Project or Package Id.
        /// </summary>
        /// <remarks>Library Ids are always case insensitive.</remarks>
        string LibraryId { get; }

        /// <summary>
        /// Path to the project file.
        /// </summary>
        string ProjectPath { get; }

        /// <summary>
        /// File path and starting line, column.
        /// </summary>
        IFileSectionContext FileSectionContext { get; }

        /// <summary>
        /// Target graphs the message applies to.
        /// If empty this applies to all graphs.
        /// </summary>
        IReadOnlyList<string> TargetGraphs { get; }
    }
}
