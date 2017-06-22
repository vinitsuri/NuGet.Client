// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// The data available when the <see cref="IRestoreEvents.SolutionRestoreCompleted"/> event is raised.
    /// </summary>
    public class SolutionRestoredEventArgs : EventArgs
    {
        public SolutionRestoredEventArgs(bool isSuccess)
        {
            IsSuccess = isSuccess;
        }

        public bool IsSuccess { get; }
    }
}
