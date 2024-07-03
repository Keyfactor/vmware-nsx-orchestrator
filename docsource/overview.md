## Overview

The VMware NSX Advanced Load Balancer (Avi) Universal Orchestrator extension enables the remote management of cryptographic certificates on VMware NSX ALB instances. This integration facilitates automated certificate operations such as inventory, adding, removing, and discovering certificates within Keyfactor Command.

VMware NSX Advanced Load Balancer uses certificates for SSL offloading and secure communication. The types of certificates managed include Application and System certificates, which require private keys, and CA certificates, which are used for building and validating certificate chains without needing private keys.

In this integration, Certificate Stores represent specific NSX ALB instances (and optionally tenants) associated with particular certificate types. Each Certificate Store configuration allows administrators to manage certificates on a per-instance and per-certificate type basis, streamlining certificate management tasks across multiple NSX ALB deployments.

