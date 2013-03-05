﻿// <copyright file="ThrottlingOptions.cs" company="Microsoft Open Technologies, Inc.">
// Copyright 2011-2013 Microsoft Open Technologies, Inc. All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using Microsoft.Owin.Throttling.Implementation;

namespace Microsoft.Owin.Throttling
{
    public class ThrottlingOptions
    {
        public ThrottlingOptions()
        {
            // TODO: use processor affinity mask in addition?

            ActiveThreadsBeforeRemoteRequestsQueue = 12 * Environment.ProcessorCount;
            ActiveThreadsBeforeLocalRequestsQueue = 24 * Environment.ProcessorCount;
            QueueLengthBeforeIncomingRequestsRejected = 5000;
            ThreadingServices = new DefaultThreadingServices();
        }

        public int ActiveThreadsBeforeRemoteRequestsQueue { get; set; }
        public int ActiveThreadsBeforeLocalRequestsQueue { get; set; }
        public int QueueLengthBeforeIncomingRequestsRejected { get; set; }

        public IThreadingServices ThreadingServices { get; set; }
    }
}
