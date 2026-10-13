
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

// ReflectionHelpers.cs
// NsxClient and NsxJob hard-construct their own HttpClient / cookie state and expose no
// test seams (private fields, get-only auto-properties, private/private-protected methods).
// Rather than change production code just to make it testable, these helpers reach past
// accessibility modifiers the same way the sibling vmware-vcenter-orchestrator test suite does.

using System;
using System.Reflection;

namespace Keyfactor.Extensions.Orchestrator.Vmware.Nsx.Tests
{
    internal static class ReflectionHelpers
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags StaticNonPublic = BindingFlags.NonPublic | BindingFlags.Static;

        public static void SetField(object target, Type declaringType, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, InstanceNonPublic)
                ?? throw new InvalidOperationException($"Could not find field '{fieldName}' on {declaringType.Name}.");
            field.SetValue(target, value);
        }

        public static void SetBackingField(object target, Type declaringType, string autoPropertyName, object value)
        {
            SetField(target, declaringType, $"<{autoPropertyName}>k__BackingField", value);
        }

        public static void SetProperty(object target, Type declaringType, string propertyName, object value)
        {
            var property = declaringType.GetProperty(propertyName, InstanceNonPublic)
                ?? throw new InvalidOperationException($"Could not find property '{propertyName}' on {declaringType.Name}.");
            property.SetValue(target, value);
        }

        public static object Invoke(object target, Type declaringType, string methodName, params object[] args)
        {
            var method = declaringType.GetMethod(methodName, InstanceNonPublic)
                ?? throw new InvalidOperationException($"Could not find method '{methodName}' on {declaringType.Name}.");
            return InvokeAndUnwrap(method, target, args);
        }

        public static object InvokeStatic(Type declaringType, string methodName, params object[] args)
        {
            var method = declaringType.GetMethod(methodName, StaticNonPublic)
                ?? throw new InvalidOperationException($"Could not find static method '{methodName}' on {declaringType.Name}.");
            return InvokeAndUnwrap(method, null, args);
        }

        private static object InvokeAndUnwrap(MethodInfo method, object target, object[] args)
        {
            try
            {
                return method.Invoke(target, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                // Unwrap so callers see (and can assert against) the real exception the
                // production method threw, not reflection's wrapper.
                throw ex.InnerException;
            }
        }
    }
}
