using AutoMapper;
using System.Text.Json;

namespace BMSD.Managers.Account
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
            
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            try
            {
                var mapperConfig = new MapperConfiguration(mc => { mc.AddProfile(new AutoMappingProfile()); });
                IMapper mapper = mapperConfig.CreateMapper();
                mapperConfig.AssertConfigurationIsValid();
                builder.Services.AddSingleton(mapper);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw;
            }

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            
            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.UseCloudEvents();

            app.MapHealthChecks("/healthz");
            
            app.MapControllers();
            app.MapSubscribeHandler();

            app.Run();
        }
    }
}