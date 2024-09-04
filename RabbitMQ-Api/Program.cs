using Microsoft.OpenApi.Models;
using RabbitMq;
using RabbitMQ_Api.Consumer;

var builder = WebApplication.CreateBuilder(args);

// Adicionando serviços ao contêiner
builder.Services.AddControllers();

// Configuração do RabbitMQ e injeção de dependências
builder.Services.AddRabbitMq("localhost", "/", 5672, "guest", "guest", false)
    .AddSingleton<PublisherConsumer>();
 
builder.Services.AddHostedService<Consumer>();

// Configuração do Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "My API",
        Version = "v1",
        Description = "API documentation for RabbitMQ integration example",
        Contact = new OpenApiContact
        {
            Name = "Support Team",
            Email = "support@example.com",
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

// Configuração do middleware
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

// Configuração dos endpoints
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

// Rodando a aplicação
app.Run();
