using System.Text.Json;
using Microsoft.Azure.Cosmos.Fluent;

namespace BMSD.Accessors.UserInfo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddHealthChecks();
            builder.Services.AddControllers().AddDapr(config =>
            {
                config.UseJsonSerializationOptions(new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }).AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });

            //get the cosmos db connection string from the configuration
            var cosmosDbConnectionString = builder.Configuration["CosmosDbConnectionString"];

            //check that cosmosDbConnectionString is not null
            if (string.IsNullOrWhiteSpace(cosmosDbConnectionString))
            {
                throw new Exception("Error in configuration: CosmosDbConnectionString is null or empty");
            }

            //Create Cosmos db client using cosmos client builder and camel case serializer
            //Important Security Note: To use CosmosDB emulator we ignore certification checks!!!
            var cosmosClient = new CosmosClientBuilder(cosmosDbConnectionString)
                .WithHttpClientFactory(() =>
                {
                    HttpMessageHandler httpMessageHandler = new HttpClientHandler()
                    {
                        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                    };
                    return new HttpClient(httpMessageHandler);
                })
                .WithConnectionModeGateway()
                .WithCustomSerializer(new CosmosSystemTextJsonSerializer(
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    })).Build();

            //add the cosmos client to the dependency injection container
            builder.Services.AddSingleton(cosmosClient);

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
    }
}