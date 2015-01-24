﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using FunctionalTests.Common;
using Microsoft.Owin;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.StaticFiles;
using Owin;
using Xunit;
using Xunit.Extensions;

namespace FunctionalTests.Facts.StaticFiles
{
    public class DirectoryBrowser
    {
        [Theory, Trait("FunctionalTests", "General")]
        [InlineData(HostType.HttpListener)]
        public void Static_DirectoryBrowserDefaults(HostType hostType)
        {
            using (ApplicationDeployer deployer = new ApplicationDeployer())
            {
                string applicationUrl = deployer.Deploy(hostType, DirectoryBrowserDefaultsConfiguration);

                HttpResponseMessage response = null;

                //1. Check directory browsing enabled at application level
                var responseText = HttpClientUtility.GetResponseTextFromUrl(applicationUrl, out response);
                Assert.True(!string.IsNullOrWhiteSpace(responseText), "Received empty response");
                Assert.True((response.Content).Headers.ContentType.ToString() == "text/html; charset=utf-8");
                Assert.True(responseText.Contains("RequirementFiles/"));

                //2. Check directory browsing @RequirementFiles with a ending '/'
                responseText = HttpClientUtility.GetResponseTextFromUrl(applicationUrl + "RequirementFiles/", out response);
                Assert.True(!string.IsNullOrWhiteSpace(responseText), "Received empty response");
                Assert.True((response.Content).Headers.ContentType.ToString() == "text/html; charset=utf-8");
                Assert.True(responseText.Contains("Dir1/") && responseText.Contains("Dir2/") && responseText.Contains("Dir3/"), "Directories Dir1, Dir2, Dir3 not found");

                //2. Check directory browsing @RequirementFiles with request path not ending '/'
                responseText = HttpClientUtility.GetResponseTextFromUrl(applicationUrl + "RequirementFiles", out response);
                Assert.True(!string.IsNullOrWhiteSpace(responseText), "Received empty response");
                Assert.True((response.Content).Headers.ContentType.ToString() == "text/html; charset=utf-8");
                Assert.True(responseText.Contains("Dir1/") && responseText.Contains("Dir2/") && responseText.Contains("Dir3/"), "Directories Dir1, Dir2, Dir3 not found");
            }
        }

        public void DirectoryBrowserDefaultsConfiguration(IAppBuilder app)
        {
            app.UseDirectoryBrowser();
        }

        [Theory, Trait("FunctionalTests", "General")]
        [InlineData(HostType.HttpListener)]
        public void Static_DirectoryMiddlewareMappedToDifferentDirectory(HostType hostType)
        {
            using (ApplicationDeployer deployer = new ApplicationDeployer())
            {
                string applicationUrl = deployer.Deploy(hostType, DirectoryMiddlewareMappedToDifferentDirectoryConfiguration);

                HttpResponseMessage response = null;

                //1. Check directory browsing enabled at application level
                var responseText = HttpClientUtility.GetResponseTextFromUrl(applicationUrl, out response);
                Assert.True(!string.IsNullOrWhiteSpace(responseText), "Received empty response");
                Assert.True((response.Content).Headers.ContentType.ToString() == "text/html; charset=utf-8");
                Assert.True(responseText.Contains("Default.html"));
                Assert.True(responseText.Contains("EmptyFile.txt"));
            }
        }

        public void DirectoryMiddlewareMappedToDifferentDirectoryConfiguration(IAppBuilder app)
        {
            app.UseDirectoryBrowser(new DirectoryBrowserOptions() { FileSystem = new PhysicalFileSystem(@"RequirementFiles\Dir1") });
        }

        [Theory, Trait("FunctionalTests", "General")]
        [InlineData(HostType.HttpListener)]
        public void Static_DirectoryCustomRequestPathToPhysicalPathMapping(HostType hostType)
        {
            using (ApplicationDeployer deployer = new ApplicationDeployer())
            {
                string applicationUrl = deployer.Deploy(hostType, DirectoryCustomRequestPathToPhysicalPathMappingConfiguration);

                HttpResponseMessage response = null;

                //Directory with a default file - case request path ending with a '/'. A local directory referred by relative path
                var responseText = HttpClientUtility.GetResponseTextFromUrl(applicationUrl + "customrequestPath/", out response);
                Assert.Equal<HttpStatusCode>(HttpStatusCode.OK, response.StatusCode);
                Assert.True(!string.IsNullOrWhiteSpace(responseText), "Received empty response");
                Assert.True((response.Content).Headers.ContentType.ToString() == "text/html; charset=utf-8");
                Assert.True(responseText.Contains("Unknown.Unknown"));
                Assert.True(responseText.Contains("Default.html"));

                //Directory with a default file - case request path ending with a '/' + Head request. A local directory referred by relative path
                responseText = HttpClientUtility.HeadResponseTextFromUrl(applicationUrl + "customrequestPath/", out response);
                Assert.Equal<HttpStatusCode>(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal<string>(string.Empty, responseText);

                //Directory with a default file - case request path ending with a '/'. A local directory referred by absolute path
                responseText = HttpClientUtility.GetResponseTextFromUrl(applicationUrl + "customrequestFullPath/", out response);
                Assert.Equal<HttpStatusCode>(HttpStatusCode.OK, response.StatusCode);
                Assert.True(responseText.Contains("TextFile2.txt"));
                Assert.True(responseText.Contains("Unknown.Unknown"));

                //Directory with a default file - case request path ending with a '/' + Head request. A local directory referred by absolute path
                responseText = HttpClientUtility.HeadResponseTextFromUrl(applicationUrl + "customrequestFullPath/", out response);
                Assert.Equal<HttpStatusCode>(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal<string>(string.Empty, responseText);

                //Directory with a default file - case request path ending with a '/'. Mapped to a UNC path.
                responseText = HttpClientUtility.GetResponseTextFromUrl(applicationUrl + "customrequestUNCPath/", out response);
                Assert.Equal<HttpStatusCode>(HttpStatusCode.OK, response.StatusCode);
                Assert.True(responseText.Contains("Dir31"));
                Assert.True(responseText.Contains("Dir32"));
                Assert.True(responseText.Contains("TextFile3.txt"));
                Assert.True(responseText.Contains("TextFile4.txt"));

                //Directory with a default file - case request path ending with a '/' + Head request. Mapped to a UNC path.
                responseText = HttpClientUtility.HeadResponseTextFromUrl(applicationUrl + "customrequestUNCPath/", out response);
                Assert.Equal<HttpStatusCode>(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal<string>(string.Empty, responseText);
            }
        }

        public void DirectoryCustomRequestPathToPhysicalPathMappingConfiguration(IAppBuilder app)
        {
            app.UseDirectoryBrowser(new DirectoryBrowserOptions()
            {
                RequestPath = new PathString("/customrequestPath"),
                FileSystem = new PhysicalFileSystem(@"RequirementFiles\Dir1")
            });

            app.UseDirectoryBrowser(new DirectoryBrowserOptions()
            {
                RequestPath = new PathString("/customrequestFullPath"),
                FileSystem = new PhysicalFileSystem(Path.Combine(Environment.CurrentDirectory, @"RequirementFiles\Dir2"))
            });

            var localAbsolutePath = Path.Combine(Environment.CurrentDirectory, @"RequirementFiles\Dir3");
            var uncPath = Path.Combine("\\\\", Environment.MachineName, localAbsolutePath.Replace(':', '$'));
            app.UseDirectoryBrowser(new DirectoryBrowserOptions()
            {
                RequestPath = new PathString("/customrequestUNCPath"),
                FileSystem = new PhysicalFileSystem(uncPath)
            });
        }
    }
}