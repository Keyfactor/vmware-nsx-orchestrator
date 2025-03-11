## Overview
The VMware NSX ALB certificate store type is set up so that each Certificate Store points to a specific NSX ALB instance (and optionally a specific tenant) and certificate type.
It is able to Inventory and Manage Application, System, and CA certificates.

Application and System certs are used by NSX ALB for SSL offloading and require private keys. CA certs are used to build and validate certificate chains and do not require private keys.

## Requirements
The NSX ALB platform needs some configuration in order to allow the Orchestrator to communicate with it.
The listed SSL/TLS certificate under Administration -> Settings -> Access Settings  needs to be trusted by the Orchestrator so that HTTPS can be used successfully.

A user also needs to be set up with a password that can be used to authenticate during Orchestrator requests. This user should be a Tenant Admin or Security Admin on the tenant that will be managed.
If a user should be used for multiple tenants, they will need to be a system admin. The tenant that they are initially assigned to be will be considered the "default" tenant if no tenant is specified for the certificate store.

When creating the store, if a tenant other than the API user's default tenant should be used, the Client Machine should be prefaced with [tenant] in brackets. If the [tenant] is not specified, you may not see the certificates you expect to see if they are in a different tenant.
For multiple certificate types on the same NSX instance, create a certificate store for each type to manage. If certificates under multiple tenants need to be managed, a seperate certificate store for each tenant should be created.

Example:
| Certs to target | Client Machine | Store Path |
| - | - | - |
| CA certs in user's default configured tenant | https://my.nsx.url/ | CA |
| Application certs in tenant "Operations" | [Operations]https://my.nsx.url/ | Application |
| System certs in tenant "IT" | [IT]https://my.nsx.url/ | System |

The required alias acts as the name for the certificate in the VMware NSX ALB system. These are also used to renew/replace and delete existing certificates.
When adding a certificate, selecting `Overwrite` and entering the same name (alias) as an existing certificate will replace that certificate, allowing for renewals of existing certificates.

Additionally, while private keys are optional for CA type certificates, they _are required_ for Application or Controller type certificates.

