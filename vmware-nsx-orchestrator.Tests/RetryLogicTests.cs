
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

// RetryLogicTests.cs
// Unit tests for the RetriesForFailedAuthentication retry/backoff logic on NsxJob
// (ExecuteWithRetry, GetRetryBackoffDelay, ParseRetriesForFailedAuthentication).
//
// This exercises the retry mechanics in isolation, with a fake "sleep" delegate injected via
// reflection so tests run instantly instead of actually waiting out the backoff delays.

using System;
using System.Collections.Generic;
using Xunit;

namespace Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Tests
{
    public class RetryLogicTests
    {
        // ------------------------------------------------------------------
        // GetRetryBackoffDelay
        // ------------------------------------------------------------------

        [Theory]
        [InlineData(1, 1000)]
        [InlineData(2, 2000)]
        [InlineData(3, 4000)]
        [InlineData(4, 5000)] // would be 8000 uncapped
        [InlineData(5, 5000)] // stays capped
        public void GetRetryBackoffDelay_DoublesUntilCapped(int attemptNumber, int expectedMs)
        {
            var delay = (TimeSpan)ReflectionHelpers.InvokeStatic(typeof(NsxJob), "GetRetryBackoffDelay", attemptNumber);

            Assert.Equal(expectedMs, delay.TotalMilliseconds);
        }

        // ------------------------------------------------------------------
        // ExecuteWithRetry
        // ------------------------------------------------------------------

        [Fact]
        public void ExecuteWithRetry_SucceedsFirstTry_NeverRetries()
        {
            int calls = 0;
            var retries = new List<int>();
            var sleeps = new List<TimeSpan>();

            Action action = () => calls++;
            Action<int, Exception> onRetry = (attempt, ex) => retries.Add(attempt);
            Action<TimeSpan> sleep = sleeps.Add;

            ReflectionHelpers.InvokeStatic(typeof(NsxJob), "ExecuteWithRetry", action, 3, onRetry, sleep);

            Assert.Equal(1, calls);
            Assert.Empty(retries);
            Assert.Empty(sleeps);
        }

        [Fact]
        public void ExecuteWithRetry_FailsThenSucceeds_RetriesWithIncreasingBackoff()
        {
            int calls = 0;
            var retries = new List<int>();
            var sleeps = new List<TimeSpan>();

            Action action = () =>
            {
                calls++;
                if (calls <= 2) throw new Exception($"transient failure {calls}");
            };
            Action<int, Exception> onRetry = (attempt, ex) => retries.Add(attempt);
            Action<TimeSpan> sleep = sleeps.Add;

            ReflectionHelpers.InvokeStatic(typeof(NsxJob), "ExecuteWithRetry", action, 2, onRetry, sleep);

            Assert.Equal(3, calls); // 1 initial attempt + 2 retries before succeeding
            Assert.Equal(new[] { 1, 2 }, retries);
            Assert.Equal(new[] { TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(2000) }, sleeps);
        }

        [Fact]
        public void ExecuteWithRetry_ExhaustsRetries_PropagatesFinalException()
        {
            int calls = 0;
            var retries = new List<int>();

            Action action = () =>
            {
                calls++;
                throw new Exception($"failure {calls}");
            };
            Action<int, Exception> onRetry = (attempt, ex) => retries.Add(attempt);
            Action<TimeSpan> sleep = _ => { };

            var ex = Assert.Throws<Exception>(() =>
                ReflectionHelpers.InvokeStatic(typeof(NsxJob), "ExecuteWithRetry", action, 2, onRetry, sleep));

            Assert.Equal(3, calls); // 1 initial attempt + 2 retries, all failing
            Assert.Equal("failure 3", ex.Message); // the last attempt's exception, not an earlier one
            Assert.Equal(new[] { 1, 2 }, retries); // onRetry only fires for the retryable failures
        }

        [Fact]
        public void ExecuteWithRetry_ZeroRetriesConfigured_FailsImmediately()
        {
            int calls = 0;
            var retries = new List<int>();
            var sleeps = new List<TimeSpan>();

            Action action = () =>
            {
                calls++;
                throw new Exception("auth failed");
            };
            Action<int, Exception> onRetry = (attempt, ex) => retries.Add(attempt);
            Action<TimeSpan> sleep = sleeps.Add;

            Assert.Throws<Exception>(() =>
                ReflectionHelpers.InvokeStatic(typeof(NsxJob), "ExecuteWithRetry", action, 0, onRetry, sleep));

            Assert.Equal(1, calls); // default (0 retries) behaves exactly like no retry logic at all
            Assert.Empty(retries);
            Assert.Empty(sleeps);
        }

        // ------------------------------------------------------------------
        // ParseRetriesForFailedAuthentication
        // ------------------------------------------------------------------

        [Theory]
        [InlineData(null, 0)]
        [InlineData("", 0)]
        [InlineData("not-a-number", 0)]
        [InlineData("-5", 0)] // negative retry counts are nonsensical; treat as disabled
        [InlineData("0", 0)]
        [InlineData("3", 3)]
        public void ParseRetriesForFailedAuthentication_HandlesMissingAndInvalidValues(string rawValue, int expected)
        {
            var result = (int)ReflectionHelpers.InvokeStatic(typeof(NsxJob), "ParseRetriesForFailedAuthentication", rawValue);

            Assert.Equal(expected, result);
        }
    }
}
