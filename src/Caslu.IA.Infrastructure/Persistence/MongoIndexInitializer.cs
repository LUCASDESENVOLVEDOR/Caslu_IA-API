using Caslu.IA.Domain.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Caslu.IA.Infrastructure.Persistence;

/// <summary>
/// Cria os índices das collections do chat na inicialização da aplicação.
/// A criação é idempotente: índices já existentes são mantidos.
/// </summary>
public sealed class MongoIndexInitializer : IHostedService
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<MongoIndexInitializer> _logger;

    /// <summary>
    /// Cria o inicializador de índices.
    /// </summary>
    /// <param name="database">Banco de dados da aplicação.</param>
    /// <param name="logger">Logger para registrar falhas na criação dos índices.</param>
    public MongoIndexInitializer(IMongoDatabase database, ILogger<MongoIndexInitializer> logger)
    {
        _database = database;
        _logger = logger;
    }

    /// <summary>
    /// Cria os índices de "conversations" e "messages". A collection "users" usa o próprio <c>_id</c>.
    /// Falhas são registradas em log sem impedir a subida da API (o /health indica o problema).
    /// </summary>
    /// <param name="cancellationToken">Token para cancelar a inicialização.</param>
    /// <returns>Uma tarefa que representa a operação.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var conversations = _database.GetCollection<Conversation>(MongoCollections.Conversations);
            await conversations.Indexes.CreateOneAsync(
                new CreateIndexModel<Conversation>(
                    Builders<Conversation>.IndexKeys
                        .Ascending(c => c.Username)
                        .Descending(c => c.UpdatedAt)),
                cancellationToken: cancellationToken);

            var messages = _database.GetCollection<ChatMessage>(MongoCollections.Messages);
            await messages.Indexes.CreateOneAsync(
                new CreateIndexModel<ChatMessage>(
                    Builders<ChatMessage>.IndexKeys
                        .Ascending(m => m.Username)
                        .Ascending(m => m.ConversationId)
                        .Ascending(m => m.CreatedAt)),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Falha ao criar os índices do MongoDB.");
        }
    }

    /// <summary>
    /// Não há nada a finalizar.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelar a finalização.</param>
    /// <returns>Uma tarefa concluída.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
