using Google.Api;
using System.Text.Json;

namespace BMSD.Managers.Notification
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddHealthChecks();
            builder.Services.AddControllers().AddDapr().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });
            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddSingleton<SignalRService>()
                .AddHostedService(sp => sp.GetService<SignalRService>())
                .AddSingleton<IHubContextStore>(sp => sp.GetService<SignalRService>()!);
              
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