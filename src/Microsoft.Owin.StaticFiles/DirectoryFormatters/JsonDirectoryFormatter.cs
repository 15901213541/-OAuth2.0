﻿// <copyright file="JsonDirectoryFormatter.cs" company="Microsoft Open Technologies, Inc.">
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
using System.Globalization;
using System.Text;

namespace Microsoft.Owin.StaticFiles.DirectoryFormatters
{
    internal class JsonDirectoryFormatter : IDirectoryInfoFormatter
    {
        public string ContentType
        {
            get { return Constants.ApplicationJson; }
        }

        public StringBuilder GenerateContent(string requestPath, FileSystems.IDirectoryInfo directoryInfo)
        {
            if (directoryInfo == null)
            {
                throw new ArgumentNullException("directoryInfo");
            }

            var builder = new StringBuilder();
            builder.Append("{ ");

            builder.AppendFormat("\"path\": \"{0}\", ", requestPath);

            bool firstItem = true;

            builder.Append("\"subdirectories\": [ ");
            foreach (var subdir in directoryInfo.GetDirectories())
            {
                if (!firstItem)
                {
                    builder.Append(", ");
                    firstItem = false;
                }
                builder.AppendFormat("\"name\": \"{0}\"", subdir.Name);
            }
            builder.Append("], ");

            firstItem = true;

            builder.Append("\"files\": [ ");
            foreach (var file in directoryInfo.GetFiles())
            {
                if (!firstItem)
                {
                    builder.Append(", ");
                    firstItem = false;
                }
                builder.Append("{ ");
                builder.AppendFormat("\"name\": \"{0}\", ", file.Name);
                builder.AppendFormat("\"length\": {0}, ", file.Length.ToString(CultureInfo.InvariantCulture));
                builder.AppendFormat("\"lastModified\": \"{0}\" ",
                    file.LastModified.ToUniversalTime().ToString(CultureInfo.InvariantCulture));
                builder.Append("} ");
            }
            builder.Append("] ");

            builder.Append("}");
            return builder;
        }
    }
}
