using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Microsoft.Extensions.Configuration;

namespace WebDemo.Web
{
    /// <summary>Defines the Hosting configuration, including registration of the store and backend services</summary>
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
            else
                app.UseHsts();

            app.UseHttpsRedirection()
                // NB Jet does now own, control or audit https://todobackend.com; it is a third party site; please satisfy yourself that this is a safe thing use in your environment before using it._
                .UseCors(x => x.WithOrigins("https://www.todobackend.com").AllowAnyHeader().AllowAnyMethod())
                .UseMvc();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)

        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
            var equinoxContext = ConfigureStore();
            ConfigureServices(services, equinoxContext);
        }

        static void ConfigureServices(IServiceCollection services, EquinoxContext context)
        {
            services.AddSingleton(_ => context);
            services.AddSingleton(sp => new ServiceBuilder(context, Serilog.Log.ForContext<EquinoxContext>()));
            services.AddSingleton(sp => sp.GetRequiredService<ServiceBuilder>().CreateTodoService());
            services.AddSingleton(sp => sp.GetRequiredService<ServiceBuilder>().CreateAggregateService());
        }

        EquinoxContext ConfigureStore()
        {
            // This is the allocation limit passed internally to a System.Caching.MemoryCache instance
            // The primary objects held in the cache are the Folded State of Event-sourced aggregates
            // see https://docs.microsoft.com/en-us/dotnet/framework/performance/caching-in-net-framework-applications for more information
            var cacheMb = 50;

            // AZURE COSMOSDB: Events are stored in an Azure CosmosDb Account (using the SQL API)
            // Provisioning Steps:
            // 1) Set the 3x environment variables EQUINOX_COSMOS_CONNECTION, EQUINOX_COSMOS_DATABASE, EQUINOX_COSMOS_CONTAINER
            // 2) Provision a container using the following command sequence:
            //     dotnet tool install -g Equinox.Cli
            //     Equinox.Cli init -ru 1000 cosmos -s $env:EQUINOX_COSMOS_CONNECTION -d $env:EQUINOX_COSMOS_DATABASE -c $env:EQUINOX_COSMOS_CONTAINER
            const string connVar = "EQUINOX_COSMOS_CONNECTION";
            var conn = Configuration.GetValue<string>(connVar);
            const string dbVar = "EQUINOX_COSMOS_DATABASE";
            var db = Configuration.GetValue<string>(dbVar);
            const string containerVar = "EQUINOX_COSMOS_CONTAINER";
            var container = Configuration.GetValue<string>(containerVar);
            if (conn == null || db == null || container == null)
                throw new Exception(
                    $"Event Storage subsystem requires the following Environment Variables to be specified: {connVar} {dbVar}, {containerVar}");
            var connMode = Equinox.Cosmos.ConnectionMode.Direct;
            var config = new CosmosConfig(connMode, conn, db, container, cacheMb);
            return new CosmosContext(config);
        }
    }

    /// Binds a storage independent Service's Handler's `resolve` function to a given Stream Policy using the StreamResolver
    internal class ServiceBuilder
    {
        readonly EquinoxContext _context;
        readonly ILogger _handlerLog;

        public ServiceBuilder(EquinoxContext context, ILogger handlerLog)
        {
            _context = context;
            _handlerLog = handlerLog;
        }

        public Todo.Service CreateTodoService() =>
            new Todo.Service(
                _handlerLog,
                _context.Resolve(
                    EquinoxCodec.Create(Todo.Event.Encode, Todo.Event.TryDecode),
                    Todo.State.Fold,
                    Todo.State.Initial,
                    Todo.State.IsOrigin,
                    Todo.State.Compact));
        public Aggregate.Service CreateAggregateService() =>
            new Aggregate.Service(
                _handlerLog,
                _context.Resolve(
                    EquinoxCodec.Create(Aggregate.Event.Encode, Aggregate.Event.TryDecode),
                    Aggregate.State.Fold,
                    Aggregate.State.Initial,
                    Aggregate.State.IsOrigin,
                    Aggregate.State.Compact));
    }
}