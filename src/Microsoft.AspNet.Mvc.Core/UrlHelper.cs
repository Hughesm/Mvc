// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Mvc.Internal;
using Microsoft.AspNet.Routing;
using Microsoft.Framework.DependencyInjection;

namespace Microsoft.AspNet.Mvc
{
    /// <summary>
    /// An implementation of <see cref="IUrlHelper"/> that contains methods to
    /// build URLs for ASP.NET MVC within an application.
    /// </summary>
    public class UrlHelper : IUrlHelper
    {
        private readonly HttpContext _httpContext;
        private readonly IRouter _router;
        private readonly IDictionary<string, object> _ambientValues;
        private readonly IActionSelector _actionSelector;

        /// <summary>
        /// Initializes a new instance of the <see cref="UrlHelper"/> class using the specified action context and
        /// action selector.
        /// </summary>
        /// <param name="contextAccessor">The <see cref="IScopedInstance{TContext}"/> to access the action context
        /// of the current request.</param>
        /// <param name="actionSelector">The <see cref="IActionSelector"/> to be used for verifying the correctness of
        /// supplied parameters for a route.
        /// </param>
        public UrlHelper(IScopedInstance<ActionContext> contextAccessor, IActionSelector actionSelector)
        {
            _httpContext = contextAccessor.Value.HttpContext;
            _router = contextAccessor.Value.RouteData.Routers[0];
            _ambientValues = contextAccessor.Value.RouteData.Values;
            _actionSelector = actionSelector;
        }

        /// <inheritdoc />
        public virtual string Action(UrlActionContext actionContext)
        {
            var valuesDictionary = TypeHelper.ObjectToDictionary(actionContext.Values);

            if (actionContext.Action != null)
            {
                valuesDictionary["action"] = actionContext.Action;
            }

            if (actionContext.Controller != null)
            {
                valuesDictionary["controller"] = actionContext.Controller;
            }

            var path = GeneratePathFromRoute(valuesDictionary);
            if (path == null)
            {
                return null;
            }

            return GenerateUrl(actionContext.Protocol, actionContext.Host, path, actionContext.Fragment);
        }

        /// <inheritdoc />
        public bool IsLocalUrl(string url)
        {
            return UrlUtility.IsLocalUrl(url);
        }

        /// <inheritdoc />
        public virtual string RouteUrl(UrlRouteContext routeContext)
        {
            var valuesDictionary = TypeHelper.ObjectToDictionary(routeContext.Values);

            var path = GeneratePathFromRoute(routeContext.RouteName, valuesDictionary);
            if (path == null)
            {
                return null;
            }

            return GenerateUrl(routeContext.Protocol, routeContext.Host, path, routeContext.Fragment);
        }

        private string GeneratePathFromRoute(IDictionary<string, object> values)
        {
            return GeneratePathFromRoute(routeName: null, values: values);
        }

        /// <summary>
        /// Generates the absolute path of the url for the specified route values by
        /// using the specified route name.
        /// </summary>
        /// <param name="routeName">The name of the route that is used to generate the URL.</param>
        /// <param name="values">A dictionary that contains the parameters for a route.</param>
        /// <returns>The absolute path of the URL.</returns>
        protected virtual string GeneratePathFromRoute(string routeName, IDictionary<string, object> values)
        {
            var context = new VirtualPathContext(_httpContext, _ambientValues, values, routeName);
            var path = _router.GetVirtualPath(context);
            if (path == null)
            {
                return null;
            }

            // See Routing Issue#31
            if (path.Length > 0 && path[0] != '/')
            {
                path = "/" + path;
            }

            var fullPath = _httpContext.Request.PathBase.Add(new PathString(path)).Value;
            if (fullPath.Length == 0)
            {
                return "/";
            }
            else
            {
                return fullPath;
            }
        }

        /// <inheritdoc />
        public virtual string Content([NotNull] string contentPath)
        {
            return GenerateClientUrl(_httpContext.Request.PathBase, contentPath);
        }

        private static string GenerateClientUrl([NotNull] PathString applicationPath,
                                                [NotNull] string path)
        {
            if (path.StartsWith("~/", StringComparison.Ordinal))
            {
                var segment = new PathString(path.Substring(1));
                return applicationPath.Add(segment).Value;
            }
            return path;
        }

        private string GenerateUrl(string protocol, string host, string path, string fragment)
        {
            // We should have a robust and centrallized version of this code. See HttpAbstractions#28
            Debug.Assert(path != null);

            var url = path;
            if (!string.IsNullOrEmpty(fragment))
            {
                url += "#" + fragment;
            }

            if (string.IsNullOrEmpty(protocol) && string.IsNullOrEmpty(host))
            {
                // We're returning a partial url (just path + query + fragment), but we still want it
                // to be rooted.
                if (!url.StartsWith("/", StringComparison.Ordinal))
                {
                    url = "/" + url;
                }

                return url;
            }
            else
            {
                protocol = string.IsNullOrEmpty(protocol) ? "http" : protocol;
                host = string.IsNullOrEmpty(host) ? _httpContext.Request.Host.Value : host;

                url = protocol + "://" + host + url;
                return url;
            }
        }
    }
}