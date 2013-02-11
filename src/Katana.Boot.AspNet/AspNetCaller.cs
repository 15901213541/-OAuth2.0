﻿// <copyright file="AspNetCaller.cs" company="Microsoft Open Technologies, Inc.">
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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Web;

namespace Katana.Boot.AspNet
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    public class AspNetCaller
    {
        public AspNetCaller()
        {
        }

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic",
            Justification = "Must not be static to be detected by Katana.Engine")]
        public Task Invoke(IDictionary<string, object> environment)
        {
            var workerRequest = new KatanaWorkerRequest(environment);
            HttpRuntime.ProcessRequest(workerRequest);
            return workerRequest.Completed;
        }
    }
}
