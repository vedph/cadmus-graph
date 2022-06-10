using Cadmus.Graph.MySql;
using Cadmus.Index.Sql;

namespace Cadmus.Graph.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddScoped<IGraphRepository>(provider =>
            {
                string cs = provider.GetService<IConfiguration>()
                    .GetConnectionString("Default");
                var repository = new MySqlGraphRepository();
                repository.Configure(new SqlOptions
                {
                    ConnectionString = cs
                });
                return repository;
            });

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

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}