
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
using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
using Keyfactor.Orchestrators.Extensions.Interfaces;

namespace Keyfactor.Extensions.Orchestrator.Vmware.Nsx
{
    public abstract class NsxJob : IOrchestratorJobExtension
    {
        // Backoff between login retries, doubling each attempt and capped so a long string
        // of retries can't stall a job for an excessive amount of time.
        private const int RetryBaseDelayMs = 1000;
        private const int RetryMaxDelayMs = 5000;

        internal ILogger _logger;
        private long _jobHistoryId;
        private string _apiVersion;
        private int _retriesForFailedAuthentication;
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
            var storeProps = JsonSerializer.Deserialize<Dictionary<string, string>>(store.Properties);
            _apiVersion = storeProps.GetValueOrDefault("ApiVersion");
            _retriesForFailedAuthentication = ParseRetriesForFailedAuthentication(storeProps.GetValueOrDefault("RetriesForFailedAuthentication"));

            try
            {
                string username = ResolvePamField(_pam, config.ServerUsername, "Server Username");
                string password = ResolvePamField(_pam, config.ServerPassword, "Server Password");

                // NSX ALB can be configured to validate logins against an external identity
                // provider (e.g. LDAP/AD) rather than its own local user store. When that
                // provider is momentarily overwhelmed or unreachable, NSX ALB returns the exact
                // same generic "Invalid credentials" error it would for an actually wrong
                // password - there's no way to tell the two apart from the response alone. A
                // short, bounded retry gives that kind of transient identity-provider issue a
                // chance to clear before we give up and fail the whole job over credentials
                // that were correct the entire time. RetriesForFailedAuthentication defaults to
                // 0, so this is a no-op unless an operator explicitly opts in.
                ExecuteWithRetry(
                    () => { Client = new NsxClient(_logger, clientMachine, username, password, tenant, _apiVersion); },
                    _retriesForFailedAuthentication,
                    (attempt, retryEx) => _logger.LogWarning($"Login to VMware NSX ALB failed (attempt {attempt} of {_retriesForFailedAuthentication + 1}): {FlattenException(retryEx)}. This may be a transient issue with a configured external identity provider; retrying."));
            }
            catch (Exception ex)
            {
                ThrowError(ex, "Initialization");
                _logger.LogError("Error during initialization, cannot return proper Error job result. Re-throwing exception.");
                throw;
            }
            _logger.LogTrace($"Configuration complete for {ExtensionName}.");
            _logger.LogTrace($"clientMachine: {clientMachine}");
            _logger.LogTrace($"tenant: {tenant}");
        }

        private static int ParseRetriesForFailedAuthentication(string value)
        {
            return int.TryParse(value, out int retries) && retries > 0 ? retries : 0;
        }

        // Retries action up to maxRetries times on any exception, waiting GetRetryBackoffDelay(attempt)
        // between attempts. The exception from the final attempt propagates unchanged if every retry
        // is exhausted. sleep defaults to a real Thread.Sleep; tests supply a fake so backoff delays
        // don't actually elapse.
        private protected static void ExecuteWithRetry(Action action, int maxRetries, Action<int, Exception> onRetry = null, Action<TimeSpan> sleep = null)
        {
            sleep ??= Thread.Sleep;
            int attempt = 0;
            while (true)
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    attempt++;
                    onRetry?.Invoke(attempt, ex);
                    sleep(GetRetryBackoffDelay(attempt));
                }
            }
        }

        // Exponential backoff starting at RetryBaseDelayMs and doubling each attempt, capped at
        // RetryMaxDelayMs so a configured retry count can't stall a job for an excessive amount of time.
        private protected static TimeSpan GetRetryBackoffDelay(int attemptNumber)
        {
            long delayMs = (long)RetryBaseDelayMs << Math.Min(attemptNumber - 1, 10);
            return TimeSpan.FromMilliseconds(Math.Min(delayMs, RetryMaxDelayMs));
        }

        private protected void DisposeClient()
        {
            try
            {
                Client?.Dispose();
            }
            catch (Exception ex)
            {
                // Client's HttpClient/HttpHandler are always released inside Dispose() before this could be thrown;
                // this only means the NSX ALB logout call itself failed, so just log it rather than masking the job result.
                _logger.LogWarning($"Failed to log out of NSX ALB session: {FlattenException(ex)}");
            }
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
