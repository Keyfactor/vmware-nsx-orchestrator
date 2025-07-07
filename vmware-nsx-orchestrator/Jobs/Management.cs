
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Models;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Jobs
{
    public class Management : NsxJob, IManagementJobExtension
    {
        public Management(IPAMSecretResolver pam)
        {
            _logger = LogHandler.GetClassLogger<Management>();
            _pam = pam;
        }

        public JobResult ProcessJob(ManagementJobConfiguration config)
        {
            string clientMachine = ParseClientMachineUrl(config.CertificateStoreDetails.ClientMachine, out string tenant);

            Initialize(clientMachine, config, config.CertificateStoreDetails, tenant);

            switch (config.OperationType)
            {
                case CertStoreOperationType.Add:
                    string certType = GetCertType(config.CertificateStoreDetails.StorePath);
                    return AddCertificateAsync(config.JobCertificate, config.Overwrite, certType).Result;
                case CertStoreOperationType.Remove:
                    return DeleteCertificateAsync(config.JobCertificate.Alias).Result;
                default:
                    return new JobResult()
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        FailureMessage = "Invalid Management Option",
                        JobHistoryId = config.JobHistoryId
                    };
            }
        }

        private async Task<JobResult> AddCertificateAsync(ManagementJobCertificate certInfo, bool overwrite, string certType)
        {
            // transform jobInfo into Nsx Certificate
            SSLKeyAndCertificate cert = ConvertToNsxCertificate(certType, certInfo.Contents, certInfo.PrivateKeyPassword);
            cert.name = certInfo.Alias;

            // if overwrite is set, check for existing cert by alias a.k.a. name
            if (overwrite)
            {
                string uuid = null;
                try
                {
                    _logger.LogTrace($"Looking for existing certificate with name '{certInfo.Alias}'");
                    SSLKeyAndCertificate foundCert = await Client.GetCertificateByName(certInfo.Alias);
                    _logger.LogTrace($"Found existing certificate with name '{certInfo.Alias}' and UUID '{foundCert.uuid}'");
                    uuid = foundCert.uuid;
                }
                catch (Exception ex)
                {
                    // assuming cert was not found (404)
                    // might need to check this assumption
                    _logger.LogWarning($"Certificate marked to overwrite but no matching certificate found with name '{certInfo.Alias}' in NSX ALB");
                }

                if (!string.IsNullOrEmpty(uuid))
                {
                    // replace found cert with cert to add
                    try
                    {
                        _logger.LogDebug($"Attempting to update existing certificate with name '{certInfo.Alias}'");
                        await Client.UpdateCertificate(uuid, cert);
                    }
                    catch (Exception ex)
                    {
                        return ThrowError(ex, "update to existing certificate in NSX ALB");
                    }
                }
                else
                {
                    // no cert found to overwrite
                    _logger.LogInformation($"No cert found to overwrite with name '{certInfo.Alias}'");
                    // add overwrite certificate as normal
                    try
                    {
                        _logger.LogDebug($"Adding certificate after finding no existing certificate with name '{certInfo.Alias}'");
                        await Client.AddCertificate(cert);
                    }
                    catch (Exception ex)
                    {
                        return ThrowError(ex, "addition of certificate (with none to overwrite) to NSX ALB");
                    }
                }
            }
            else
            {
                // add new certificate
                try
                {
                    _logger.LogDebug($"Adding new certificate with name '{certInfo.Alias}'");
                    await Client.AddCertificate(cert);
                }
                catch (Exception ex)
                {
                    return ThrowError(ex, "addition of new certificate to NSX ALB");
                }
            }

            return Success();
        }

        private async Task<JobResult> DeleteCertificateAsync(string name)
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
                return ThrowError(ex, $"Removing certificate by uuid '{uuid}' from NSX ALB");
            }
            return Success();
        }
    }
}
