﻿using KissLog.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace KissLog.AspNetCore
{
    internal static class HttpRequestFactory
    {
        public static KissLog.Web.HttpRequest Create(HttpRequest request)
        {
            KissLog.Web.HttpRequest result = new Web.HttpRequest();

            if (request == null)
                return result;

            try
            {
                if (request.HttpContext.Session != null && request.HttpContext.Session.IsAvailable)
                {
                    bool isNewSession = false;

                    string lastSessionId = request.HttpContext.Session.GetString("X-KissLogSessionId");
                    if (string.IsNullOrEmpty(lastSessionId) || string.Compare(lastSessionId, request.HttpContext.Session.Id, StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        isNewSession = true;
                        request.HttpContext.Session.SetString("X-KissLogSessionId", request.HttpContext.Session.Id);
                    }

                    result.IsNewSession = isNewSession;
                    result.SessionId = request.HttpContext.Session.Id;
                }
            }
            catch
            {
                // ignored
            }

            result.StartDateTime = DateTime.UtcNow;
            result.UserAgent = request.Headers[HeaderNames.UserAgent].ToString();

            string url = request.GetDisplayUrl();
            result.Url = new Uri(url);

            result.MachineName = GetMachineName();

            KissLog.Web.RequestProperties properties = new KissLog.Web.RequestProperties();
            result.Properties = properties;

            AddUserClaims(request, result);

            result.RemoteAddress = request.HttpContext.Connection?.RemoteIpAddress?.ToString();
            result.HttpMethod = request.Method;

            string httpReferer = null;
            string requestContentType = null;
            string inputStream = null;

            foreach (string key in request.Headers.Keys)
            {
                if (string.Compare(key, "Cookie", StringComparison.OrdinalIgnoreCase) == 0)
                    continue;

                StringValues values;
                request.Headers.TryGetValue(key, out values);

                string value = values.ToString();

                properties.Headers.Add(new KeyValuePair<string, string>(key, value));

                if (string.Compare(key, "Referer", StringComparison.OrdinalIgnoreCase) == 0)
                    httpReferer = value;

                if (string.Compare(key, "Content-Type", StringComparison.OrdinalIgnoreCase) == 0)
                    requestContentType = value;
            }

            foreach (string key in request.Cookies.Keys)
            {
                string value = request.Cookies[key];

                properties.Cookies.Add(new KeyValuePair<string, string>(key, value));
            }

            foreach (string key in request.Query.Keys)
            {
                string value = string.Join("; ", request.Query[key]);

                properties.QueryString.Add(
                    new KeyValuePair<string, string>(key, value)
                );
            }

            if (request.HasFormContentType && KissLogConfiguration.Options.ApplyShouldLogRequestFormData(result))
            {
                foreach (string key in request.Form.Keys)
                {
                    string value = string.Join("; ", request.Form[key]);
                    properties.FormData.Add(new KeyValuePair<string, string>(key, value));
                }
            }

            if (InternalHelpers.ShouldLogInputStream(properties.Headers) && KissLogConfiguration.Options.ApplyShouldLogRequestInputStream(result))
            {
                inputStream = ReadInputStream(request);
            }

            result.HttpReferer = httpReferer;
            result.Properties.InputStream = inputStream;

            return result;
        }

        private static void AddUserClaims(HttpRequest request, KissLog.Web.HttpRequest requestProperties)
        {
            if (request.HttpContext.User?.Identity == null || request.HttpContext.User.Identity.IsAuthenticated == false)
                return;

            if ((request.HttpContext.User != null) == false)
                return;

            ClaimsPrincipal claimsPrincipal = (ClaimsPrincipal)request.HttpContext.User;
            ClaimsIdentity identity = (ClaimsIdentity)claimsPrincipal?.Identity;

            if (identity == null)
                return;

            List<KeyValuePair<string, string>> claims = ToDictionary(identity);
            requestProperties.Properties.Claims = claims;

            requestProperties.IsAuthenticated = true;

            KissLog.Web.UserDetails user = KissLogConfiguration.Options.ApplyGetUser(requestProperties.Properties);
            requestProperties.User = user;
        }

        private static string GetMachineName()
        {
            string name = null;

            try
            {
                name =
                    Environment.GetEnvironmentVariable("CUMPUTERNAME") ??
                    Environment.GetEnvironmentVariable("HOSTNAME") ??
                    System.Net.Dns.GetHostName();
            }
            catch
            {
                // ignored
            }

            return name;
        }

        private static string ReadInputStream(HttpRequest request)
        {
            try
            {
                return PackageInit.ReadInputStreamProvider.ReadInputStream(request);
            }
            catch (Exception ex)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Error on WebRequestPropertiesFactory.ReadInputStream()");
                sb.AppendLine(ex.ToString());

                KissLog.Internal.InternalHelpers.Log(sb.ToString(), LogLevel.Error);
            }

            return string.Empty;
        }

        public static List<KeyValuePair<string, string>> ToDictionary(ClaimsIdentity identity)
        {
            List<KeyValuePair<string, string>> claims =
                identity.Claims
                    .Where(p => string.IsNullOrEmpty(p.Type) == false)
                    .Select(p => new KeyValuePair<string, string>(p.Type, p.Value))
                    .ToList();

            return claims;
        }
    }
}
