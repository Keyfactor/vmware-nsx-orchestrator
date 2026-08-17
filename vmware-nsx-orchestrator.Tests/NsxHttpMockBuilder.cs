
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

// NsxHttpMockBuilder.cs
// Registers mock responses for the NSX ALB (Avi Vantage) endpoints NsxClient calls,
// and builds an NsxClient wired to them via TestableNsxClient.

using System.Net;
using System.Net.Http;
using System.Text.Json;
using Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Models;
using Keyfactor.Logging;
using Microsoft.Extensions.Logging;
using RichardSzalay.MockHttp;

namespace Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Tests
{
    internal class NsxHttpMockBuilder
    {
        // Mirrors NsxClient's own options: the model classes expose public fields, not
        // properties, so IncludeFields is required for System.Text.Json to see them.
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions { IncludeFields = true };

        private readonly MockHttpMessageHandler _handler = new MockHttpMessageHandler();
        private readonly string _baseUrl;

        public NsxHttpMockBuilder(string baseUrl = "https://nsx-alb.example.com/")
        {
            _baseUrl = baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/";
        }

        public MockHttpMessageHandler Handler => _handler;

        public NsxHttpMockBuilder WithLogin(HttpStatusCode status = HttpStatusCode.OK)
        {
            _handler.When(HttpMethod.Post, $"{_baseUrl}login")
                    .Respond(status, "application/json", "{}");
            return this;
        }

        public NsxHttpMockBuilder WithLoginError(HttpStatusCode status, string body = "{\"error\":\"Invalid credentials\"}")
        {
            _handler.When(HttpMethod.Post, $"{_baseUrl}login")
                    .Respond(status, "application/json", body);
            return this;
        }

        public NsxHttpMockBuilder WithLogout(HttpStatusCode status = HttpStatusCode.OK)
        {
            _handler.When(HttpMethod.Post, $"{_baseUrl}logout")
                    .Respond(status, "application/json", "{}");
            return this;
        }

        public NsxHttpMockBuilder WithLogoutError(HttpStatusCode status, string body = "{\"error\":\"session already expired\"}")
        {
            _handler.When(HttpMethod.Post, $"{_baseUrl}logout")
                    .Respond(status, "application/json", body);
            return this;
        }

        public NsxHttpMockBuilder WithCertPage(string certType, int page, int pageSize, GetCertificateResponse response)
        {
            _handler.When(HttpMethod.Get, $"{_baseUrl}api/sslkeyandcertificate")
                    .WithQueryString("type", certType)
                    .WithQueryString("page", page.ToString())
                    .WithQueryString("page_size", pageSize.ToString())
                    .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(response, SerializerOptions));
            return this;
        }

        public NsxHttpMockBuilder WithCertPageError(string certType, int page, int pageSize, HttpStatusCode status)
        {
            _handler.When(HttpMethod.Get, $"{_baseUrl}api/sslkeyandcertificate")
                    .WithQueryString("type", certType)
                    .WithQueryString("page", page.ToString())
                    .WithQueryString("page_size", pageSize.ToString())
                    .Respond(status, "application/json", "{\"error\":\"server error\"}");
            return this;
        }

        public NsxHttpMockBuilder WithGetCertByName(string name, GetCertificateResponse response)
        {
            _handler.When(HttpMethod.Get, $"{_baseUrl}api/sslkeyandcertificate")
                    .WithQueryString("name", name)
                    .Respond(HttpStatusCode.OK, "application/json", JsonSerializer.Serialize(response, SerializerOptions));
            return this;
        }

        public NsxHttpMockBuilder WithGetCertByNameError(string name, HttpStatusCode status)
        {
            _handler.When(HttpMethod.Get, $"{_baseUrl}api/sslkeyandcertificate")
                    .WithQueryString("name", name)
                    .Respond(status, "application/json", "{\"error\":\"server error\"}");
            return this;
        }

        public NsxHttpMockBuilder WithAddCertificate(HttpStatusCode status, SSLKeyAndCertificate returned = null)
        {
            _handler.When(HttpMethod.Post, $"{_baseUrl}api/sslkeyandcertificate")
                    .Respond(status, "application/json", JsonSerializer.Serialize(returned ?? new SSLKeyAndCertificate(), SerializerOptions));
            return this;
        }

        public NsxHttpMockBuilder WithUpdateCertificate(string uuid, HttpStatusCode status, SSLKeyAndCertificate returned = null)
        {
            _handler.When(HttpMethod.Put, $"{_baseUrl}api/sslkeyandcertificate/{uuid}")
                    .Respond(status, "application/json", JsonSerializer.Serialize(returned ?? new SSLKeyAndCertificate(), SerializerOptions));
            return this;
        }

        public NsxHttpMockBuilder WithDeleteCertificate(string uuid, HttpStatusCode status)
        {
            _handler.When(HttpMethod.Delete, $"{_baseUrl}api/sslkeyandcertificate/{uuid}")
                    .Respond(status, "application/json", "{}");
            return this;
        }

        public NsxClient BuildClient(ILogger logger = null, string username = "svc-keyfactor", string password = "password", string tenant = null)
        {
            return TestableNsxClient.Create(_handler, logger ?? LogHandler.GetClassLogger<NsxClient>(), _baseUrl, username, password, tenant);
        }
    }
}
