// Copyright 2021 Keyfactor
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

using Keyfactor.AnyAgent.AviVantage.Models;
using Keyfactor.Platform.Extensions.Agents;
using Keyfactor.Platform.Extensions.Agents.Delegates;
using Keyfactor.Platform.Extensions.Agents.Enums;
using Keyfactor.Platform.Extensions.Agents.Interfaces;
using System;
using System.Collections.Generic;

namespace Keyfactor.AnyAgent.AviVantage.Jobs
{
    [Job(Constants.JobTypes.INVENTORY)]
    public class Inventory : AviVantageJob, IAgentJobExtension
    {
        public override AnyJobCompleteInfo processJob(AnyJobConfigInfo config, SubmitInventoryUpdate submitInventory, SubmitEnrollmentRequest submitEnrollmentRequest, SubmitDiscoveryResults sdr)
        {
            Initialize(config);
            List<SSLKeyAndCertificate> allCerts;
            List<AgentCertStoreInventoryItem> inventory = new List<AgentCertStoreInventoryItem>();

            try
            {
                string certType = GetAviCertType(config.Store.StorePath);
                allCerts = Client.GetAllCertificates(certType).Result;
            }
            catch (Exception ex)
            {
                return ThrowError(ex, "Certificate Retrieval");
            }

            foreach(var foundCert in allCerts)
            {
                inventory.Add(new AgentCertStoreInventoryItem()
                {
                    Alias = foundCert.name,
                    Certificates = new string[] { foundCert.certificate.certificate }, // need to check base64 status
                    PrivateKeyEntry = !string.IsNullOrEmpty(foundCert.key),
                    UseChainLevel = false,
                    ItemStatus = AgentInventoryItemStatus.Unknown
                });
            }

            submitInventory.Invoke(inventory);
            return Success();
        }
    }
}
