﻿// <copyright file="AutoTuneMiddleware.cs" company="Microsoft Open Technologies, Inc.">
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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin.Host.HttpListener;

namespace Katana.Performance.ReferenceApp
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Not disposed until app teardown.")]
    public class AutoTuneMiddleware
    {
        private readonly AppFunc _next;
        private readonly OwinHttpListener _server;
        private Timer _timer;
        private int _requestsProcessed = 0;
        private double _currentMaxAccepts = 5;
        private int _currentMaxRequests = 1000;

        public AutoTuneMiddleware(AppFunc next, OwinHttpListener server)
        {
            _next = next;
            _server = server;
            _server.SetRequestProcessingLimits((int)_currentMaxAccepts, _currentMaxRequests);

            _timer = new Timer(TimerFired, null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0.1));
        }

        public Task Invoke(IDictionary<string, object> environment)
        {
            Interlocked.Increment(ref _requestsProcessed);
            return _next(environment);
        }

        private void TimerFired(object state)
        {
            int requestsProcessed = Interlocked.Exchange(ref _requestsProcessed, 0);

            int maxAccepts, maxRequests;
            _server.GetRequestProcessingLimits(out maxAccepts, out maxRequests);
            Console.WriteLine("Active/MaxAccepts:"
                + maxAccepts + "/" + (int)_currentMaxAccepts
                + ", Active/MaxRequests:"
                + maxRequests + "/" + _currentMaxRequests
                + ", Requests/1sec: " + requestsProcessed);

            _server.SetRequestProcessingLimits((int)(_currentMaxAccepts += 0.1), _currentMaxRequests);
        }
    }
}
