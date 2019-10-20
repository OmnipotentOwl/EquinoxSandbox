# EquinoxSandbox
Sandbox for Miscellaneous Equinox/Propulsion experimentation in mostly C#.

Current Goal: C# sample of Equinox todo with Propulsion projector and consumer for read model.

## Setup
1. *export 3x env vars*

    ```powershell
    $env:EQUINOX_COSMOS_CONNECTION="AccountEndpoint=https://....;AccountKey=....=;"
    $env:EQUINOX_COSMOS_DATABASE="equinox-test"
    $env:EQUINOX_COSMOS_CONTAINER="equinox-test"
    ```

2. use the `eqx` tool to initialize the database and/or container (using preceding env vars)

    ```powershell
    dotnet tool uninstall Equinox.Tool -g
    dotnet tool install Equinox.Tool -g --version 2.0.0-rc*
    eqx init -ru 400 cosmos # generates a database+container, adds optimized indexes
    ```
3. Use `propulsion` tool to run a CosmosDb ChangeFeedProcessor

    ```powershell
    dotnet tool uninstall Propulsion.Tool -g
    dotnet tool install Propulsion.Tool -g

    propulsion init -ru 400 cosmos # generates a -aux container for the ChangeFeedProcessor to maintain consumer group progress within
    # -v for verbose ChangeFeedProcessor logging
    # `projector1` represents the consumer group - >=1 are allowed, allowing multiple independent projections to run concurrently
    # stats specifies one only wants stats regarding items (other options include `kafka` to project to Kafka)
    # cosmos specifies source overrides (using defaults in step 1 in this instance)
    ```

## Testing

1) Start CosmosDB Emulator
2) Open WebDemo Solution
3) Update appsettings.json files in Web and CSharpProjector if required to match Setup env vars.
4) Start Web Project
5) Start CSharpProjector Project
6) The generated code includes a CORS whitelisting for https://todobackend.com. _Cors configuration should be considered holistically in the overall design of an app - Equinox itself has no requirement of any specific configuration; you should ensure appropriate care and attention is paid to this aspect of securiting your application as normal_.

7) Run the API compliance test suite (can be useful to isolate issues if the application is experiencing internal errors):

       start https://www.todobackend.com/specs/index.html?https://localhost:5001/todos
    
8) Once you've confirmed that the backend is listening and fulfulling the API obligations, you can run the frontend app:

       # Interactive UI; NB error handling is pretty minimal, so hitting refresh and/or F12 is recommended ;)
       start https://www.todobackend.com/client/index.html?https://localhost:5001/todos