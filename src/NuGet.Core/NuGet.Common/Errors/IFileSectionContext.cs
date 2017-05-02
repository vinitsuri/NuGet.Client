// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Common
{
    /// <summary>
    /// Represents a sub section of a file by marking
    /// the start line and column and the end line and
    /// column.
    /// </summary>
    public interface IFileSectionContext
    {
        /// <summary>
        /// Full path to the file.
        /// </summary>
        string FilePath { get; }

        /// <summary>
        /// Start line number.
        /// </summary>
        int LineNumber { get; }

        /// <summary>
        /// Start column number.
        /// </summary>
        int ColumnNumber { get; }

        /// <summary>
        /// End line number.
        /// </summary>
        int EndLineNumber { get; }

        /// <summary>
        /// End column number.
        /// </summary>
        int EndColumnNumber { get; }
    }
}
