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

namespace Keyfactor.AnyAgent.AviVantage.Models
{
    public static class Constants
    {
        public static class SSLCertificate
        {
            public static class Format
            {
                public const string PEM = "SSL_PEM";
                public const string PKCS12 = "SSL_PKCS12";
            }

            public static class Type
            {
                public const string VIRTUAL_SERVICE = "SSL_CERTIFICATE_TYPE_VIRTUALSERVICE"; // Application
                public const string SYSTEM = "SSL_CERTIFICATE_TYPE_SYSTEM"; // Controller
                public const string CA = "SSL_CERTIFICATE_TYPE_CA";

                public static string GetType(string certificateStoreType)
                {
                    switch (certificateStoreType)
                    {
                        case "CA":
                            return CA;
                        case "Controller":
                            return SYSTEM;
                        case "Application":
                        default:
                            return VIRTUAL_SERVICE;
                    }
                }
            }

            public static class Status
            {
                public const string FINISHED = "SSL_CERTIFICATE_FINISHED";
            }
        }

        public static class SSLKey
        {
            public static class Algorithms
            {
                public const string RSA = "SSL_KEY_ALGORITHM_RSA";
                public const string EC = "SSL_KEY_ALGORITHM_EC";
            }

            public static class ECParams
            {
                public const string CURVE_SECP256 = "SSL_KEY_EC_CURVE_SECP256R1";
                public const string CURVE_SECP384 = "SSL_KEY_EC_CURVE_SECP384R1";
                public const string CURVE_SECP521 = "SSL_KEY_EC_CURVE_SECP521R1";
            }

            public static class RSAParams
            {
                public const string SIZE_1024 = "SSL_KEY_1024_BITS";
                public const string SIZE_2048 = "SSL_KEY_2048_BITS";
                public const string SIZE_3072 = "SSL_KEY_3072_BITS";
                public const string SIZE_4096 = "SSL_KEY_4096_BITS";
            }
        }
    }
}
