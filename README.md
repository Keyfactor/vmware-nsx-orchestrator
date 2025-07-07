<h1 align="center" style="border-bottom: none">
    VMware NSX Advanced Load Balancer (Avi) Universal Orchestrator Extension
</h1>

<p align="center">
  <!-- Badges -->
<img src="https://img.shields.io/badge/integration_status-production-3D1973?style=flat-square" alt="Integration Status: production" />
<a href="https://github.com/Keyfactor/vmware-nsx-orchestrator/releases"><img src="https://img.shields.io/github/v/release/Keyfactor/vmware-nsx-orchestrator?style=flat-square" alt="Release" /></a>
<img src="https://img.shields.io/github/issues/Keyfactor/vmware-nsx-orchestrator?style=flat-square" alt="Issues" />
<img src="https://img.shields.io/github/downloads/Keyfactor/vmware-nsx-orchestrator/total?style=flat-square&label=downloads&color=28B905" alt="GitHub Downloads (all assets, all releases)" />
</p>

<p align="center">
  <!-- TOC -->
  <a href="#support">
    <b>Support</b>
  </a>
  ·
  <a href="#installation">
    <b>Installation</b>
  </a>
  ·
  <a href="#license">
    <b>License</b>
  </a>
  ·
  <a href="https://github.com/orgs/Keyfactor/repositories?q=orchestrator">
    <b>Related Integrations</b>
  </a>
</p>

## Overview

The VMware NSX ALB certificate store type is set up so that each Certificate Store points to a specific NSX ALB instance (and optionally a specific tenant) and certificate type.
It is able to Inventory and Manage Application, System, and CA certificates.

Application and System certs are used by NSX ALB for SSL offloading and require private keys. CA certs are used to build and validate certificate chains and do not require private keys.



## Compatibility

This integration is compatible with Keyfactor Universal Orchestrator version 10.1 and later.

## Support
The VMware NSX Advanced Load Balancer (Avi) Universal Orchestrator extension If you have a support issue, please open a support ticket by either contacting your Keyfactor representative or via the Keyfactor Support Portal at https://support.keyfactor.com.

> To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.

## Requirements & Prerequisites

Before installing the VMware NSX Advanced Load Balancer (Avi) Universal Orchestrator extension, we recommend that you install [kfutil](https://github.com/Keyfactor/kfutil). Kfutil is a command-line tool that simplifies the process of creating store types, installing extensions, and instantiating certificate stores in Keyfactor Command.


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


## VMware-NSX Certificate Store Type

To use the VMware NSX Advanced Load Balancer (Avi) Universal Orchestrator extension, you **must** create the VMware-NSX Certificate Store Type. This only needs to happen _once_ per Keyfactor Command instance.








#### Supported Operations

| Operation    | Is Supported                                                                                                           |
|--------------|------------------------------------------------------------------------------------------------------------------------|
| Add          | ✅ Checked        |
| Remove       | ✅ Checked     |
| Discovery    | 🔲 Unchecked  |
| Reenrollment | 🔲 Unchecked |
| Create       | 🔲 Unchecked     |

#### Store Type Creation

##### Using kfutil:
`kfutil` is a custom CLI for the Keyfactor Command API and can be used to create certificate store types.
For more information on [kfutil](https://github.com/Keyfactor/kfutil) check out the [docs](https://github.com/Keyfactor/kfutil?tab=readme-ov-file#quickstart)
   <details><summary>Click to expand VMware-NSX kfutil details</summary>

   ##### Using online definition from GitHub:
   This will reach out to GitHub and pull the latest store-type definition
   ```shell
   # VMware-NSX
   kfutil store-types create VMware-NSX
   ```

   ##### Offline creation using integration-manifest file:
   If required, it is possible to create store types from the [integration-manifest.json](./integration-manifest.json) included in this repo.
   You would first download the [integration-manifest.json](./integration-manifest.json) and then run the following command
   in your offline environment.
   ```shell
   kfutil store-types create --from-file integration-manifest.json
   ```
   </details>


#### Manual Creation
Below are instructions on how to create the VMware-NSX store type manually in
the Keyfactor Command Portal
   <details><summary>Click to expand manual VMware-NSX details</summary>

   Create a store type called `VMware-NSX` with the attributes in the tables below:

   ##### Basic Tab
   | Attribute | Value | Description |
   | --------- | ----- | ----- |
   | Name | VMware-NSX | Display name for the store type (may be customized) |
   | Short Name | VMware-NSX | Short display name for the store type |
   | Capability | VMware-NSX | Store type name orchestrator will register with. Check the box to allow entry of value |
   | Supports Add | ✅ Checked | Check the box. Indicates that the Store Type supports Management Add |
   | Supports Remove | ✅ Checked | Check the box. Indicates that the Store Type supports Management Remove |
   | Supports Discovery | 🔲 Unchecked |  Indicates that the Store Type supports Discovery |
   | Supports Reenrollment | 🔲 Unchecked |  Indicates that the Store Type supports Reenrollment |
   | Supports Create | 🔲 Unchecked |  Indicates that the Store Type supports store creation |
   | Needs Server | ✅ Checked | Determines if a target server name is required when creating store |
   | Blueprint Allowed | 🔲 Unchecked | Determines if store type may be included in an Orchestrator blueprint |
   | Uses PowerShell | 🔲 Unchecked | Determines if underlying implementation is PowerShell |
   | Requires Store Password | 🔲 Unchecked | Enables users to optionally specify a store password when defining a Certificate Store. |
   | Supports Entry Password | 🔲 Unchecked | Determines if an individual entry within a store can have a password. |

   The Basic tab should look like this:

   ![VMware-NSX Basic Tab](docsource/images/VMware-NSX-basic-store-type-dialog.png)

   ##### Advanced Tab
   | Attribute | Value | Description |
   | --------- | ----- | ----- |
   | Supports Custom Alias | Required | Determines if an individual entry within a store can have a custom Alias. |
   | Private Key Handling | Optional | This determines if Keyfactor can send the private key associated with a certificate to the store. Required because IIS certificates without private keys would be invalid. |
   | PFX Password Style | Default | 'Default' - PFX password is randomly generated, 'Custom' - PFX password may be specified when the enrollment job is created (Requires the Allow Custom Password application setting to be enabled.) |

   The Advanced tab should look like this:

   ![VMware-NSX Advanced Tab](docsource/images/VMware-NSX-advanced-store-type-dialog.png)

   > For Keyfactor **Command versions 24.4 and later**, a Certificate Format dropdown is available with PFX and PEM options. Ensure that **PFX** is selected, as this determines the format of new and renewed certificates sent to the Orchestrator during a Management job. Currently, all Keyfactor-supported Orchestrator extensions support only PFX.

   ##### Custom Fields Tab
   Custom fields operate at the certificate store level and are used to control how the orchestrator connects to the remote target server containing the certificate store to be managed. The following custom fields should be added to the store type:

   | Name | Display Name | Description | Type | Default Value/Options | Required |
   | ---- | ------------ | ---- | --------------------- | -------- | ----------- |
   | ServerUsername | Server Username | The username of the user to log on as in VMware NSX ALB. | Secret |  | ✅ Checked |
   | ServerPassword | Server Password | The password of the user to log on as in VMware NSX ALB. | Secret |  | ✅ Checked |
   | ApiVersion | X-Avi-Version | The API Version of Avi / NSX to target. A default is set for the version this was originally developed and tested against. | String | 20.1.1 | ✅ Checked |

   The Custom Fields tab should look like this:

   ![VMware-NSX Custom Fields Tab](docsource/images/VMware-NSX-custom-fields-store-type-dialog.png)

   </details>

## Installation

1. **Download the latest VMware NSX Advanced Load Balancer (Avi) Universal Orchestrator extension from GitHub.**

    Navigate to the [VMware NSX Advanced Load Balancer (Avi) Universal Orchestrator extension GitHub version page](https://github.com/Keyfactor/vmware-nsx-orchestrator/releases/latest). Refer to the compatibility matrix below to determine whether the `net6.0` or `net8.0` asset should be downloaded. Then, click the corresponding asset to download the zip archive.

   | Universal Orchestrator Version | Latest .NET version installed on the Universal Orchestrator server | `rollForward` condition in `Orchestrator.runtimeconfig.json` | `vmware-nsx-orchestrator` .NET version to download |
   | --------- | ----------- | ----------- | ----------- |
   | Older than `11.0.0` | | | `net6.0` |
   | Between `11.0.0` and `11.5.1` (inclusive) | `net6.0` | | `net6.0` |
   | Between `11.0.0` and `11.5.1` (inclusive) | `net8.0` | `Disable` | `net6.0` |
   | Between `11.0.0` and `11.5.1` (inclusive) | `net8.0` | `LatestMajor` | `net8.0` |
   | `11.6` _and_ newer | `net8.0` | | `net8.0` |

    Unzip the archive containing extension assemblies to a known location.

    > **Note** If you don't see an asset with a corresponding .NET version, you should always assume that it was compiled for `net6.0`.

2. **Locate the Universal Orchestrator extensions directory.**

    * **Default on Windows** - `C:\Program Files\Keyfactor\Keyfactor Orchestrator\extensions`
    * **Default on Linux** - `/opt/keyfactor/orchestrator/extensions`

3. **Create a new directory for the VMware NSX Advanced Load Balancer (Avi) Universal Orchestrator extension inside the extensions directory.**

    Create a new directory called `vmware-nsx-orchestrator`.
    > The directory name does not need to match any names used elsewhere; it just has to be unique within the extensions directory.

4. **Copy the contents of the downloaded and unzipped assemblies from __step 2__ to the `vmware-nsx-orchestrator` directory.**

5. **Restart the Universal Orchestrator service.**

    Refer to [Starting/Restarting the Universal Orchestrator service](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/StarttheService.htm).


6. **(optional) PAM Integration**

    The VMware NSX Advanced Load Balancer (Avi) Universal Orchestrator extension is compatible with all supported Keyfactor PAM extensions to resolve PAM-eligible secrets. PAM extensions running on Universal Orchestrators enable secure retrieval of secrets from a connected PAM provider.

    To configure a PAM provider, [reference the Keyfactor Integration Catalog](https://keyfactor.github.io/integrations-catalog/content/pam) to select an extension and follow the associated instructions to install it on the Universal Orchestrator (remote).


> The above installation steps can be supplemented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/CustomExtensions.htm?Highlight=extensions).



## Defining Certificate Stores



### Store Creation

#### Manually with the Command UI

<details><summary>Click to expand details</summary>

1. **Navigate to the _Certificate Stores_ page in Keyfactor Command.**

    Log into Keyfactor Command, toggle the _Locations_ dropdown, and click _Certificate Stores_.

2. **Add a Certificate Store.**

    Click the Add button to add a new Certificate Store. Use the table below to populate the **Attributes** in the **Add** form.

   | Attribute | Description                                             |
   | --------- |---------------------------------------------------------|
   | Category | Select "VMware-NSX" or the customized certificate store name from the previous step. |
   | Container | Optional container to associate certificate store with. |
   | Client Machine | This is the URL for the VMware NSX instance. It also includes an optional tenant in square brackets before the URL. A tenant value is required when the certificates being managed are in a different tenant from the default tenant set for the NSX User specified for the store. This should look like either: [optional-tenant-name]https://my.nsx.url/ OR https://my.nsx.url/  |
   | Store Path | A selection from the different certificate types supported: Application, Controller, or CA. |
   | Orchestrator | Select an approved orchestrator capable of managing `VMware-NSX` certificates. Specifically, one with the `VMware-NSX` capability. |
   | ServerUsername | The username of the user to log on as in VMware NSX ALB. |
   | ServerPassword | The password of the user to log on as in VMware NSX ALB. |
   | ApiVersion | The API Version of Avi / NSX to target. A default is set for the version this was originally developed and tested against. |

</details>



#### Using kfutil CLI

<details><summary>Click to expand details</summary>

1. **Generate a CSV template for the VMware-NSX certificate store**

    ```shell
    kfutil stores import generate-template --store-type-name VMware-NSX --outpath VMware-NSX.csv
    ```
2. **Populate the generated CSV file**

    Open the CSV file, and reference the table below to populate parameters for each **Attribute**.

   | Attribute | Description |
   | --------- | ----------- |
   | Category | Select "VMware-NSX" or the customized certificate store name from the previous step. |
   | Container | Optional container to associate certificate store with. |
   | Client Machine | This is the URL for the VMware NSX instance. It also includes an optional tenant in square brackets before the URL. A tenant value is required when the certificates being managed are in a different tenant from the default tenant set for the NSX User specified for the store. This should look like either: [optional-tenant-name]https://my.nsx.url/ OR https://my.nsx.url/  |
   | Store Path | A selection from the different certificate types supported: Application, Controller, or CA. |
   | Orchestrator | Select an approved orchestrator capable of managing `VMware-NSX` certificates. Specifically, one with the `VMware-NSX` capability. |
   | Properties.ServerUsername | The username of the user to log on as in VMware NSX ALB. |
   | Properties.ServerPassword | The password of the user to log on as in VMware NSX ALB. |
   | Properties.ApiVersion | The API Version of Avi / NSX to target. A default is set for the version this was originally developed and tested against. |

3. **Import the CSV file to create the certificate stores**

    ```shell
    kfutil stores import csv --store-type-name VMware-NSX --file VMware-NSX.csv
    ```

</details>


#### PAM Provider Eligible Fields
<details><summary>Attributes eligible for retrieval by a PAM Provider on the Universal Orchestrator</summary>

If a PAM provider was installed _on the Universal Orchestrator_ in the [Installation](#Installation) section, the following parameters can be configured for retrieval _on the Universal Orchestrator_.

   | Attribute | Description |
   | --------- | ----------- |
   | ServerUsername | The username of the user to log on as in VMware NSX ALB. |
   | ServerPassword | The password of the user to log on as in VMware NSX ALB. |

Please refer to the **Universal Orchestrator (remote)** usage section ([PAM providers on the Keyfactor Integration Catalog](https://keyfactor.github.io/integrations-catalog/content/pam)) for your selected PAM provider for instructions on how to load attributes orchestrator-side.
> Any secret can be rendered by a PAM provider _installed on the Keyfactor Command server_. The above parameters are specific to attributes that can be fetched by an installed PAM provider running on the Universal Orchestrator server itself.

</details>


> The content in this section can be supplemented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/ReferenceGuide/Certificate%20Stores.htm?Highlight=certificate%20store).





## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor Universal Orchestrator extensions](https://github.com/orgs/Keyfactor/repositories?q=orchestrator).