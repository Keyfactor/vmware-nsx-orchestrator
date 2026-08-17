
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

// TestableNsxClient.cs
// A factory that produces an NsxClient whose HTTP traffic is routed through a
// MockHttpMessageHandler (RichardSzalay.MockHttp), so tests never touch the network.
//
// NsxClient's constructor hard-constructs its own HttpClientHandler/HttpClient and
// immediately performs a real login POST — there's no injection seam. To intercept that
// without changing production code, this factory:
//   1. Allocates an NsxClient without running its constructor (RuntimeHelpers.GetUninitializedObject).
//   2. Injects a mock-backed HttpClient via the private HttpClient/HttpHandler backing fields.
//   3. Invokes the real private Login(username, password) method via reflection, so the
//      actual production auth/error-handling code path (and its exact exception text) runs.
//   4. Seeds the CSRF cookie the real constructor would have captured automatically from the
//      login response's Set-Cookie header — our CookieContainer never sees that header
//      because the mock handler bypasses HttpClientHandler's cookie processing.

using System;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Tests
{
    internal static class TestableNsxClient
    {
        public static NsxClient Create(
            HttpMessageHandler mockHandler,
            ILogger logger,
            string baseUrl,
            string username = "svc-keyfactor",
            string password = "password",
            string tenant = null,
            string apiVersion = null,
            string csrfToken = "fake-csrf-token")
        {
            if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
            {
                baseUrl += "/";
            }

            var cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler { CookieContainer = cookieContainer };

            var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri(baseUrl) };
            httpClient.DefaultRequestHeaders.Add("X-Avi-Version", apiVersion ?? "20.1.1");
            if (tenant != null)
            {
                httpClient.DefaultRequestHeaders.Add("X-Avi-Tenant", tenant);
            }

            var client = (NsxClient)RuntimeHelpers.GetUninitializedObject(typeof(NsxClient));

            // GetUninitializedObject skips every constructor, so NsxClient's own field
            // initializer for serializerOptions never runs. Without this, it stays null,
            // and JsonSerializer treats a null options argument as "use the defaults" —
            // silently deserializing every field-based model back to a blank instance.
            ReflectionHelpers.SetField(client, typeof(NsxClient), "serializerOptions", new JsonSerializerOptions
            {
                IncludeFields = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            ReflectionHelpers.SetField(client, typeof(NsxClient), "_logger", logger);
            ReflectionHelpers.SetBackingField(client, typeof(NsxClient), "HttpHandler", handler);
            ReflectionHelpers.SetBackingField(client, typeof(NsxClient), "HttpClient", httpClient);
            ReflectionHelpers.SetBackingField(client, typeof(NsxClient), "BaseUrl", baseUrl);

            // Throws the real production exception (from EnsureSuccessfulResponse) if the
            // mocked login endpoint responds with a non-success status.
            ReflectionHelpers.Invoke(client, typeof(NsxClient), "Login", username, password);

            var loginUri = new Uri(baseUrl + "login");
            cookieContainer.Add(loginUri, new Cookie("csrftoken", csrfToken));
            var loginCookies = cookieContainer.GetCookies(loginUri);
            ReflectionHelpers.SetBackingField(client, typeof(NsxClient), "LoginCookies", loginCookies);

            httpClient.DefaultRequestHeaders.Add("X-CSRFToken", loginCookies["csrftoken"].Value);
            httpClient.DefaultRequestHeaders.Add("Referer", httpClient.BaseAddress.OriginalString);

            return client;
        }
    }
}
