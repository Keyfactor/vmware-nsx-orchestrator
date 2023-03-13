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
using System;
using System.Collections.Generic;

namespace Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Jobs
{
    public class Inventory : NsxJob, IInventoryJobExtension
    {
        public JobResult ProcessJob(InventoryJobConfiguration config, SubmitInventoryUpdate submitInventory)
        {
            _logger = LogHandler.GetClassLogger<Inventory>();

            string clientMachine = ParseClientMachineUrl(config.CertificateStoreDetails.ClientMachine, out string tenant);

            Initialize(clientMachine, config.ServerUsername, config.ServerPassword, tenant, config.JobHistoryId);
            List<SSLKeyAndCertificate> allCerts;
            List<CurrentInventoryItem> inventory = new List<CurrentInventoryItem>();

            try
            {
                string certType = GetCertType(config.CertificateStoreDetails.StorePath);
                allCerts = Client.GetAllCertificates(certType).Result;
            }
            catch (Exception ex)
            {
                return ThrowError(ex, "Certificate Retrieval");
            }

            foreach(var foundCert in allCerts)
            {
                inventory.Add(new CurrentInventoryItem()
                {
                    Alias = foundCert.name,
                    Certificates = new string[] { foundCert.certificate.certificate }, // need to check base64 status
                    PrivateKeyEntry = !string.IsNullOrEmpty(foundCert.key),
                    UseChainLevel = false,
                    ItemStatus = Orchestrators.Common.Enums.OrchestratorInventoryItemStatus.Unknown
                });
            }

            submitInventory.Invoke(inventory);
            return Success();
        }
    }
}
