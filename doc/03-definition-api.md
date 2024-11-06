# Definition API

## Methods with custom data types

This example shows how an API method can return an object of any type, as long as it is serializable. The API Server uses JSON serialization.

```csharp
Method.Define("helloworld")
    .HandleGet(request => new DataObject {Message = "Hello world!"}));

// ...

// This class defines the type of data object that will be sent to the client.
public class DataObject
{
    public string Message { get; set; }
}
```

This example shows how an API method can accept input data of any serializable type. The API server expects a JSON document in the request body and will deserialize it to an object of the specified type.

``` csharp
Method.Define("helloworld/post")
    .Handle<PostedMessage, DataObject>("POST",
    (posted, qr) => new DataObject
    {
        Message = "You posted the following message: " + posted.Input
    });

// ...

// This class defines the type of data object that will be sent from the client to the server.
public class PostedMessage
{
    public string Input { get; set; }
}
```

This is an example of a method that generates plain text (not JSON formatted). You can use this approach to generate content of any type by returning an appropriate `HttpResponseMessage`.

``` csharp

Method.Define("helloworld/text")
    .HandleGet(new ContentTypeSelector
    {
        // Specify the MIME type of the response.
        new ResponseBuilder("text/plain", async (qr, ct) =>
        {
            return new System.Net.Http.HttpResponseMessage
            {
                Content = new System.Net.Http.StringContent("Hello world!")
            };
        })
    }, typeof(string));
```

## Module attribution

Associating API methods with an Identity Manager module is important for correct API client generation, as API clients are defined by module.

You can logically assign API methods to a module with a code attribute:

```csharp
[assembly: QBM.CompositionApi.PlugIns.Module("CCC")]
```

This attribute will automatically assign all methods defined in this DLL to the `CCC` (customer) module of Identity Manager.

## Request validation

```csharp
public void Build(IApiBuilder builder)
{
	builder.AddMethod(Method.Define("testvalidation")
        .WithParameter("p", description: "Parameter must be 'a' or 'b'")
		.WithValidator("Parameter validation", request =>
		{
			var value = request.Parameters.Get<string>("p");
			if (value != "a" && value != "b")
				return new ValidationError("Invalid value for parameter");
			return null;
		})
		.HandleGet(request => "Hello world!")
    );
}
```

## Defining multiple routes on an API method

This example shows how to build a method with two GET handlers on different routes using the `WithRoute` method. This is useful if the handlers share some logic (i.e. validation or parameterization).

```csharp
Method.Define("parentroute")
    // Define a GET handler for the /parentroute endpoint
    .HandleGet(request => "This is a request to /parentroute")
    // Add a subroute for the /parentroute/subroute endpoint
    .WithRoute("/subroute")
    // Add a query parameter to the subroute. This parameter exists on the subroute only!
    .WithParameter(new RequestParameter("parameter")
    {
        Type = typeof(string),
        IsInQuery = true
    })
    .HandleGet(request => "This is a request to /parentroute/childroute. The parameter value is: " +
        request.Parameters.Get<string>("parameter"))
);
```


## Returning binary data from a method

This method shows how to render binary data, such as images, through an API handler with automatic detection of the image type.

```csharp
Method.Define("personimage/{uid}")
    .WithParameter("uid", typeof(string), "Unique person identifier", isInQuery: false)
    .HandleGetBinaryData(
        "Person", // Database table
        "JPegPhoto", // Database column
        "uid", // Name of the URL parameter
        // You could enforce a MIME type here. Use null to let the API server guess the image type
        null
    ));
```


## Working with web sockets

Configure this method to use a web socket. The client can make a request to this method using a `ws://` or `wss://` URL. Requests to a WebSocket method will follow the usual lifecycle (including authentication and parameter validation), and then transfer control of the request to the provided `ISocketAdapter` implementation.

```csharp
Method.Define("socketdemo")
    .HandleSocket(request => new SocketAdapter(request)));

// ...
private class SocketAdapter : ISocketAdapter
{
    // This is the object that provides access to:
    // - Socket: the underlying socket, which you can use to send data to the client
    // - Request: the API request including access to the Identity Manager session
    private readonly ISocketRequest _request;
    public SocketAdapter(ISocketRequest request)
    {
        _request = request;
    }

    public async Task OnMessageReceivedAsync(ArraySegment<byte> data, WebSocketMessageType type,
        CancellationToken ct = new CancellationToken())
    {
        // TODO: handle the message received from the client
    }

    public void OnReceiveError(Exception exc)
    {
        // TODO: an error has occurred.
    }

    public async Task OnCloseAsync(WebSocketCloseStatus? status, string description)
    {
        // TODO: handle the closing of the socket.
    }
}
```

## SQL method definition

The following example shows how to define a SQL entity method. Methods of this type support execution of a predefined SQL statement.

Note that the result schema columns and data types have to be explicitly defined. The names of the columns have to match exactly the names of the SQL result set. 

```csharp
Method.Define("sql/changecount")
    // Insert the statement name (QBMLimitedSQL.Ident_QBMLimitedSQL)
    .HandleGetBySqlStatement("SystemConfig_ChangeCount", SqlStatementType.SqlExecute)
    .WithResultColumns(
        new SqlResultColumn("TableName", ValType.String),
        new SqlResultColumn("Count", ValType.Int)
    )
```

You can also pass SQL parameters to the statement by modifying the `HandleGetBySqlStatement` call. The following example maps the URL parameter `urlparameter` to the SQL parameter `sqlparameter`.

``` csharp
.WithParameter("urlparameter", typeof(string), "The parameter value for the SQL statement", isInQuery: false)
.HandleGetBySqlStatement(request =>
{
    return new[]
    {
        new SqlStatementRun
        {
            StatementName = "MyStatementName",
            Parameters = new[]
            {
                new QueryParameter("sqlparameter", ValType.String,
                    request.Parameters.Get<string>("urlparameter"))
            }
        }
    };
}, SqlStatementType.SqlExecute)
``` 

## Modifying a SQL method

Call the `ModifyStatementMethod` method to modify a SQL statement method.

```csharp
builder.ModifyStatementMethod(
    // This is the URL of the method to be modified.
    "sql/changecount",
    method =>
    {
        // Simply include one more property in the result.
        method.WithCalculatedProperties(new CalculatedProperty("TableDisplay", ValType.String,
            // Statement to calculate the property value
            async (context, ct) =>
            {
                // Get the (technical) table name
                var tablename = context.Entity.GetValue("TableName").String;
                // Obtain the table definition
                var metaTable = await context.Session.MetaData()
                    .GetTableAsync(tablename, ct).ConfigureAwait(false);
                // Return the translated table display name
                return new EntityColumnData(metaTable.Display.Translated);
            }));
    });
```

## Exceptions and HTTP status codes

By default, the API Server does not forward exception messages to the client, using only a generic message ("An error has occurred.") instead. Exception details are hidden from the client on the assumption that they may contain sensitive data.

To generate an exception whose message will be forwarded to the client, use the `ViException` type and set the `ExceptionRelevance`:

``` csharp
throw new ViException("An error occurred.", ExceptionRelevance.EndUser);
```

This also applies to exceptions thrown in script code.

To return a specific HTTP status code to the client, throw an `HttpException`:

```csharp
Method.Define("test")
   .HandleGet<object>((r, ct) =>
   throw new HttpException((int) HttpStatusCode.NotImplemented, "This is a test."));
```

## Integrating with ASP.NET Core

You can directly integrate with ASP.NET Core using the plugin model. Add a class implementing the `IBuilderPlugIn` interface to run code using the `IApplicationBuilder` object.

```csharp
public class MyPlugIn : IBuilderPlugIn
{
    public void Start(IBuilderPlugInContext context)
    {
        // Get the ASP.NET Core IServiceCollection
        
        var services = context.Services;

        // TODO: Integrate the required services and handlers here.

        context.RegisterCallback(pc =>
        {
            // This code will be executed after the ASP.NET application has been built.
        }
    }
}
```

Add a class implementing the `IPlugIn` interface to run code using after the application has been built.

This plugin class registers a new server-level middleware that registers the `/example` URL and returns the HTTP status code 418 with an empty response.

``` csharp
public class Middleware : IPlugIn
{
    public void Start(IPluginContext context)
    {
        var app = context.ApplicationBuilder;
        app.Map("/example", builder => builder.Use(async (c, next) =>
        {
            c.Response.StatusCode = 418;

            await next(c).ConfigureAwait(false);
        }));
    }


    public int OrderNumber => 0;
}
``` 

## Global method processing event

``` csharp
.Configure(m =>
{
    m.Settings.ProcessingRequest.Subscribe(async request =>
    {
        // handle request here
    });
    return m;
})
```


## Disambiguation of method paths

API Server creates a *method identifier* for every API method. This method identifier is used to build the OpenAPI documentation and the API clients.

When defining two methods with similar URLs, sometimes the same method identifier will be created for two methods.

For example, these two method URLs map have the same identifier ("app_simplemethod_get"):

  - `GET app/simplemethod`
  - `GET app/simplemethod/{id}`

Defining methods like this will result in an error. To solve this issue, set the `ClientSuffix` property of one of the methods to make the method identifiers unique:

```csharp
Method.Define("simplemethod/{id}")
    .With(m => m.Settings.ClientSuffix = "_byid")
    .HandleGet(request => "Method M/{id}");
```

The method identifier for this method will now be `app_simplemethod_byid_get`.

## API configuration

API configuration is designed to be used:

- when configuration settings correspond to a .NET object structure and can be strongly-typed
- when configuration settings need to be exposed in the Server Administration app
- when the set of configuration settings is dynamic and may change at runtime.

This class shows an example of exposing configuration settings of your API. The API configuration service manages configuration objects, and you can add your own configuration objects.

```csharp
    public void Build(IApiBuilder builder)
    {
        var configService = builder.Resolver.Resolve<IConfigService>();
        var configurableObject = new ConfigurableObject();

        // This call registers the object instance with the configuration
        // service. This has the following effects:
        // 1. The object's configurable properties will be exposed to
        // the external configurator.
        // 2. Changes made through external configuration will be directly
        // applied to the configurable object.
        configService.RegisterConfigurableObject(configurableObject);
    }

    public class ConfigurableObject
    {
        [DisplayName("A string property.")]
        public string StringProperty { get; set; }

        [DisplayName("A numeric property.")]
        public int IntegerSetting { get; set; } = 42;
    }
```

## Configuring HTTP headers

Use the `IHttpHeaderConfig` service to define global HTTP headers.

``` csharp
var httpHeaders = builder.Resolver.Resolve<IHttpHeaderConfig>();
// Add a new HTTP header - this sample shows a constant value
httpHeaders.Headers["my-header"] = context => "my-header-value";
```

## Adding a KPI/chart

KPI charts and heatmaps are added to the system by registering them with the `IChartService`. The statistics definition must exist in the database. The job of the `IChartService` is to manage the available charts and organize them into areas.

```csharp
var chartService = builder.Resolver.Resolve<IChartService>();
var chartArea = new ChartArea(new TranslatableKey("Custom statistics"));
chartService.Add(new ChartInfo("Name-of-my-dashboard-def")
{
    Area = chartArea
});
```

Note that the type of chart currently cannot be set in the definition. The UI chooses the best-suited chart type at runtime based on the data.

## Adding a heatmap

Heatmaps also rely on a statistics definition in the database. They support an additional statistics references for monthly and yearly value comparison.

```csharp
var heatmapService = builder.Resolver.Resolve<IChartService>();
heatmapService.Add(new HeatmapInfo(new ChartInfo("Name-of-the-dashboarddef")
    {
        Area = chartArea
    },
    new ChartInfo("name-of-the-yearly-dashboarddef"),
    new ChartInfo("name-of-the-monthly-dashboarddef"))
);
```

## Defining methods without authentication

You can call the method `AllowUnauthenticated` to define that this method does not need authentication.

```csharp
Method.Define("somemethod")
   .AllowUnauthenticated()
```

Note that the `IRequest.Session` property will be `null` for these methods.

**Be careful when using `AllowUnauthenticated`**. Do not expose any sensitive data to callers of these APIs. It is preferable to use alternative methods to authenticate the caller.

## Assigning API method descriptions

Assigning descriptions on API methods is recommended. Descriptions are included when generating OpenAPI documents and when compiling TypeScript API clients.

You can set a description on the method, which will automatically be applied to all its routes.

```csharp
// Assign a description to the method.
.WithDescription("Returns identity main data.")
```

The following description is used to document the `PUT` route associated with updating an identity.

```csharp
.With(update => update.Description = "Updates the main data of an identity.")
```

## File system access

The API method class `FileStorageMethod` provides access to the file system and can be used to build dedicated API methods for different scenarios that require file system access.

The `FileStorageMethod` requires a defined root folder.

``` csharp
builder.AddMethod(new FileStorageMethod("fileaccess", new FileSystemStorage(request =>
{
    return "<path to root folder>";
}))
{
    // set to true if the method should only allow read access
    IsReadOnly = false
});
```

This definition creates several endpoints on the `/file` route, allowing a client to:
 - download a file (`GET fileaccess/file/path/file.txt`)
 - upload a file (`POST fileaccess/file/path/file.txt`)
 - delete a file (`DELETE fileaccess/file/path/file.txt`)

Directory access is possible through the /directory route:
 - list contents of a directory (`GET fileaccess/directory/path`)
 - delete a directory (`DELETE fileaccess/directory/path`)

Note that the file system access is made using the credentials of the user account running the API Server, *not* the user who is logged into the API project on the client.

## Access control

The following example shows how to restrict access to an API project. The `CheckFeaturePlugin` checks for a specific program function at login time. If the user does not have this program function, the login attempt is rejected.

```csharp
public void Build(IApiBuilder builder)
{
    // This example uses the "ApiServer_Admin" function.
	builder.AddPlugin(new CheckFeaturePlugin("ApiServer_Admin"));
}
```
