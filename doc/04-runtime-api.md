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

The `IRequest` interface provides a property `Context` that gives access to the underlying ASP.NET `HttpContext`. [See the official ASP.NET Core documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.httpcontext?view=aspnetcore-8.0).

## Accessing the request body

If the method was defined with an input type, the request body is provided as a object of that type.

``` csharp
Method.Define("helloworld")
    .Handle<PostedMessage>("POST",
    (message, request) =>
    {
        // message is of type PostedMessage
    });
```

You can also deserialize the JSON object to any type at runtime.

``` csharp
Method.Define("helloworld")
    .Handle("POST", async (request, ct) =>
    {
        var message = await request.GetContentAsync<PostedMessage>(ct).ConfigureAwait(false);
    });
```

## Accessing URL parameters

To access the value of a URL parameter, you can use the `Parameters` property of the request. The following example uses a `string`, although
other types and JSON-deserializable types are also possible.

> [!NOTE]
> Treat URL input parameters as unsafe. Whenever you process the value of a URL parameter in a sensitive context, such as building a SQL WHERE clause, use the `SqlFormatter` object to ensure that the value is properly escaped.

``` csharp
Method.Define("tags/{UID_AccProduct}")
    .WithParameter("UID_AccProduct", typeof(string), "Unique service item identifier", isInQuery: false)
    .WithDescription("Obtains the tags associated with a service item.")
    .FromTable("DialogTag")
    .EnableRead()
    .WithWhereClause(async (request, ct) =>
    {
        // Get the value of the UID_AccProduct URL parameter
        var uidaccproduct = request.Parameters.Get<string>("UID_AccProduct");

        // Use the value of the parameter in a SQL WHERE clause, using the SqlFormatter
        // for safe escaping of the value.
        return string.Format(
            "UID_DialogTag in (select uid_dialogtag from dialogtaggeditem where {0})",
            request.Session.SqlFormatter().UidComparison("ObjectKey",
                new DbObjectKey("AccProduct", uidaccproduct).ToXmlString()));
    })
```

## Accessing the Identity Manager connection

You can access the current user's session object through the `IRequest.Session` property.

``` csharp
Method.Define("helloworld")
    .HandleGet(request => "Hello, you are: " + request.Session.User().Display)
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


## Managing session state

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
