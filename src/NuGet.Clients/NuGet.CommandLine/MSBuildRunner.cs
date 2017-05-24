// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.CommandLine
{
    public class MSBuildRunner
    {
        public string MSBuildPath { get; }

        public ILogger Logger { get; }

        public List<ISet<string>> FilterTokens { get; } = new List<ISet<string>>();

        private bool _displayOutput = true;

        public MSBuildRunner(string msbuildPath, IEnumerable<ISet<string>> filterTokens, ILogger logger)
        {
            MSBuildPath = msbuildPath;
            Logger = logger;
            filterTokens.Union(filterTokens);
        }

        public async Task RunAsync(string arguments, TimeSpan timeout)
        {
            var processStartInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                FileName = MSBuildPath,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            await Logger.LogAsync(LogLevel.Debug, $"{processStartInfo.FileName} {processStartInfo.Arguments}");

            using (var process = Process.Start(processStartInfo))
            {
                var errors = new StringBuilder();
                var output = new StringBuilder();
                var errorTask = ConsumeStreamReaderAsync(process.StandardError, LogLevel.Error);
                var outputTask = ConsumeStreamReaderAsync(process.StandardOutput, LogLevel.Information);

                try
                {
                    var finished = process.WaitForExit((int)timeout.TotalMilliseconds);

                    if (!finished)
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (Exception ex)
                        {
                            throw new CommandLineException(
                                LocalizedResourceManager.GetString(nameof(NuGetResources.Error_CannotKillMsBuild)) + " : " +
                                ex.Message,
                                ex);
                        }

                        throw new CommandLineException(
                            LocalizedResourceManager.GetString(nameof(NuGetResources.Error_MsBuildTimedOut)));
                    }
                }
                finally
                {
                    // Log all output
                    await Task.WhenAll(outputTask, errorTask);
                }

                if (process.ExitCode != 0)
                {
                    await Logger.LogAsync(LogLevel.Debug, $"MSBuild exit code: {process.ExitCode}");
                    throw new ExitCodeException(1);
                }
            }
        }

        private async Task ConsumeStreamReaderAsync(StreamReader reader, LogLevel level)
        {
            await Task.Yield();

            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                // Filter out certain terms.
                var filterOutput = level != LogLevel.Error && FilterTokens.Any(e => IsIgnoredOutput(line, e));

                if (_displayOutput && !filterOutput)
                {
                    await Logger.LogAsync(level, line);
                }
            }
        }

        /// <summary>
        /// filter a line if all tokens are provided.
        /// </summary>
        private static bool IsIgnoredOutput(string line, IEnumerable<string> excluded)
        {
            return excluded.All(p => line.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
