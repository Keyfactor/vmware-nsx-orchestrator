
### VMware-NSX Store Type
#### kfutil Create VMware-NSX Store Type
The following commands can be used with [kfutil](https://github.com/Keyfactor/kfutil). Please refer to the kfutil documentation for more information on how to use the tool to interact w/ Keyfactor Command.

```
bash
kfutil login
kfutil store - types create--name VMware-NSX 
```

#### UI Configuration
##### UI Basic Tab
| Field Name              | Required | Value                                     |
|-------------------------|----------|-------------------------------------------|
| Name                    | &check;  | VMware-NSX                          |
| ShortName               | &check;  | VMware-NSX                          |
| Custom Capability       |          | Unchecked [ ]                             |
| Supported Job Types     | &check;  | Inventory,Add,Remove     |
| Needs Server            | &check;  | Checked [x]                         |
| Blueprint Allowed       |          | Unchecked [ ]                       |
| Uses PowerShell         |          | Unchecked [ ]                             |
| Requires Store Password |          | Unchecked [ ]                          |
| Supports Entry Password |          | Unchecked [ ]                         |
      
![vmware-nsx_basic.png](docs%2Fscreenshots%2Fstore_types%2Fvmware-nsx_basic.png)

##### UI Advanced Tab
| Field Name            | Required | Value                 |
|-----------------------|----------|-----------------------|
| Store Path Type       |          | ["Application","Controller","CA"]      |
| Supports Custom Alias |          | Required |
| Private Key Handling  |          | Optional  |
| PFX Password Style    |          | Default   |

![vmware-nsx_advanced.png](docs%2Fscreenshots%2Fstore_types%2Fvmware-nsx_advanced.png)

##### UI Custom Fields Tab
| Name           | Display Name         | Type   | Required | Default Value |
| -------------- | -------------------- | ------ | -------- | ------------- |
|ServerUsername|Server Username|Secret|null|true|
|ServerPassword|Server Password|Secret|null|true|
|ServerUseSsl|Use SSL|Bool|true|true|
|ApiVersion|X-Avi-Version|String|20.1.1|true|


**Entry Parameters:**

Entry parameters are inventoried and maintained for each entry within a certificate store.
They are typically used to support binding of a certificate to a resource.

|Name|Display Name| Type|Default Value|Required When |
|----|------------|-----|-------------|--------------|

