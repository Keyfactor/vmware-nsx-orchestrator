﻿
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Models;
using Keyfactor.Logging;
using Keyfactor.PKI.PrivateKeys;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NsxConstants = Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Models.Constants;
using Newtonsoft.Json;
using System.Collections.Generic;
using Keyfactor.Orchestrators.Extensions.Interfaces;

namespace Keyfactor.Extensions.Orchestrator.Vmware.Nsx
{
    public abstract class NsxJob : IOrchestratorJobExtension
    {
        internal ILogger _logger;
        private long _jobHistoryId;
        private string _apiVersion;
        private protected IPAMSecretResolver _pam;
        private protected NsxClient Client { get; set; }

        public string ExtensionName => "VMware-NSX";
                
        private protected SSLKeyAndCertificate ConvertToNsxCertificate(string certType, string base64cert, string password)
        {
            SSLKeyAndCertificate nsxCert = new SSLKeyAndCertificate()
            {
                certificate = new SSLCertificate(),
                status = NsxConstants.SSLCertificate.Status.FINISHED,
                type = certType
            };

            if (string.IsNullOrEmpty(password))
            {
                // CA certificate, put contents directly in PEM armor
                nsxCert.certificate.certificate = $"-----BEGIN CERTIFICATE-----\n{base64cert}\n-----END CERTIFICATE-----";
                nsxCert.certificate_base64 = false;
                nsxCert.format = NsxConstants.SSLCertificate.Format.PEM;
                nsxCert.key = "";
            }
            else
            {
                // App or Controller certificate, process with X509Certificate2 and Private Key Converter
                byte[] certBytes = Convert.FromBase64String(base64cert);
                X509Certificate2 x509 = new X509Certificate2(certBytes, password);
                PrivateKeyConverter pkey = PrivateKeyConverterFactory.FromPKCS12(certBytes, password);

                nsxCert.certificate.certificate = $"-----BEGIN CERTIFICATE-----\n{Convert.ToBase64String(x509.RawData, Base64FormattingOptions.InsertLineBreaks)}\n-----END CERTIFICATE-----";

                // check type of key
                string keyType;
                using (AsymmetricAlgorithm keyAlg = x509.GetRSAPublicKey())
                {
                    keyType = keyAlg != null ? "RSA" : "EC";
                }
                
                nsxCert.key = $"-----BEGIN {keyType} PRIVATE KEY-----\n{Convert.ToBase64String(pkey.ToPkcs8BlobUnencrypted())}\n-----END {keyType} PRIVATE KEY-----";
                nsxCert.key_base64 = false;
                nsxCert.key_passphrase = password;
            }

            return nsxCert;
        }

        private protected string GetCertType(string certType)
        {
            return NsxConstants.SSLCertificate.Type.GetType(certType);
        }

        private protected string ParseClientMachineUrl(string clientMachine, out string tenant)
        {
            string url;
            _logger.LogTrace("Parsing NSX client machine for tenant value");

            // if a tenant is being used, the client machine will be formatted: [TENANT]client.machine.url
            if (clientMachine.Contains("[")
                && clientMachine.Contains("]"))
            {
                _logger.LogDebug($"Splitting original client machine: {clientMachine}");
                var split = clientMachine.Split(new string[] { "[", "]" }, 2, StringSplitOptions.RemoveEmptyEntries);
                tenant = split[0];
                url = split[1];

                _logger.LogDebug($"Parsed tenant: {tenant}");
            }
            else
            {
                tenant = null; // null tenant maps to Default tenant
                url = clientMachine;
            }

            _logger.LogDebug($"Parsed client machine url: {url}");
            return url;
        }



        private protected void Initialize(string clientMachine, JobConfiguration config, CertificateStore store, string tenant)
        {
            _jobHistoryId = config.JobHistoryId;

            // check if store properties has an Api Version set
            var storeProps = JsonConvert.DeserializeObject<Dictionary<string, string>>(store.Properties);
            _apiVersion = storeProps.GetValueOrDefault("ApiVersion");

            try
            {
                string username = ResolvePamField(_pam, config.ServerUsername, "Server Username");
                string password = ResolvePamField(_pam, config.ServerPassword, "Server Password");
                Client = new NsxClient(_logger, clientMachine, username, password, tenant, _apiVersion);
            }
            catch (Exception ex)
            {
                ThrowError(ex, "Initialization");
                _logger.LogError("Error during initialization, cannot return proper Error job result. Re-throwing exception.");
                throw;
            }
            _logger.LogTrace($"Configuration complete for {ExtensionName}.");
        }

        private string ResolvePamField(IPAMSecretResolver pam, string key, string fieldName)
        {
            _logger.LogTrace($"Attempting to resolve PAM eligible field: '{fieldName}'");
            return string.IsNullOrEmpty(key) ? key : pam.Resolve(key);
        }

        private protected JobResult Success(string message = null)
        {
            return new JobResult()
            {
                Result = OrchestratorJobStatusJobResult.Success,
                JobHistoryId = _jobHistoryId,
                FailureMessage = message                
            };
        }

        private protected JobResult ThrowError(Exception exception, string jobSection)
        {
            string message = FlattenException(exception);
            _logger.LogError($"Error performing {jobSection} in {ExtensionName} - {message}");
            return new JobResult()
            {
                Result = OrchestratorJobStatusJobResult.Failure,
                FailureMessage = message,
                JobHistoryId = _jobHistoryId
            };
        }

        private string FlattenException(Exception ex)
        {
            string returnMessage = ex.Message;
            if (ex.InnerException != null)
            {
                returnMessage += (" - " + FlattenException(ex.InnerException));
            }
            return returnMessage;
        }
    }
}
