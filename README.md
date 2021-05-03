# Introduction 
The AVI certificate store type is set up so that each Cert Store points to a specific Avi Vantage instance and certificate type.
For multiple certificate types on the same Avi Vantage instance, create a certificate store for each type to manage.

# Setting up AVI Cert Store Type
Short Name: AVI
Needs Server: true
Custom Alias: required
Store Path Type: Multiple Choice
Store Path Value: 'Application, Controller, CA'
Private Keys: Optional
Job Types: Add, Remove

# Notes
- Required aliases act as the name for the certificate in the Avi Vantage system. These are also used to renew/replace and delete existing certificates.
- While Private Keys are optional, they should _are required_ for Application or Controller type certificates.