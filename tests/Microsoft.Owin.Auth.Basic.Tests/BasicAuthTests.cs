﻿// <copyright file="BasicAuthTests.cs" company="Katana contributors">
//   Copyright 2011-2012 Katana contributors
// </copyright>
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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Owin.Auth.Basic.Tests
{
    using AppFunc = Func<IDictionary<string, object>, Task>;
    
    public class BasicAuthTests
    {
        private static readonly AppFunc NotImplemented = env => { throw new NotImplementedException(); };

        [Fact]
        public void Ctor_NullParameters_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new BasicAuth(null, new BasicAuth.Options()));
            Assert.Throws<ArgumentNullException>(() => new BasicAuth(NotImplemented, null));
        }
    }
}
