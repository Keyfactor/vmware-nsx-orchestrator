// Copyright 2023 Keyfactor
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

using Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;

namespace Keyfactor.Extensions.Orchestrator.Vmware.Nsx
{
    public class NsxClient : IDisposable
    {
        private HttpClientHandler HttpHandler { get; }
        private HttpClient HttpClient { get; }
        private CookieCollection LoginCookies { get; }
        private string BaseUrl { get; }

        private const string LOGIN_ENDPOINT = "login";
        private const string LOGOUT_ENDPOINT = "logout";
        private const string CERT_ENDPOINT = "api/sslkeyandcertificate";
        private readonly JsonSerializerSettings serializerSettings = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public NsxClient(string url, string username, string password, string tenant, string apiVersion)
        {
            // declare cookies and handler to be able to access them after Login process
            CookieContainer cookies = new CookieContainer();
            HttpHandler = new HttpClientHandler();
            HttpHandler.CookieContainer = cookies;

            HttpClient = new HttpClient(HttpHandler);

            string aviVersion = apiVersion ?? "20.1.1";
            HttpClient.DefaultRequestHeaders.Add("X-Avi-Version", aviVersion);
            if (tenant != null)
            {
                HttpClient.DefaultRequestHeaders.Add("X-Avi-Tenant", tenant);
            }
            
            // ensure base url ends as expected
            if (!url.EndsWith("/"))
            {
                url += "/";
            }
            if (url.EndsWith("api/", StringComparison.OrdinalIgnoreCase))
            {
                url = url.Substring(0, url.Length - 4); // remove "api/" from end of base url
            }
            BaseUrl = url;
            HttpClient.BaseAddress = new Uri(BaseUrl);

#if DEBUG
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback
                (delegate { return true; });
#endif

            Login(username, password);

            // check cookies after login
            Uri loginUri = new Uri(BaseUrl + LOGIN_ENDPOINT);
            LoginCookies = cookies.GetCookies(loginUri);
        }

        private void Login(string username, string password)
        {
            dynamic loginBody = new {
                username = username,
                password = password
            };
            StringContent content = new StringContent(JsonConvert.SerializeObject(loginBody), Encoding.UTF8, "application/json");
            var resp = HttpClient.PostAsync(LOGIN_ENDPOINT, content).Result;
            EnsureSuccessfulResponse(resp);
        }

        private void SetAuthCookiesForRequest(string requestRelativeUrl)
        {
            var requestCookies = HttpHandler.CookieContainer;
            requestCookies.Add(new Uri(BaseUrl + requestRelativeUrl), LoginCookies);
        }

        private void Logout()
        {
            HttpClient.DefaultRequestHeaders.Add("X-CSRFToken", LoginCookies["csrftoken"].Value);
            HttpClient.DefaultRequestHeaders.Add("Referer", HttpClient.BaseAddress.OriginalString);
            SetAuthCookiesForRequest(LOGOUT_ENDPOINT);
            var resp = HttpClient.PostAsync(LOGOUT_ENDPOINT, null).Result;
            EnsureSuccessfulResponse(resp);
        }

        public async Task<List<SSLKeyAndCertificate>> GetAllCertificates(string certType, int pageSize)
        {
            List<SSLKeyAndCertificate> allCerts = new List<SSLKeyAndCertificate>();
            GetCertificateResponse response;
            int page = 1;
            do
            {
                response = await GetCertificatesPage(certType, pageSize, page);
                allCerts.AddRange(response.results);
                page++;
            }
            while (!string.IsNullOrEmpty(response.next));

            return allCerts;
        }

        private async Task<GetCertificateResponse> GetCertificatesPage(string certType, int pageSize, int page)
        {
            string requestEndpoint = CERT_ENDPOINT + $"?type={certType}&page={page}&page_size={pageSize}";
            SetAuthCookiesForRequest(requestEndpoint);
            return await GetResponseAsync<GetCertificateResponse>(await HttpClient.GetAsync(requestEndpoint));
        }

        public async Task<SSLKeyAndCertificate> GetCertificateByName(string name)
        {
            string requestEndpoint = CERT_ENDPOINT + $"?name={name}";
            SetAuthCookiesForRequest(requestEndpoint);
            GetCertificateResponse response = await GetResponseAsync<GetCertificateResponse>(await HttpClient.GetAsync(requestEndpoint));
            return response.results.Single();
        }

        public async Task<SSLKeyAndCertificate> AddCertificate(SSLKeyAndCertificate certToImport)
        {
            StringContent content = new StringContent(JsonConvert.SerializeObject(certToImport, serializerSettings), Encoding.UTF8, "application/json");
            SetAuthCookiesForRequest(CERT_ENDPOINT);
            return await GetResponseAsync<SSLKeyAndCertificate>(await HttpClient.PostAsync(CERT_ENDPOINT, content));
        }

        public async Task<SSLKeyAndCertificate> UpdateCertificate(string uuid, SSLKeyAndCertificate certUpdate)
        {
            StringContent content = new StringContent(JsonConvert.SerializeObject(certUpdate, serializerSettings), Encoding.UTF8, "application/json");
            string requestEndpoint = string.Join("/", CERT_ENDPOINT, uuid);
            SetAuthCookiesForRequest(requestEndpoint);
            return await GetResponseAsync<SSLKeyAndCertificate>(await HttpClient.PutAsync(requestEndpoint, content));
        }

        public async Task<bool> DeleteCertificate(string uuid)
        {
            string requestEndpoint = string.Join("/", CERT_ENDPOINT, uuid);
            SetAuthCookiesForRequest(requestEndpoint);
            HttpResponseMessage response = await HttpClient.DeleteAsync(requestEndpoint);
            EnsureSuccessfulResponse(response);
            return true;
        }

        private async Task<T> GetResponseAsync<T>(HttpResponseMessage response)
        {
            EnsureSuccessfulResponse(response);
            string stringResponse = new StreamReader(await response.Content.ReadAsStreamAsync()).ReadToEnd();
            return JsonConvert.DeserializeObject<T>(stringResponse);
        }

        private void EnsureSuccessfulResponse(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                string error = new StreamReader(response.Content.ReadAsStreamAsync().Result).ReadToEnd();
                throw new Exception($"Request to VMware NSX ALB was not successful - {response.StatusCode} - {error}");
            }
        }

        public void Dispose()
        {
            try
            {
                Logout();
            }
            catch (Exception ex)
            {
                throw new Exception("Logout Failed", ex);
            }
            finally
            {
                HttpClient.Dispose();
                HttpHandler.Dispose();
            }
        }
    }
}
