# Identity Manager API Plugin

This folder contains a C# project template for an API plugin.

This C# project targets Identity Manager 9.3 and .NET 8.

## Using the C# sample project

The project references various assemblies that are part of an Identity Manager installation. To enable compilation, set the ReferencePath in the `ApiPlugIn.csproj` file to the Identity Manager installation path:

``` xml
<ReferencePath>c:\Program Files\One Identity</ReferencePath>
```

## Working with the debugger

You can start an API Server from your IDE, including any plugins that you want to debug. To start the API Server, use the command line `imxclient.exe run-apiserver -B /baseurl *baseurl*`.

After launch, the API server will be running on http://localhost:8182, and it will only accept connections from localhost. Press the ENTER key to stop the server.

If you cannot start the local API Server, your user account may not have sufficient permissions to listen on the specified HTTP port. In this case, use the `netsh` tool to add permissions. For example:

`netsh http add urlacl url=http://localhost:8182/ user=<domain\user>`

See https://docs.microsoft.com/en-us/windows/win32/http/add-urlacl for more information.
