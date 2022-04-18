# Avi Vantage

The Avi Vantage Orchestrator allows for the management of certificates stored in the Avi Vantage ADC solution. Application, System, and CA cert types are supported. Inventory, Management, and Renewal functions are supported.

#### Integration status: Production - Ready for use in production environments.

## About the Keyfactor Windows Orchestrator AnyAgent

This repository contains a Windows Orchestrator AnyAgent, which is a plugin to the Keyfactor Windows Orchestrator. Within the Keyfactor Platform, Orchestrators are used to manage “certificate stores” $mdash; collections of certificates and roots of trust that are found within and used by various applications.

The Windows Orchestrator is part of the Keyfactor software distribution and is available via the Keyfactor customer portal. For general instructions on installing AnyAgents, see the “Keyfactor Command Orchestrator Installation and Configuration Guide” section of the Keyfactor documentation. For configuration details of this specific AnyAgent, see below in this readme.

Note that in Keyfactor Version 9, the Windows Orchestrator have been replaced by the Universal Orchestrator. While this AnyAgent continues to work with the Windows Orchestrator, and the Windows Orchestrator is supported alongside the Universal Orchestrator talking to Keyfactor version 9, AnyAgent plugins cannot be used with the Universal Orchestrator.

---

﻿*** 

# Introduction 
The AVI certificate store type is set up so that each Cert Store points to a specific Avi Vantage instance and certificate type.
For multiple certificate types on the same Avi Vantage instance, create a certificate store for each type to manage.

Application and System certs are used by Avi for SSL offloading and require private keys. CA certs are used to
build and validate certificate chains and do not require private keys.

# Setting up AVI Cert Store Type
Short Name: `AVI`
Needs Server: `true`
Custom Alias: `required`
Store Path Type: `Multiple Choice`
Store Path Value: `'Application, Controller, CA'`
Private Keys: `Optional`
Job Types: `Add, Remove`

# Supported Functionality
- Inventory, Management, Renewal.
- Agent can manage CA certificates present on Avi Vantage.
- Certificates (with private keys required) can also replaced in-place on the Avi Vantage platform.

# Not Implemented/Supported
- Discovery


# Notes
- Required aliases act as the name for the certificate in the AVI Vantage system. These are also used to renew/replace and delete existing certificates.
- While Private Keys are optional, they _are required_ for Application or Controller type certificates.

 ***

### License
[Apache](https://apache.org/licenses/LICENSE-2.0)
