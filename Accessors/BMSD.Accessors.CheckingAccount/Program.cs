using System.Text.Json;
using BMSD.Accessors.CheckingAccount.DB;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Logging;

namespace BMSD.Accessors.CheckingAccount
{
    public class Program
    {
        const string DatabaseName = "BMSDB";
        
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddHealthChecks();
            builder.Services.AddControllers().AddDapr().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });

            using var loggerFactory = LoggerFactory.Create(loggingBuilder => loggingBuilder
                   .SetMinimumLevel(LogLevel.Trace)
                   .AddConsole());
            
            AddCosmosService(builder.Services, builder.Configuration, loggerFactory);

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.MapHealthChecks("/healthz");

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }


        public static void AddCosmosService(IServiceCollection services, IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            //get the cosmos db connection string from the configuration
            var cosmosDbConnectionString = configuration["CosmosDbConnectionString"];

            //Create Cosmos db client using cosmos client builder and camel case serializer
            //Important Security Note: To use CosmosDB emulator we ignore certification checks!!!
            var cosmosClient = new CosmosClientBuilder(cosmosDbConnectionString)
                .WithSerializerOptions(new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                })
                .WithHttpClientFactory(() =>
                {
                    HttpMessageHandler httpMessageHandler = new HttpClientHandler()
                    {
                        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                    };
                    return new HttpClient(httpMessageHandler);
                })
                .WithConnectionModeGateway()
                .Build();

            //get logger from services
            var logger = loggerFactory.CreateLogger<CosmosDBWrapper>();

            var cosmosDBWrapper = new CosmosDBWrapper(cosmosClient, DatabaseName, logger);

            services.AddSingleton<ICosmosDBWrapper>(cosmosDBWrapper);
        }
    }
}