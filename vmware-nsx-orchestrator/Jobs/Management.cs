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
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace Keyfactor.AnyAgent.AviVantage.Jobs
{
    [Job(Constants.JobTypes.MANAGEMENT)]
    public class Management : AviVantageJob, IAgentJobExtension
    {
        public override AnyJobCompleteInfo processJob(AnyJobConfigInfo config, SubmitInventoryUpdate submitInventory, SubmitEnrollmentRequest submitEnrollmentRequest, SubmitDiscoveryResults sdr)
        {
            Initialize(config);

            switch (config.Job.OperationType)
            {
                case AnyJobOperationType.Add:
                    string certType = GetAviCertType(config.Store.StorePath);
                    return AddCertificateAsync(config.Job, certType).Result;
                case AnyJobOperationType.Remove:
                    return DeleteCertificateAsync(config.Job.Alias).Result;
                default:
                    return new AnyJobCompleteInfo()
                    {
                        Status = 4,
                        Message = "Invalid Management Option"
                    };
            }
        }

        private async Task<AnyJobCompleteInfo> AddCertificateAsync(AnyJobJobInfo jobInfo, string certType)
        {
            // transform jobInfo into Avi Certificate
            SSLKeyAndCertificate cert = ConvertToAviCertificate(certType, jobInfo.EntryContents, jobInfo.PfxPassword);
            cert.name = jobInfo.Alias;

            // if overwrite is set, check for existing cert by alias a.k.a. name
            if (jobInfo.Overwrite)
            {
                string uuid = null;
                try
                {
                    SSLKeyAndCertificate foundCert = await Client.GetCertificateByName(jobInfo.Alias);
                    uuid = foundCert.uuid;
                }
                catch (Exception ex)
                {
                    // assuming cert was not found (404)
                    // might need to check this assumption
                    Logger.Warn($"Certificate marked to overwrite but no matching certificate found with name '{jobInfo.Alias}' in Avi Vantage");
                }

                if (!string.IsNullOrEmpty(uuid))
                {
                    // replace found cert with cert to add
                    try
                    {
                        await Client.UpdateCertificate(uuid, cert);
                    }
                    catch (Exception ex)
                    {
                        return ThrowError(ex, "update to existing certificate in Avi Vantage");
                    }
                }
                else
                {
                    // no cert found
                    Logger.Info($"No cert found to overwrite with name '{jobInfo.Alias}'");
                }
            }

            // add new certificate
            try
            {
                await Client.AddCertificate(cert);
            }
            catch (Exception ex)
            {
                return ThrowError(ex, "addition of new certificate to Avi Vantage");
            }
            return Success();
        }

        private async Task<AnyJobCompleteInfo> DeleteCertificateAsync(string name)
        {
            string uuid;
            try
            {
                SSLKeyAndCertificate foundCert = await Client.GetCertificateByName(name);
                uuid = foundCert.uuid;
            }
            catch (Exception ex)
            {
                return ThrowError(ex, $"Retrieving certificate by name '{name}' for deletion");
            }
            try
            {
                await Client.DeleteCertificate(uuid);
            }
            catch (Exception ex)
            {
                return ThrowError(ex, $"Removing certificate by uuid '{uuid}' from Avi Vantage");
            }
            return Success();
        }
    }
}
