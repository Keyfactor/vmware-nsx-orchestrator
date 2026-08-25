
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

// NsxJobDisposalTests.cs
// Regression tests for the NSX ALB session-leak fix: every job must dispose (and therefore
// log out) its NsxClient, and a failed logout must never propagate and mask the job's result.
//
// ProcessJob's own Initialize() call always attempts a real login before the try/finally that
// now calls DisposeClient(), so it can't be exercised end-to-end without a network seam that
// doesn't exist in production. Instead — matching how the sibling vmware-vcenter-orchestrator
// test suite handles the same constraint — these tests drive NsxJob.DisposeClient() directly
// against a mock-backed client, which is the exact code path the fix added.

using System.Net;
using Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Jobs;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Tests
{
    public class NsxJobDisposalTests
    {
        [Fact]
        public void DisposeClient_LogoutSucceeds_LogsOutWithoutWarning()
        {
            var mock = new NsxHttpMockBuilder().WithLogin().WithLogout();
            var logger = new RecordingLogger();
            var client = mock.BuildClient(logger);

            var inventory = new Inventory(null);
            ReflectionHelpers.SetField(inventory, typeof(NsxJob), "_logger", logger);
            ReflectionHelpers.SetProperty(inventory, typeof(NsxJob), "Client", client);

            ReflectionHelpers.Invoke(inventory, typeof(NsxJob), "DisposeClient");

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
        }

        [Fact]
        public void DisposeClient_LogoutRejectedByController_SwallowsExceptionAndLogsWarning()
        {
            // This is the exact regression the fix guards against: NsxClient.Dispose() re-throws
            // when the NSX ALB logout call itself fails (e.g. the session already expired
            // server-side). Before the fix, nothing called Dispose()/Logout() at all, so
            // sessions leaked and accumulated against the service account until Avi's
            // session/login limits started intermittently rejecting logins with valid
            // credentials. Now that jobs call DisposeClient() in a finally block, a failed
            // logout must never propagate and mask the job's actual result.
            var mock = new NsxHttpMockBuilder()
                .WithLogin()
                .WithLogoutError(HttpStatusCode.InternalServerError);
            var logger = new RecordingLogger();
            var client = mock.BuildClient(logger);

            var management = new Management(null);
            ReflectionHelpers.SetField(management, typeof(NsxJob), "_logger", logger);
            ReflectionHelpers.SetProperty(management, typeof(NsxJob), "Client", client);

            // Should not throw despite the mocked logout call failing.
            ReflectionHelpers.Invoke(management, typeof(NsxJob), "DisposeClient");

            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Failed to log out"));
        }

        [Fact]
        public void DisposeClient_ClientNeverInitialized_DoesNotThrow()
        {
            // Initialize() never assigns Client when login itself fails, so DisposeClient()
            // must tolerate a null Client instead of throwing a NullReferenceException.
            var logger = new RecordingLogger();
            var inventory = new Inventory(null);
            ReflectionHelpers.SetField(inventory, typeof(NsxJob), "_logger", logger);

            ReflectionHelpers.Invoke(inventory, typeof(NsxJob), "DisposeClient");

            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
        }
    }
}
