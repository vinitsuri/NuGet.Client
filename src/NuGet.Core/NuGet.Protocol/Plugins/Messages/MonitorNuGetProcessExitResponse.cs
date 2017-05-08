// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Protocol.Plugins
{
    public sealed class MonitorNuGetProcessExitResponse
    {
        [JsonRequired]
        public MessageResponseCode ResponseCode { get; }

        [JsonConstructor]
        public MonitorNuGetProcessExitResponse(MessageResponseCode responseCode)
        {
            ResponseCode = responseCode;
        }
    }
}