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

namespace Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Models
{
    public class SSLKeyAndCertificate
    {
        public SSLCertificate certificate;
        public bool certificate_base64;
        public string format;
        public string key;
        public string key_passphrase;
        public SSLKeyParams key_params;
        public bool key_base64;
        public string name;
        public string status;
        public string type;

        // Avi Vantage generated fields
        public string url;
        public string uuid;
        public string tenant_ref;
    }

    public class SSLCertificate
    {
        public string certificate;
        public string not_after;
        public SSLCertificateDescription subject;
    }

    public class SSLCertificateDescription
    {
        public string common_name;
        public string country;
        public string distinguished_name;
        public string email_address;
        public string locality;
        public string organization;
        public string organization_unit;
        public string state;
    }

    public class SSLKeyParams
    {
        public string algorithm;
        public SSLKeyECParams ec_params;
        public SSLKeyRSAParams rsa_params;
    }

    public class SSLKeyRSAParams
    {
        public int exponent;
        public string key_size;
    }

    public class SSLKeyECParams
    {
        public string curve;
    }
}
