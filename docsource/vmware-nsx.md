## Overview

The VMware-NSX Certificate Store Type in Keyfactor Command allows for the streamlined management of SSL/TLS certificates on VMware NSX Advanced Load Balancer (Avi) instances. This store type is configured to point to a specific NSX ALB instance and, optionally, a specific tenant and certificate type, facilitating granular control over certificates for different functionalities such as SSL offloading and certificate chain validation.

The Certificate Store Type represents a target NSX ALB environment, helping administrators to handle certificates for Application, System, and CA types uniquely. Application and System certificates require private keys, whereas CA certificates do not.

### Caveats and Considerations

- **User Permissions:** The user configured for the orchestrator must be either a Tenant Admin or Security Admin for single-tenancy management or a System Admin for multi-tenancy management.
- **Trust Requirements:** The SSL/TLS certificate listed under Administration -> Settings -> Access Settings on the NSX ALB platform needs to be trusted by the orchestrator to ensure secure HTTPS communication.
- **Version Targeting:** A custom field can be added to set the X-Avi-Version to target specific NSX ALB versions, providing flexibility in managing environments running different software versions.

### SDK and Limitations

While the documentation does not specify the usage of an SDK, the integration relies on NSX ALB's API for interacting with and managing certificates. There are no significant limitations or areas of confusion mentioned, but administrators should ensure they configure the correct user permissions and SSL/TLS trust settings for seamless operation.

## Requirements

### VMware NSX ALB Configuration
The NSX ALB platform needs some configuration in order to allow the Orchestrator to communicate with it.
The listed SSL/TLS certificate under Administration -> Settings -> Access Settings  needs to be trusted by the Orchestrator so that HTTPS can be used successfully.

A user also needs to be set up with a password that can be used to authenticate during Orchestrator requests. This user should be a Tenant Admin or Security Admin on the tenant that will be managed.
If a user should be used for multiple tenants, they will need to be a system admin. The tenant that they are initially assigned to be will be considered the "default" tenant if no tenant is specified for the certificate store.

### VMware NSX ALB Orchestrator Extension Configuration
**1. Create the New Certificate Store Type for the NSX orchestrator extension**

The easiest way to create the Certificate Store Type is to use the `kfutil` tool to automatically install the Store Type definition. However, you can manually add it with the information below.
In Keyfactor Command create a new Certificate Store Type similar to the one below by clicking Settings (the gear icon in the top right) => Certificate Store Types => Add:

![](images/store-type-basic.png)
![](images/store-type-advanced.png)

You will also need to add the following Custom Field if you want to be able to set the X-Avi-Version to target a version other than 20.1.1. 

![](images/store-type-avi-version.png)

**2. Create a new NSX Certificate Store**

After the Certificate Store Type has been configured, a new NSX Certificate Store can be created.
When creating the store, if a tenant other than the API user's default tenant should be used, the Client Machine should be preface with [tenant] in brackets.

| Certificate Store parameter | Input | Alternative Input |
|-|-|-|
| Client Machine | [optional-tenant-name]https://my.nsx.url/ | https://my.nsx.url/ |
| Store Path | Application | CA (or Controller) |
| X-Avi-Version | 20.1.1 (default value) | 18.2.9 |

**3. Adding or Replacing (Renewing) Certificates**
The required alias acts as the name for the certificate in the VMware NSX ALB system. These are also used to renew/replace and delete existing certificates.
When adding a certificate, selecting `Overwrite` and entering the same name (alias) as an existing certificate will replace that certificate, allowing for renewals of existing certificates.

Additionally, while private keys are optional for CA type certificates, they _are required_ for Application or Controller type certificates.

