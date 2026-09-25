using Caslu.IA.Domain.Entities;

namespace Caslu.IA.Application.Abstractions;

/// <summary>
/// Acesso às conversas e mensagens (camada 2 da memória).
/// Todos os métodos exigem o username do dono como primeiro parâmetro.
/// </summary>
public interface IConversationRepository
{
    /// <summary>
    /// Cria uma conversa nova para o usuário.
    /// </summary>
    /// <param name="username">Username normalizado do dono da conversa.</param>
    /// <param name="title">Título da conversa.</param>
    /// <param name="createdAt">Data de criação (UTC), normalmente o momento em que a requisição chegou; também é o <see cref="Conversation.UpdatedAt"/> inicial.</param>
    /// <param name="cancellationToken">Token para cancelar a operação.</param>
    /// <returns>A conversa criada, com o identificador preenchido.</returns>
    Task<Conversation> CreateAsync(string username, string title, DateTime createdAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca uma conversa do usuário.
    /// </summary>
    /// <param name="username">Username normalizado do dono da conversa.</param>
    /// <param name="conversationId">Identificador da conversa.</param>
    /// <param name="cancellationToken">Token para cancelar a operação.</param>
    /// <returns>A conversa, ou <c>null</c> se não existir para esse usuário.</returns>
    Task<Conversation?> GetAsync(string username, string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adiciona várias mensagens a uma conversa do usuário numa única operação.
    /// O username e a conversa de cada mensagem gravada são sempre os informados nos parâmetros.
    /// </summary>
    /// <param name="username">Username normalizado do dono da conversa.</param>
    /// <param name="conversationId">Identificador da conversa.</param>
    /// <param name="messages">Mensagens a gravar (papel, texto e data de criação), na ordem em que ocorreram.</param>
    /// <param name="cancellationToken">Token para cancelar a operação.</param>
    /// <returns>Uma tarefa que representa a operação.</returns>
    Task AddMessagesAsync(string username, string conversationId, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca as últimas mensagens de uma conversa do usuário.
    /// </summary>
    /// <param name="username">Username normalizado do dono da conversa.</param>
    /// <param name="conversationId">Identificador da conversa.</param>
    /// <param name="limit">Quantidade máxima de mensagens.</param>
    /// <param name="cancellationToken">Token para cancelar a operação.</param>
    /// <returns>As últimas <paramref name="limit"/> mensagens, em ordem cronológica.</returns>
    Task<IReadOnlyList<ChatMessage>> GetLastMessagesAsync(string username, string conversationId, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza a data da última atividade (<see cref="Conversation.UpdatedAt"/>) de uma conversa do usuário.
    /// </summary>
    /// <param name="username">Username normalizado do dono da conversa.</param>
    /// <param name="conversationId">Identificador da conversa.</param>
    /// <param name="cancellationToken">Token para cancelar a operação.</param>
    /// <returns>Uma tarefa que representa a operação.</returns>
    Task TouchAsync(string username, string conversationId, CancellationToken cancellationToken = default);
}
