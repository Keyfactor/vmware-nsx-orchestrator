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
    public class NsxClient
    {
        private HttpClient _httpClient { get; }

        private const string ENDPOINT = "sslkeyandcertificate";
        private readonly JsonSerializerSettings serializerSettings = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public NsxClient(string url, string username, string password)
        {
            _httpClient = new HttpClient();
            string authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));

            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {authValue}");
            _httpClient.DefaultRequestHeaders.Add("X-Avi-Version", "18.2.9");
            
            // ensure base url ends as expected
            if (!url.EndsWith("/"))
            {
                url += "/";
            }
            if (!url.EndsWith("api/", StringComparison.OrdinalIgnoreCase))
            {
                url += "api/";
            }
            _httpClient.BaseAddress = new Uri(url);

#if DEBUG
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback
                (delegate { return true; });
#endif
        }

        public async Task<List<SSLKeyAndCertificate>> GetAllCertificates(string certType)
        {
            ApiResponse response = await GetResponseAsync<ApiResponse>(await _httpClient.GetAsync(ENDPOINT + $"?type={certType}"));
            return response.results;
        }

        public async Task<SSLKeyAndCertificate> GetCertificateByName(string name)
        {
            ApiResponse response = await GetResponseAsync<ApiResponse>(await _httpClient.GetAsync(ENDPOINT + $"?name={name}"));
            return response.results.Single();
        }

        public async Task<SSLKeyAndCertificate> AddCertificate(SSLKeyAndCertificate certToImport)
        {
            StringContent content = new StringContent(JsonConvert.SerializeObject(certToImport, serializerSettings), Encoding.UTF8, "application/json");
            return await GetResponseAsync<SSLKeyAndCertificate>(await _httpClient.PostAsync(ENDPOINT, content));
        }

        public async Task<SSLKeyAndCertificate> UpdateCertificate(string uuid, SSLKeyAndCertificate certUpdate)
        {
            StringContent content = new StringContent(JsonConvert.SerializeObject(certUpdate, serializerSettings), Encoding.UTF8, "application/json");
            return await GetResponseAsync<SSLKeyAndCertificate>(await _httpClient.PutAsync(string.Join("/", ENDPOINT, uuid), content));
        }

        public async Task<bool> DeleteCertificate(string uuid)
        {
            HttpResponseMessage response = await _httpClient.DeleteAsync(string.Join("/", ENDPOINT, uuid));
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
                throw new Exception($"Request to Avi Vantage was not successful - {response.StatusCode} - {error}");
            }
        }
    }
}
