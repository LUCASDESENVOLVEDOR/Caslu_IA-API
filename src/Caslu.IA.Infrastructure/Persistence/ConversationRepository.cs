using Caslu.IA.Application.Abstractions;
using Caslu.IA.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Caslu.IA.Infrastructure.Persistence;

/// <summary>
/// Repositório de conversas e mensagens nas collections "conversations" e "messages".
/// </summary>
public sealed class ConversationRepository : IConversationRepository
{
    private readonly IMongoCollection<Conversation> _conversations;
    private readonly IMongoCollection<ChatMessage> _messages;

    /// <summary>
    /// Cria o repositório de conversas.
    /// </summary>
    /// <param name="database">Banco de dados da aplicação.</param>
    public ConversationRepository(IMongoDatabase database)
    {
        _conversations = database.GetCollection<Conversation>(MongoCollections.Conversations);
        _messages = database.GetCollection<ChatMessage>(MongoCollections.Messages);
    }

    /// <inheritdoc />
    public async Task<Conversation> CreateAsync(string username, string title, DateTime createdAt, CancellationToken cancellationToken = default)
    {
        EnsureUsername(username);

        var conversation = new Conversation
        {
            Username = username,
            Title = title,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        await _conversations.InsertOneAsync(conversation, cancellationToken: cancellationToken);
        return conversation;
    }

    /// <inheritdoc />
    public async Task<Conversation?> GetAsync(string username, string conversationId, CancellationToken cancellationToken = default)
    {
        var filter = ForUser(username, Builders<Conversation>.Filter.Eq(c => c.Id, conversationId));

        // Id em formato inválido não existe para ninguém; evita erro de conversão no driver.
        if (!ObjectId.TryParse(conversationId, out _))
        {
            return null;
        }

        return await _conversations.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Usa um único <c>InsertMany</c>. Inserções não têm filtro: o isolamento vem do username do parâmetro,
    /// gravado em cada documento (o username e a conversa recebidos nas mensagens são ignorados).
    /// </remarks>
    public async Task AddMessagesAsync(string username, string conversationId, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        EnsureUsername(username);

        if (messages.Count == 0)
        {
            return;
        }

        var documents = messages.Select(m => new ChatMessage
        {
            Username = username,
            ConversationId = conversationId,
            Role = m.Role,
            Content = m.Content,
            CreatedAt = m.CreatedAt
        }).ToList();

        await _messages.InsertManyAsync(documents, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessage>> GetLastMessagesAsync(string username, string conversationId, int limit, CancellationToken cancellationToken = default)
    {
        var filter = ForUser(username, Builders<ChatMessage>.Filter.Eq(m => m.ConversationId, conversationId));

        var latest = await _messages.Find(filter)
            .SortByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        latest.Reverse();
        return latest;
    }

    /// <inheritdoc />
    public async Task TouchAsync(string username, string conversationId, CancellationToken cancellationToken = default)
    {
        var filter = ForUser(username, Builders<Conversation>.Filter.Eq(c => c.Id, conversationId));
        var update = Builders<Conversation>.Update.Set(c => c.UpdatedAt, DateTime.UtcNow);

        await _conversations.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Único ponto de montagem de filtros deste repositório: sempre restringe ao username.
    /// </summary>
    /// <typeparam name="T">Tipo do documento (<see cref="Conversation"/> ou <see cref="ChatMessage"/>).</typeparam>
    /// <param name="username">Username do dono dos dados.</param>
    /// <param name="filter">Filtro adicional, combinado com o do username.</param>
    /// <returns>O filtro restrito ao usuário.</returns>
    /// <exception cref="ArgumentException">Username vazio.</exception>
    private static FilterDefinition<T> ForUser<T>(string username, FilterDefinition<T>? filter = null)
    {
        EnsureUsername(username);

        var byUser = Builders<T>.Filter.Eq(nameof(Conversation.Username), username);
        return filter is null ? byUser : Builders<T>.Filter.And(byUser, filter);
    }

    /// <summary>
    /// Garante que o username foi informado.
    /// </summary>
    /// <param name="username">Username do dono dos dados.</param>
    /// <exception cref="ArgumentException">Username vazio.</exception>
    private static void EnsureUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("O username é obrigatório.", nameof(username));
        }
    }
}
