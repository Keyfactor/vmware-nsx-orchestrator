2.1.3
- Added code to mitigate problem with older versions of the UO framework throwing an exception when bag attributes are included in the PEM data.
- Inventory jobs will now continue if an individual cert is badly formatted and log the details.

2.1.2
- correctly format certs sent in Management job to prevent Inventory errors later
- no longer setting Inventory status to "Unknown"
- catch and log errors that occur during Login process
- move documentation to doctool
- build net6.0 and net8.0 targeting binaries

2.1.1
- Fix CSRF token usage for token auth

2.1.0
- Add PAM support for Server Username and Password
- Allow X-Avi-Version to be set on each Certificate Store to allow for use with older API Versions
- Page Inventory results to allow for larger Inventory results
- Use Session Authentication instead of Basic Authentication

2.0.1
- Fixes a bug in the renewal overwrite process that would fail after overwriting the existing certificate by attempting to add the certificate again

2.0.0
- Initial release of the Universal Orchestrator capability
- Replaces the original Windows Orchestrator capability in `avi-vantage-windowsorchestrator`

