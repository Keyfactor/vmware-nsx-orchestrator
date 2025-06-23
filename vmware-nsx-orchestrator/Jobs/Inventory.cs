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
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Utilities.IO.Pem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;
using PemWriter = Org.BouncyCastle.OpenSsl.PemWriter;

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

            // ----added this for testing only

            //var badCertString = "Bag Attributes\r\n    localKeyID: 01 00 00 00 \r\n    friendlyName: MasGarantiaPif\r\nsubject=C = MX, L = Ciudad de Mexico, O = Servicios Liverpool SA de CV., CN = masgarantiapifqa.liverpool.com.mx\r\n\r\nissuer=C = US, O = DigiCert Inc, OU = www.digicert.com, CN = GeoTrust TLS RSA CA G1\r\n\r\n-----BEGIN CERTIFICATE-----\r\nMIIGuTCCBaGgAwIBAgIQCF+qzMNrz2WqMlpG4wPctDANBgkqhkiG9w0BAQsFADBg\r\nMQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGlnaUNlcnQgSW5jMRkwFwYDVQQLExB3\r\nd3cuZGlnaWNlcnQuY29tMR8wHQYDVQQDExZHZW9UcnVzdCBUTFMgUlNBIENBIEcx\r\nMB4XDTI0MTIwMzAwMDAwMFoXDTI1MTIxMDIzNTk1OVowfDELMAkGA1UEBhMCTVgx\r\nGTAXBgNVBAcTEENpdWRhZCBkZSBNZXhpY28xJjAkBgNVBAoTHVNlcnZpY2lvcyBM\r\naXZlcnBvb2wgU0EgZGUgQ1YuMSowKAYDVQQDEyFtYXNnYXJhbnRpYXBpZnFhLmxp\r\ndmVycG9vbC5jb20ubXgwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDZ\r\nnf+xf08SYiT30UHRix86hCRXlLtlGkrS1xifHbMH5uHMOeCJ3biLT31kc7cRJySO\r\nHmr1WRqg+nW4KBtut8ElJrSyKaAiEJOSBUWh0inLjNRX+mZuiarLlt3tt9pCjJ3v\r\nDq9Wk7fHBQExZwQNiplY0zDYl6nPrLwC1Qp8T2w5S66el3gvyxUoyCC1DnNnKsoQ\r\n6dGoVOrWWi4fixAesHKjaDtgpeo1IQjuQaBFNr+QRE3zsBFd0caZ/NbA7CiECbk5\r\nRJefr9cnCHDPTwGYJkRD3A+GAtWubwKGq46WJanLCPvZMJG2ZMYa5RLx0+aXlWZz\r\n6QabIj78HtGRvYqMAUH5AgMBAAGjggNRMIIDTTAfBgNVHSMEGDAWgBSUT9Rdi+Sk\r\n4qaA/v3Y+QDvo74CVzAdBgNVHQ4EFgQUmrclnBlLtmis1D+p+rA7V+U6IfcwUwYD\r\nVR0RBEwwSoIhbWFzZ2FyYW50aWFwaWZxYS5saXZlcnBvb2wuY29tLm14giV3d3cu\r\nbWFzZ2FyYW50aWFwaWZxYS5saXZlcnBvb2wuY29tLm14MD4GA1UdIAQ3MDUwMwYG\r\nZ4EMAQICMCkwJwYIKwYBBQUHAgEWG2h0dHA6Ly93d3cuZGlnaWNlcnQuY29tL0NQ\r\nUzAOBgNVHQ8BAf8EBAMCBaAwHQYDVR0lBBYwFAYIKwYBBQUHAwEGCCsGAQUFBwMC\r\nMD8GA1UdHwQ4MDYwNKAyoDCGLmh0dHA6Ly9jZHAuZ2VvdHJ1c3QuY29tL0dlb1Ry\r\ndXN0VExTUlNBQ0FHMS5jcmwwdgYIKwYBBQUHAQEEajBoMCYGCCsGAQUFBzABhhpo\r\ndHRwOi8vc3RhdHVzLmdlb3RydXN0LmNvbTA+BggrBgEFBQcwAoYyaHR0cDovL2Nh\r\nY2VydHMuZ2VvdHJ1c3QuY29tL0dlb1RydXN0VExTUlNBQ0FHMS5jcnQwDAYDVR0T\r\nAQH/BAIwADCCAX4GCisGAQQB1nkCBAIEggFuBIIBagFoAHcAEvFONL1TckyEBhnD\r\njz96E/jntWKHiJxtMAWE6+WGJjoAAAGTjV/vzAAABAMASDBGAiEAl7VcxSuzwvmA\r\nMo5aeMgZ+vegjd70HDCv/ShJG2q3JcYCIQC/oHEN9kmJxOea6bXdg6YVLJjmjGF9\r\nveg06Y8TOP14gQB1AO08S9boBsKkogBX28sk4jgB31Ev7cSGxXAPIN23Pj/gAAAB\r\nk41f74UAAAQDAEYwRAIgBUwa6pxjg8T75utYf6uNNF6tfZJPSiZGMzKazE8LLnsC\r\nICA3oR4yFwKvtI4EUzygsG0ow4uO2E1XscgYwJXiJH2BAHYA5tIxY0B3jMEQQQbX\r\ncbnOwdJA9paEhvu6hzId/R43jlAAAAGTjV/vmAAABAMARzBFAiBOfq3oUv4Tq05W\r\naToDiyQH6jonBGuccsx+/I3gderbMQIhAIutqFSZYhFNUnc1jzpgvhHCTusndBtL\r\n0QYWZ79981LLMA0GCSqGSIb3DQEBCwUAA4IBAQAz2Ed3nRnCMOzh8aSsl1OwslZe\r\nqXm6triNlTTxSQdPZoUXJr8BnJQ5nH1TUudc8nM1MutRiUX6oIN6j1wc5+b9ghN3\r\nBIXdAGELUzBTk07R06ObK544iYaT+S4z5aRZTB+eBC0RxYNB7o+hSqMaZheyQabI\r\nDY5sBrV8N4GjAyFFbAndnnG0L+B8DQU8E0OHBjPYkqtKfLF9XepTNllQEppnUSlK\r\nnXi5o1/PFr42a3CgYPb16H1qvWAqxEojZVr7NdanHj8h6cPUZGz3Jf/QkW/XxfD4\r\n9nShgexVmiWWdXJufDZhRRUTO+7UgHJix1KQDymnn5m1C9iyraV7sAWUV/Yj\r\n-----END CERTIFICATE-----";
            //allCerts.Add(new SSLKeyAndCertificate() { name = "badCert", certificate = new SSLCertificate() { certificate = badCertString } });

            // ----

            foreach (var foundCert in allCerts)
            {
                _logger.LogTrace($"Found Certificate - {foundCert.name}");

                // the below check is in place to prevent an error on older versions of the UO framework
                // when parsing PEM data with extra text before wrapper tags.  

                #region checkPEMformat

                try
                {
                    // try pemtoder
                    var test = PKI.PEM.PemUtilities.PEMToDER(foundCert.certificate.certificate);
                }
                catch (Exception ex)
                {
                    // it failed, attempt cleanup.
                    
                    _logger.LogWarning("Unable to perform PEM to DER conversion on cert contents.");
                    
                    // try cleanup up extra info

                    var cleanPEM = CleanPEMString(foundCert.certificate.certificate);

                    try
                    {
                        var test = PKI.PEM.PemUtilities.PEMToDER(cleanPEM);
                        // success if no exception.

                        inventory.Add(new CurrentInventoryItem()
                        {
                            Alias = foundCert.name,
                            Certificates = new string[] { cleanPEM },
                            PrivateKeyEntry = !string.IsNullOrEmpty(foundCert.key),
                            UseChainLevel = false
                        });

                        continue;
                    }
                    catch {
                        _logger.LogWarning($"still failing to parse, skipping this one ({foundCert.name}) and continuing with inventory.");
                        warningCount++;
                        continue;
                    }
                }

                #endregion

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

        /// <summary>
        /// This method does a preliminary check to circumvent the UO framework error when parsing headers
        /// </summary>
        /// <param name="pemCert"></param>
        /// <param name="cleanPem">If </param>
        /// <returns></returns>
        string CleanPEMString(string dirtyPEM)
        {
            _logger.LogWarning("attempting to clean failing PEM string");
            _logger.LogWarning("original cert contents:");
            _logger.LogWarning($"\n{dirtyPEM}");

            using (var sr = new StringReader(dirtyPEM))
            {
                Org.BouncyCastle.OpenSsl.PemReader pemReader = new Org.BouncyCastle.OpenSsl.PemReader(sr);

                var pemObj = pemReader.ReadPemObject();
                
                _logger.LogWarning("Unable to perform PEM to DER conversion on cert contents.");
                _logger.LogWarning("cert contents:");
                _logger.LogWarning($"\n{dirtyPEM}");

                PemObject po = new PemObject("CERTIFICATE", pemObj.Content);
                _logger.LogTrace("content (without comments): ");
                var sw = new StringWriter();
                var pw = new PemWriter(sw);
                pw.WriteObject(po);
                _logger.LogTrace($"{sw.ToString()}");
                return sw.ToString();
            }

        }
    }
}
