
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

// NsxClientTests.cs
// Unit tests for NsxClient against a mocked NSX ALB (Avi Vantage) API — no live controller
// needed. Covers login, the certificate CRUD surface, and session teardown (Dispose/Logout).

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Models;
using Xunit;

namespace Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Tests
{
    public class NsxClientTests
    {
        // ------------------------------------------------------------------
        // Login
        // ------------------------------------------------------------------

        [Fact]
        public void Constructor_LoginSucceeds_DoesNotThrow()
        {
            var mock = new NsxHttpMockBuilder().WithLogin();

            var client = mock.BuildClient();

            Assert.NotNull(client);
        }

        [Fact]
        public void Constructor_LoginRejected_ThrowsWithNsxErrorDetails()
        {
            // Reproduces the exact failure reported against a live NSX ALB Controller:
            // a 401 with an "Invalid credentials" body, even though the credentials are correct
            // (root cause: leaked, un-logged-out sessions tripping Avi's login/session limits).
            var mock = new NsxHttpMockBuilder()
                .WithLoginError(HttpStatusCode.Unauthorized, "{\"error\":\"Invalid credentials\"}");

            var ex = Assert.Throws<Exception>(() => mock.BuildClient());

            Assert.Contains("Unauthorized", ex.Message);
            Assert.Contains("Invalid credentials", ex.Message);
        }

        // ------------------------------------------------------------------
        // GetAllCertificates (pagination)
        // ------------------------------------------------------------------

        [Fact]
        public async System.Threading.Tasks.Task GetAllCertificates_SinglePage_ReturnsAllResults()
        {
            var mock = new NsxHttpMockBuilder()
                .WithLogin()
                .WithCertPage("SSL_CERTIFICATE_TYPE_VIRTUALSERVICE", 1, 25, new GetCertificateResponse
                {
                    count = 2,
                    next = null,
                    results = new List<SSLKeyAndCertificate>
                    {
                        new SSLKeyAndCertificate { name = "cert-a", uuid = "uuid-a" },
                        new SSLKeyAndCertificate { name = "cert-b", uuid = "uuid-b" }
                    }
                });

            var client = mock.BuildClient();

            var result = await client.GetAllCertificates("SSL_CERTIFICATE_TYPE_VIRTUALSERVICE", 25);

            Assert.Equal(2, result.Count);
            Assert.Contains(result, c => c.name == "cert-a");
            Assert.Contains(result, c => c.name == "cert-b");
        }

        [Fact]
        public async System.Threading.Tasks.Task GetAllCertificates_MultiPage_AggregatesAcrossPages()
        {
            var mock = new NsxHttpMockBuilder()
                .WithLogin()
                .WithCertPage("SSL_CERTIFICATE_TYPE_VIRTUALSERVICE", 1, 2, new GetCertificateResponse
                {
                    count = 3,
                    next = "https://nsx-alb.example.com/api/sslkeyandcertificate?page=2",
                    results = new List<SSLKeyAndCertificate>
                    {
                        new SSLKeyAndCertificate { name = "cert-a" },
                        new SSLKeyAndCertificate { name = "cert-b" }
                    }
                })
                .WithCertPage("SSL_CERTIFICATE_TYPE_VIRTUALSERVICE", 2, 2, new GetCertificateResponse
                {
                    count = 3,
                    next = null,
                    results = new List<SSLKeyAndCertificate>
                    {
                        new SSLKeyAndCertificate { name = "cert-c" }
                    }
                });

            var client = mock.BuildClient();

            var result = await client.GetAllCertificates("SSL_CERTIFICATE_TYPE_VIRTUALSERVICE", 2);

            Assert.Equal(3, result.Count);
            Assert.Contains(result, c => c.name == "cert-c");
        }

        [Fact]
        public async System.Threading.Tasks.Task GetAllCertificates_ServerError_Throws()
        {
            var mock = new NsxHttpMockBuilder()
                .WithLogin()
                .WithCertPageError("SSL_CERTIFICATE_TYPE_VIRTUALSERVICE", 1, 25, HttpStatusCode.InternalServerError);

            var client = mock.BuildClient();

            await Assert.ThrowsAsync<Exception>(() => client.GetAllCertificates("SSL_CERTIFICATE_TYPE_VIRTUALSERVICE", 25));
        }

        // ------------------------------------------------------------------
        // GetCertificateByName
        // ------------------------------------------------------------------

        [Fact]
        public async System.Threading.Tasks.Task GetCertificateByName_Found_ReturnsCert()
        {
            var mock = new NsxHttpMockBuilder()
                .WithLogin()
                .WithGetCertByName("my-cert", new GetCertificateResponse
                {
                    count = 1,
                    results = new List<SSLKeyAndCertificate> { new SSLKeyAndCertificate { name = "my-cert", uuid = "uuid-123" } }
                });

            var client = mock.BuildClient();

            var result = await client.GetCertificateByName("my-cert");

            Assert.Equal("uuid-123", result.uuid);
        }

        [Fact]
        public async System.Threading.Tasks.Task GetCertificateByName_NoMatch_Throws()
        {
            var mock = new NsxHttpMockBuilder()
                .WithLogin()
                .WithGetCertByName("missing-cert", new GetCertificateResponse
                {
                    count = 0,
                    results = new List<SSLKeyAndCertificate>()
                });

            var client = mock.BuildClient();

            // production code calls response.results.Single(), which throws when no match is found
            await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetCertificateByName("missing-cert"));
        }

        [Fact]
        public async System.Threading.Tasks.Task GetCertificateByName_ServerError_Throws()
        {
            var mock = new NsxHttpMockBuilder()
                .WithLogin()
                .WithGetCertByNameError("my-cert", HttpStatusCode.InternalServerError);

            var client = mock.BuildClient();

            await Assert.ThrowsAsync<Exception>(() => client.GetCertificateByName("my-cert"));
        }

        // ------------------------------------------------------------------
        // AddCertificate
        // ------------------------------------------------------------------

        [Fact]
        public async System.Threading.Tasks.Task AddCertificate_Success_ReturnsCert()
        {
            var mock = new NsxHttpMockBuilder()
                .WithLogin()
                .WithAddCertificate(HttpStatusCode.Created, new SSLKeyAndCertificate { name = "new-cert", uuid = "uuid-new" });

            var client = mock.BuildClient();

            var result = await client.AddCertificate(new SSLKeyAndCertificate { name = "new-cert" });

            Assert.Equal("uuid-new", result.uuid);
        }

        [Fact]
        public async System.Threading.Tasks.Task AddCertificate_ServerError_Throws()
        {
            var mock = new NsxHttpMockBuilder()
                .WithLogin()
                .WithAddCertificate(HttpStatusCode.BadRequest);

            var client = mock.BuildClient();

            await Assert.ThrowsAsync<Exception>(() => client.AddCertificate(new SSLKeyAndCertificate { name = "new-cert" }));
        }

        // ------------------------------------------------------------------
        // UpdateCertificate
        // ------------------------------------------------------------------

        [Fact]
        public async System.Threading.Tasks.Task UpdateCertificate_Success_ReturnsCert()
        {
            var mock = new NsxHttpMockBuilder()
                .WithLogin()
                .WithUpdateCertificate("uuid-123", HttpStatusCode.OK, new SSLKeyAndCertificate { name = "updated-cert", uuid = "uuid-123" });

            var client = mock.BuildClient();

            var result = await client.UpdateCertificate("uuid-123", new SSLKeyAndCertificate { name = "updated-cert" });

            Assert.Equal("updated-cert", result.name);
        }

        [Fact]
        public async System.Threading.Tasks.Task UpdateCertificate_ServerError_Throws()
        {
            var mock = new NsxHttpMockBuilder()
                .WithLogin()
                .WithUpdateCertificate("uuid-123", HttpStatusCode.InternalServerError);

            var client = mock.BuildClient();

            await Assert.ThrowsAsync<Exception>(() => client.UpdateCertificate("uuid-123", new SSLKeyAndCertificate { name = "x" }));
        }

        // ------------------------------------------------------------------
        // DeleteCertificate
        // ------------------------------------------------------------------

        [Fact]
        public async System.Threading.Tasks.Task DeleteCertificate_Success_ReturnsTrue()
        {
            var mock = new NsxHttpMockBuilder()
                .WithLogin()
                .WithDeleteCertificate("uuid-123", HttpStatusCode.OK);

            var client = mock.BuildClient();

            var result = await client.DeleteCertificate("uuid-123");

            Assert.True(result);
        }

        [Fact]
        public async System.Threading.Tasks.Task DeleteCertificate_ServerError_Throws()
        {
            var mock = new NsxHttpMockBuilder()
                .WithLogin()
                .WithDeleteCertificate("uuid-123", HttpStatusCode.NotFound);

            var client = mock.BuildClient();

            await Assert.ThrowsAsync<Exception>(() => client.DeleteCertificate("uuid-123"));
        }

        // ------------------------------------------------------------------
        // Dispose / Logout — the session lifecycle this project's login-failure
        // bug fix depends on (see NsxJobDisposalTests for the job-level contract).
        // ------------------------------------------------------------------

        [Fact]
        public void Dispose_LogoutSucceeds_DoesNotThrow()
        {
            var mock = new NsxHttpMockBuilder()
                .WithLogin()
                .WithLogout();

            var client = mock.BuildClient();

            client.Dispose();
        }

        [Fact]
        public void Dispose_LogoutRejected_ThrowsLogoutFailedException()
        {
            var mock = new NsxHttpMockBuilder()
                .WithLogin()
                .WithLogoutError(HttpStatusCode.InternalServerError);

            var client = mock.BuildClient();

            var ex = Assert.Throws<Exception>(() => client.Dispose());

            Assert.Equal("Logout Failed", ex.Message);
        }
    }
}
