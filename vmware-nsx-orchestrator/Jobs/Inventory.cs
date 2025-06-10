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
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Jobs
{
    public class Inventory : NsxJob, IInventoryJobExtension
    {
        private const int PAGE_SIZE = 2;

        public Inventory(IPAMSecretResolver pam)
        {
            _logger = LogHandler.GetClassLogger<Inventory>();
            _pam = pam;
        }

        public JobResult ProcessJob(InventoryJobConfiguration config, SubmitInventoryUpdate submitInventory)
        {
            string clientMachine = ParseClientMachineUrl(config.CertificateStoreDetails.ClientMachine, out string tenant);

            Initialize(clientMachine, config, config.CertificateStoreDetails, tenant);
            List<SSLKeyAndCertificate> allCerts;
            List<CurrentInventoryItem> inventory = new List<CurrentInventoryItem>();

            string certType = GetCertType(config.CertificateStoreDetails.StorePath);
            try
            {
                allCerts = Client.GetAllCertificates(certType, PAGE_SIZE).Result;
            }
            catch (Exception ex)
            {
                return ThrowError(ex, "Certificate Retrieval");
            }

            _logger.LogDebug($"Total certificates found of type {certType} - {allCerts.Count}");
            var warningCount = 0;


            // ---- added this for testing only
            // var badCertString = "----BEGIN CERTIFICATE-----\r\nMIIDOTCCAiGgAwIBAgIUVum9+J50qpc29CSzIc195ovWO1EwDQYJKoZIhvcNAQEL\r\nBQAwGDEWMBQGA1UEAwwNKi5rZnRyYWluLmxhYjAeFw0yNTA2MDUyMDA4NTRaFw0y\r\nNjA2MDUyMDA4NTRaMBgxFjAUBgNVBAMMDSoua2Z0cmFpbi5sYWIwggEiMA0GCSqG\r\nSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDLXD/fEDq1Nb+wOwZiDcvVtQMJ5pWWGCL/\r\ncUcEHDI43ib2mpQ75U6fshyCf+LVyvIs3xtBolvfHKD0E2sQgjfFjmf7sMDdnjzf\r\n1xeeuSnuqAn78Ocvz2da88OrSmsnI+ncGXqMzEZtvnUfUZyFKkhVNfvT8HxS+ypz\r\nw1jcvU+5wMzN3cNJSzeZLCQ8wkFHP+WhC+xlAf8iIHfx5KO+brJFxBL/VyteEWWY\r\n2DPgxxiLTqYrHG2ifjV6WUl1ty0Qh1YrR75IEyfnRwQx/hJmnLshLkGLSJ99GGLc\r\nPcpdXAwKKmlHdU43G06H9vO+r24D+txMbD0AD4IZF0BEIkr66iUDAgMBAAGjezB5\r\nMAkGA1UdEwQCMAAwLAYJYIZIAYb4QgENBB8WHU9wZW5TU0wgR2VuZXJhdGVkIENl\r\ncnRpZmljYXRlMB0GA1UdDgQWBBRtDk3lmPekrz0jkBoAwtzVHcNOBTAfBgNVHSME\r\nGDAWgBRtDk3lmPekrz0jkBoAwtzVHcNOBTANBgkqhkiG9w0BAQsFAAOCAQEAym1p\r\ncJtIS+9HeAWUIZt+7NqD7TR31I4bai6QTTlkuYPFnmWjwhZbHDpmjGkP930+SfGd\r\nDsVPLwR2sSVUAfpIcqwbZPcH7Nuh9t0/ZLm5FfbBXDInapmqZThliOAPwMIkdVW8\r\nZJbRWd4r9xYLsFwLaQm9XVmedoZQovde+GJx30r3HCNCBdFyxy6TDI62IUEX7Nt7\r\nkG3kZ/XpNC5+zfwdR5O4kEpBNRT0+oCau5s7LMrSG1gg7bjODRUKUCue9FCFViVs\r\nM5JDIFayiGttwJv7jN+r/YBetSaVQsChEkh4djFvqnyNUg5h5A3QrksLxXm4ZBJn\r\nzptyUbe/m/cpCVx13A==\r\n----END CERTIFICATE-----\r\n-----END CERTIFICATE-----\r\n";
            // allCerts.Add(new SSLKeyAndCertificate() { name = "badCert", certificate = new SSLCertificate() { certificate = badCertString } });
            // ----

            foreach (var foundCert in allCerts)
            {
                _logger.LogTrace($"Found Certificate - {foundCert.name}");
                _logger.LogTrace(foundCert.certificate.certificate);
                try
                {
                    // try pemtoder
                    var test = PKI.PEM.PemUtilities.PEMToDER(foundCert.certificate.certificate);
                }
                catch (Exception ex)
                {
                    // it failed; log a warning and continue.

                    _logger.LogWarning("Unable to perform PEM to DER conversion on cert contents.");
                    _logger.LogWarning("cert contents:");
                    _logger.LogWarning($"\n{foundCert.certificate.certificate}");
                    warningCount++;
                    continue;
                }

                inventory.Add(new CurrentInventoryItem()
                {
                    Alias = foundCert.name,
                    Certificates = new string[] { foundCert.certificate.certificate }, 
                    PrivateKeyEntry = !string.IsNullOrEmpty(foundCert.key),
                    UseChainLevel = false
                });
            }

            var successMessage = $"Successfully processed {inventory.Count} certificates. ";
            if (warningCount > 0) successMessage += $"\n{warningCount} certificate(s) could not be processed.\nReview the logs on the orchestrator for more details.";
            if (submitInventory.Invoke(inventory)) return Success(successMessage);
            return ThrowError(new Exception("Inventory Job Failed.  Review the orchestrator logs for more details."), "Inventory");
        }
    }
}
