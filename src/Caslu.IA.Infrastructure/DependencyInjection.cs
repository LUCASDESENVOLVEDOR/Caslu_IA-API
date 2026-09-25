using Caslu.IA.Infrastructure.Configuration;
using Caslu.IA.Infrastructure.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Caslu.IA.Infrastructure;

/// <summary>
/// Métodos de extensão para registrar os serviços da camada de infraestrutura.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra as opções, os serviços de infraestrutura (MongoDB e Ollama) e os health checks no contêiner de dependências.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    /// <returns>A mesma coleção de serviços, permitindo encadeamento.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MongoDbOptions>()
            .Bind(configuration.GetSection(MongoDbOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "MongoDb:ConnectionString é obrigatório.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DatabaseName),
                "MongoDb:DatabaseName é obrigatório.")
            .ValidateOnStart();

        services.AddOptions<OllamaOptions>()
            .Bind(configuration.GetSection(OllamaOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.BaseUrl),
                "Ollama:BaseUrl é obrigatório.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Model),
                "Ollama:Model é obrigatório.")
            .Validate(options => options.TimeoutSeconds > 0,
                "Ollama:TimeoutSeconds deve ser maior que zero.")
            .ValidateOnStart();

        services.AddSingleton<IMongoClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;
            return new MongoClient(options.ConnectionString);
        });

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MongoDbOptions>>().Value;
            return sp.GetRequiredService<IMongoClient>().GetDatabase(options.DatabaseName);
        });

        services.AddHttpClient<OllamaHealthCheck>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddHealthChecks()
            .AddCheck<MongoHealthCheck>("mongodb", tags: ["db"])
            .AddCheck<OllamaHealthCheck>("llm", tags: ["llm"]);

        return services;
    }
}
