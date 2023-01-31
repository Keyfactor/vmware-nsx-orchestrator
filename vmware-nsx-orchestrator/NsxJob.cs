﻿// Copyright 2023 Keyfactor
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
using Keyfactor.PKI.PrivateKeys;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NsxConstants = Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Models.Constants;

namespace Keyfactor.Extensions.Orchestrator.Vmware.Nsx
{
    public abstract class NsxJob : IOrchestratorJobExtension
    {
        private ILogger _logger;
        private long _jobHistoryId;
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

                nsxCert.certificate.certificate = $"-----BEGIN CERTIFICATE-----\n{Convert.ToBase64String(x509.RawData)}\n-----END CERTIFICATE-----";

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

        private protected string GetTenant(string storePath)
        {
            if (string.IsNullOrWhiteSpace(storePath)
                || storePath.Equals("DEFAULT"))
            {
                // if store path was set to DEFAULT, use default tenant i.e. no tenant
                return null;
            }

            return storePath;
        }

        private protected void Initialize(string clientMachine, string username, string password, string tenant, long jobHistoryId, ILogger logger)
        {
            _logger = logger;
            _jobHistoryId = jobHistoryId;
            try
            {
                Client = new NsxClient(clientMachine, username, password, tenant);
            }
            catch (Exception ex)
            {
                ThrowError(ex, "Initialization");
            }
            _logger.LogTrace($"Configuration complete for {ExtensionName}.");
        }

        private protected JobResult Success(string message = null)
        {
            return new JobResult()
            {
                Result = OrchestratorJobStatusJobResult.Success,
                JobHistoryId = _jobHistoryId
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
