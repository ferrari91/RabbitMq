using Microsoft.OpenApi.Models;
using RabbitMq;
using RabbitMQ_Api.Consumer;
using RabbitMQ_Api.Model;
using RabbitMQ_Api.Publisher;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Adicionando servi�os ao cont�iner
builder.Services.AddControllers();

// Configura��o do RabbitMQ e inje��o de depend�ncias
builder.Services.AddRabbitMq("localhost", "/", 5672, "guest", "guest", false)
    .AddSingleton<IMyModelPublisher<MyModel>, MyModelPublisher>();
 
builder.Services.AddHostedService<MyModelConsumer>();

// Configura��o do Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RabbitMQ - API",
        Version = "v1",
        Description = "API documentation for RabbitMQ integration example",
        Contact = new OpenApiContact
        {
            Name = "Fernando Ferrari",
            Email = "ferrari.cf91@outlook.com",
        }
    });
});

builder.Services.AddOptions();

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = long.MaxValue;
});

builder.Services.AddCors();

var app = builder.Build();

// Configura��o do middleware
app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();
app.UseAuthentication();

app.UseCors(options => options.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

// Habilitando o Swagger e a interface Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
    c.RoutePrefix = string.Empty; // Isso faz com que a UI do Swagger seja carregada na URL raiz
});

// Configura��o dos endpoints
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

// Rodando a aplica��o
app.Run();
