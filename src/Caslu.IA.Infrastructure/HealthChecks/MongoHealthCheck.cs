using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Caslu.IA.Infrastructure.HealthChecks;

/// <summary>
/// Verifica a disponibilidade do MongoDB executando o comando <c>{ ping: 1 }</c>.
/// </summary>
public sealed class MongoHealthCheck : IHealthCheck
{
    private readonly IMongoDatabase _database;

    /// <summary>
    /// Cria o health check do MongoDB.
    /// </summary>
    /// <param name="database">Banco de dados da aplicação, usado para enviar o comando de ping.</param>
    public MongoHealthCheck(IMongoDatabase database)
    {
        _database = database;
    }

    /// <summary>
    /// Executa o ping no MongoDB e informa se o banco está respondendo.
    /// </summary>
    /// <param name="context">Contexto da execução do health check.</param>
    /// <param name="cancellationToken">Token para cancelar a verificação.</param>
    /// <returns>
    /// <see cref="HealthCheckResult.Healthy"/> se o MongoDB responder; caso contrário,
    /// <see cref="HealthCheckResult.Unhealthy"/> com a mensagem do erro.
    /// </returns>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1),
                cancellationToken: cancellationToken);

            return HealthCheckResult.Healthy("MongoDB respondeu ao ping.");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // A exceção não é anexada ao resultado para não expor detalhes de conexão.
            return HealthCheckResult.Unhealthy($"MongoDB indisponível: {ex.Message}");
        }
    }
}
