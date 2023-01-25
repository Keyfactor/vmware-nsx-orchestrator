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

using CSS.Common.Logging;
using CSS.PKI.PrivateKeys;
using Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Models;
using Keyfactor.Platform.Extensions.Agents;
using Keyfactor.Platform.Extensions.Agents.Delegates;
using Keyfactor.Platform.Extensions.Agents.Interfaces;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AviConstants = Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Models.Constants;

namespace Keyfactor.Extensions.Orchestrator.Vmware.Nsx
{
    public abstract class NsxJob : LoggingClientBase, IAgentJobExtension
    {
        private protected NsxClient Client { get; set; }

        public string GetJobClass()
        {
            JobAttribute attribute = GetType().GetCustomAttributes(true).First(a => a.GetType() == typeof(JobAttribute)) as JobAttribute;
            return attribute?.JobClass ?? string.Empty;
        }

        public string GetStoreType() => Constants.STORE_TYPE_NAME;

        public abstract AnyJobCompleteInfo processJob(AnyJobConfigInfo config, SubmitInventoryUpdate submitInventory, SubmitEnrollmentRequest submitEnrollmentRequest, SubmitDiscoveryResults sdr);

        private protected SSLKeyAndCertificate ConvertToAviCertificate(string certType, string base64cert, string password)
        {
            SSLKeyAndCertificate aviCert = new SSLKeyAndCertificate()
            {
                certificate = new SSLCertificate(),
                status = AviConstants.SSLCertificate.Status.FINISHED,
                type = certType
            };

            if (string.IsNullOrEmpty(password))
            {
                // CA certificate, put contents directly in PEM armor
                aviCert.certificate.certificate = $"-----BEGIN CERTIFICATE-----\n{base64cert}\n-----END CERTIFICATE-----";
                aviCert.certificate_base64 = false;
                aviCert.format = AviConstants.SSLCertificate.Format.PEM;
                aviCert.key = "";
            }
            else
            {
                // App or Controller certificate, process with X509Certificate2 and Private Key Converter
                byte[] certBytes = Convert.FromBase64String(base64cert);
                X509Certificate2 x509 = new X509Certificate2(certBytes, password);
                PrivateKeyConverter pkey = PrivateKeyConverterFactory.FromPKCS12(certBytes, password);

                aviCert.certificate.certificate = $"-----BEGIN CERTIFICATE-----\n{Convert.ToBase64String(x509.RawData)}\n-----END CERTIFICATE-----";

                // check type of key
                string keyType;
                using (AsymmetricAlgorithm keyAlg = x509.GetRSAPublicKey())
                {
                    keyType = keyAlg != null ? "RSA" : "EC";
                }
                aviCert.key = $"-----BEGIN {keyType} PRIVATE KEY-----\n{Convert.ToBase64String(pkey.ToPkcs8BlobUnencrypted())}\n-----END {keyType} PRIVATE KEY-----";
                aviCert.key_base64 = false;
                aviCert.key_passphrase = password;
            }

            return aviCert;
        }

        private protected string GetAviCertType(string certType)
        {
            return AviConstants.SSLCertificate.Type.GetType(certType);
        }

        private protected void Initialize(AnyJobConfigInfo config)
        {
            try
            {
                Client = new AviVantageClient(config.Store.ClientMachine, config.Server.Username, config.Server.Password);
            }
            catch (Exception ex)
            {
                ThrowError(ex, "Initialization");
            }
            Logger.Trace($"Configuration complete for {GetStoreType()} {GetJobClass()}.");
        }

        private protected AnyJobCompleteInfo Success(string message = null)
        {
            return new AnyJobCompleteInfo()
            {
                Status = 2,
                Message = message ?? $"{GetStoreType()} {GetJobClass()} Completed Successfully."
            };
        }

        private protected AnyJobCompleteInfo ThrowError(Exception exception, string jobSection)
        {
            string message = FlattenException(exception);
            Logger.Error($"Error performing {jobSection} in {GetStoreType()} {GetJobClass()} - {message}");
            return new AnyJobCompleteInfo()
            {
                Status = 4,
                Message = message
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
