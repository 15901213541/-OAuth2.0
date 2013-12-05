﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Cors;

namespace Microsoft.Owin.Cors
{
    /// <summary>
    /// Processes requests according to the provided cross domain policy.
    /// </summary>
    public class CorsMiddleware : OwinMiddleware
    {
        private readonly ICorsPolicyProvider _corsPolicyProvider;
        private readonly ICorsEngine _corsEngine;

        /// <summary>
        /// Creates a new instance of CorsMiddleware.
        /// </summary>
        /// <param name="next"></param>
        /// <param name="options"></param>
        public CorsMiddleware(OwinMiddleware next, CorsOptions options)
            : base(next)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            _corsPolicyProvider = options.PolicyProvider ?? new CorsPolicyProvider();
            _corsEngine = options.CorsEngine ?? new CorsEngine();
        }

        /// <summary>
        /// Evaluates and applies the CORS policy. Responses will be generated for preflight requests.
        /// Requests that are permitted by the CORS policy will be passed onto the next middleware.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override async Task Invoke(IOwinContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            CorsRequestContext corsRequestContext = GetCorsRequestContext(context);
            CorsPolicy policy = await _corsPolicyProvider.GetCorsPolicyAsync(context.Request);

            if (policy != null && corsRequestContext != null)
            {
                if (corsRequestContext.IsPreflight)
                {
                    await HandleCorsPreflightRequestAsync(context, policy, corsRequestContext);
                }
                else
                {
                    await HandleCorsRequestAsync(context, policy, corsRequestContext);
                }
            }
            else
            {
                await Next.Invoke(context);
            }
        }

        private Task HandleCorsRequestAsync(IOwinContext context, CorsPolicy policy, CorsRequestContext corsRequestContext)
        {
            CorsResult result;
            if (TryEvaluateCorsPolicy(policy, corsRequestContext, out result))
            {
                WriteCorsHeaders(context, result);
            }

            return Next.Invoke(context);
        }

        private Task HandleCorsPreflightRequestAsync(IOwinContext context, CorsPolicy policy, CorsRequestContext corsRequestContext)
        {
            CorsResult result;
            if (!String.IsNullOrEmpty(corsRequestContext.AccessControlRequestMethod) &&
                TryEvaluateCorsPolicy(policy, corsRequestContext, out result))
            {
                context.Response.StatusCode = 200;
                WriteCorsHeaders(context, result);
            }
            else
            {
                // We couldn't evaluate the cors policy so it's a bad request
                context.Response.StatusCode = 400;
            }

            return Task.FromResult(0);
        }

        private bool TryEvaluateCorsPolicy(CorsPolicy policy, CorsRequestContext corsRequestContext, out CorsResult result)
        {
            result = _corsEngine.EvaluatePolicy(corsRequestContext, policy);
            return result != null && result.IsValid;
        }

        private static void WriteCorsHeaders(IOwinContext context, CorsResult result)
        {
            IDictionary<string, string> corsHeaders = result.ToResponseHeaders();
            if (corsHeaders != null)
            {
                foreach (var header in corsHeaders)
                {
                    context.Response.Headers.Set(header.Key, header.Value);
                }
            }
        }

        private static CorsRequestContext GetCorsRequestContext(IOwinContext context)
        {
            string origin = context.Request.Headers.Get(CorsConstants.Origin);

            if (String.IsNullOrEmpty(origin))
            {
                return null;
            }

            var requestContext = new CorsRequestContext
            {
                RequestUri = context.Request.Uri,
                HttpMethod = context.Request.Method,
                Host = context.Request.Host.Value,
                Origin = origin,
                AccessControlRequestMethod = context.Request.Headers.Get(CorsConstants.AccessControlRequestMethod)
            };

            IList<string> headerValues = context.Request.Headers.GetCommaSeparatedValues(CorsConstants.AccessControlRequestHeaders);

            if (headerValues != null)
            {
                foreach (var header in headerValues)
                {
                    requestContext.AccessControlRequestHeaders.Add(header);
                }
            }

            return requestContext;
        }
    }
}
