
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Models;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Utilities.IO.Pem;
using System;
using System.Collections.Generic;
using System.IO;
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
                    catch
                    {
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
