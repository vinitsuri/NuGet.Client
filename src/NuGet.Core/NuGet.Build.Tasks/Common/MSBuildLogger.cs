// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Common;

namespace NuGet.Build
{
    /// <summary>
    /// TaskLoggingHelper -> ILogger
    /// </summary>
    internal class MSBuildLogger : LoggerBase, Common.ILogger
    {
        private readonly TaskLoggingHelper _taskLogging;

        public MSBuildLogger(TaskLoggingHelper taskLogging)
        {
            _taskLogging = taskLogging;
        }

        public override void Log(ILogMessage message)
        {
            if (DisplayMessage(message.Level))
            {
                LogForMono(message);
            }
        }

        /// <summary>
        /// Log using basic methods to avoid missing methods on mono.
        /// </summary>
        private void LogForMono(ILogMessage message)
        {
            switch (message.Level)
            {
                case LogLevel.Error:
                    _taskLogging.LogError(message.Message);
                    break;

                case LogLevel.Warning:
                    _taskLogging.LogWarning(message.Message);
                    break;

                case LogLevel.Minimal:
                    _taskLogging.LogMessage(MessageImportance.High, message.Message);
                    break;

                case LogLevel.Information:
                    _taskLogging.LogMessage(MessageImportance.Normal, message.Message);
                    break;

                case LogLevel.Debug:
                case LogLevel.Verbose:
                default:
                    // Default to LogLevel.Debug and low importance
                    _taskLogging.LogMessage(MessageImportance.Low, message.Message);
                    break;
            }

            return;
        }

        public override System.Threading.Tasks.Task LogAsync(ILogMessage message)
        {
            Log(message);

            return System.Threading.Tasks.Task.FromResult(0);
        }
    }
}