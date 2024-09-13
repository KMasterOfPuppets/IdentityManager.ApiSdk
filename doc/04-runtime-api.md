# Runtime API

This document covers the request-handling part of the API model, and provides samples for common use cases. 

## Accessing the current HTTP request

To obtain the current request context, you can use a static property. The request context is provided through the asynchronous call context.

```csharp
var context = RequestScopeContext.Current;
```

The context object provides access to the ASP.NET request. You can also resolve the services object to obtain the API Server's `IRequest` object like this:

```csharp
var request = RequestScopeContext.Current.GetServices().Resolve<IRequest>();
```

## Calling a customizer method

You can call customizer methods by loading an interactive entity and using the `GetMethod` method. Note that customizer methods can only be called for interactive entities.

```csharp
Method.Define("customizermethod")
    .HandleGet(async qr =>
    {
        var person = await qr.Session.Source().GetAsync(new DbObjectKey("Person", qr.Session.User().Uid),
                EntityLoadType.Interactive)
            .ConfigureAwait(false);
        // Load the GetCulture method. This one does not take any parameters.
        var method = person.GetMethod(qr.Session, "GetCulture", Array.Empty<object>());
        // Call the method and return the result (in this case, it's a string).
        var result = await method.CallAsync(qr.Session, person).ConfigureAwait(false);
        return result;
    });
```

Currently it is not possible to combine customizer method calls with the entity API.

## Calling a script

This method takes two string parameters and returns a string. For demonstration purposes, the method simply calls the script `VI_BuildInitials`.

```csharp
Method.Define("initials/{firstname}/{lastname}")
    .WithParameter("firstname", typeof(string), isInQuery: false)
    .WithParameter("lastname", typeof(string), isInQuery: false)
    .HandleGet(qr =>
    {
        // Setup the script runner
        var scriptClass = qr.Session.Scripts().GetScriptClass(ScriptContext.Scripts);
        var runner = new ScriptRunner(scriptClass, qr.Session);

        // Add any script input parameters to this array.
        // In this example, the script parameters are defined as
        // URL parameters, and their values must be supplied
        // by the client. This does not have to be the case.
        var parameters = new object[]
        {
            qr.Parameters.Get<string>("firstname"),
            qr.Parameters.Get<string>("lastname")
        };

        // This assumes that the script returns a string.
        return runner.Eval("VI_BuildInitials", parameters) as string;
    });
```

## Rendering a report

The following example shows an API to render the report `VI_Attestation_Person_overview`.

``` csharp
Method.Define("report")
    .HandleReport(req =>
    {
        var parameters = new System.Collections.Generic.Dictionary<string, object>
        {
            // Use the authenticated user's key as a parameter value.
            ["ObjectKeyBase"] = new DbObjectKey("Person", req.Session.User().Uid).ToXmlString(),
            ["IncludeSubIdentities"] = false
            //  Additional parameters could be defined here.
        };

        return new ReportGeneration
        {
            ReportName = "VI_Attestation_Person_overview",
            Parameters = parameters
        };
    });
```

The reporting API supports different target formats, such as PDF. The client can set the HTTP `Accept` header to define the requested format.

## Logging

The following code shows how to generate log messages during request processing. It is a good practice to use the session-specific logger to make it easier to correlate log messages to user sessions.

```csharp
Method.Define("logging")
    .WithParameter("param", typeof(string))
    .HandleGet(request =>
    {
        var param = request.Parameters.Get<string>("param");
        if (string.IsNullOrEmpty(param))
            param = "(no value)";

        var sessionLog = request.Session.GetLogSession();

        sessionLog.Info("This is a session-specific log message. The client sent: " + param);
        // Return a result to the client
        return "You sent the parameter: " + param;
    });
```

## Changing an entity request at runtime

You can change the parameters of an entity request at runtime. For example, you can add filters or change the sorting.

``` csharp
Method.Define("person")
   .FromTable("Person")
   .EnableRead()
   .SubscribeProcessing((request, ct) => PrepareRequestAsync(request, ct))

// ...

private static async Task PrepareRequestAsync(IRequest request, CancellationToken ct)
{
    // Get the request's entity configuration
    var env = request.GetEntityCollectionRequest();

    // add a filter clause
    env.FilterClauses.Add(new WhereClause("IsInactive = 0"));
}
```

You can also define an entity method that is not bound to an object type. In this case, you have to set the object type at runtime.

``` csharp
Method.Define("generic")
   .FromTable() // this is an unbound entity method
   .EnableRead()
   .SubscribeProcessing((request, ct) => PrepareRequestAsync(request, ct))

// ...

private static async Task PrepareRequestAsync(IRequest request, CancellationToken ct)
{
    // Get the request's entity configuration
    var env = request.GetEntityCollectionRequest();

    // set the Person table as the data source
    env.Table = new MetaTableDescriptor(request.Session.MetaData().GetTable("Person"));
}
```


## Managing session state

The API Server implements its own session state management. (ASP.NET Core session management is not used.)

A *session group* consists of independent Identity Manager sessions for each API project. It is identified by the value of the `imx_sessiongroup` cookie. It is important to understand that a user may have authenticated sessions for 0, 1 or more API projects within a session group. For example, a session group can have the following state:

|API project|Session|
|---|---|
|`portal`|Authenticated as user A|
|`passwordreset`|Not authenticated|
|`opsupport`|Authenticated as user B|

To set the lifetime of the cookie, change the value of the `QBM\ApiServer\Defaults\SameSiteCookie` configuration parameter.

The value of the cookie corresponds to a `SessionGuid` entry in the `QBMSessionStore` table. For security reasons, the value of the cookie is changed after each successful authentication.

You can read and write data to the session store using the `IServerSession` interface. You can store objects of any serializable types.

```csharp
private async Task AccessSession(IRequest request)
{
   var sessionState = request.SessionStatus;
   // write some values to the session store
   sessionState.SetData("my-key", "value");
   // CommitAsync writes the data to the database
   await sessionState.CommitAsync().ConfigureAwait(false);
   // read the data
   var data = sessionState.GetData("my-key");
}
```

Note that the session state store is not designed to handle large amounts of data. Writing larger objects can lead to performance degradation.
