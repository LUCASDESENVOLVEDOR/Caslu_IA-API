using System.Text;
using Caslu.IA.Application.Abstractions;
using Caslu.IA.Domain.Entities;

namespace Caslu.IA.Application.Chat;

/// <summary>
/// Orquestra uma troca de mensagens do chat: resolve a conversa, monta em memória o contexto
/// enviado à LLM e só grava perfil, conversa e mensagens depois que a LLM responde
/// (resposta completa ou em streaming).
/// </summary>
public sealed class ChatService
{
    private const int TitleMaxLength = 50;

    private readonly IUserProfileRepository _profiles;
    private readonly IConversationRepository _conversations;
    private readonly ILlmClient _llm;
    private readonly ChatOptions _options;

    /// <summary>
    /// Cria o serviço de chat.
    /// </summary>
    /// <param name="profiles">Repositório de perfis globais (camada 1).</param>
    /// <param name="conversations">Repositório de conversas e mensagens (camada 2).</param>
    /// <param name="llm">Cliente da LLM.</param>
    /// <param name="options">Opções do chat.</param>
    public ChatService(IUserProfileRepository profiles, IConversationRepository conversations, ILlmClient llm, ChatOptions options)
    {
        _profiles = profiles;
        _conversations = conversations;
        _llm = llm;
        _options = options;
    }

    /// <summary>
    /// Envia uma mensagem do usuário e devolve a resposta da LLM.
    /// O contexto é montado em memória; se a LLM não responder, nada é gravado
    /// (nem perfil, nem conversa, nem mensagens).
    /// </summary>
    /// <param name="username">Username do usuário (será normalizado).</param>
    /// <param name="conversationId">Conversa existente (espaços nas pontas são removidos); <c>null</c>, vazio ou só com espaços inicia uma conversa nova.</param>
    /// <param name="message">Texto enviado pelo usuário; os espaços nas pontas são removidos antes de validar e gravar.</param>
    /// <param name="cancellationToken">Token para cancelar a operação.</param>
    /// <returns>
    /// O resultado com o identificador da conversa e a resposta, ou <see cref="ChatResult.NotFound"/>
    /// se a conversa informada não existir para esse usuário.
    /// </returns>
    /// <exception cref="ArgumentException">Username ou mensagem vazios.</exception>
    /// <exception cref="LlmUnavailableException">A LLM não respondeu; nada foi gravado.</exception>
    public async Task<ChatResult> SendAsync(string username, string? conversationId, string message, CancellationToken cancellationToken = default)
    {
        var chat = await PrepareAsync(username, conversationId, message, cancellationToken);
        if (chat is null)
        {
            return ChatResult.NotFound;
        }

        var reply = await _llm.CompleteAsync(chat.LlmMessages, cancellationToken);
        var respondedAt = DateTime.UtcNow;

        // A LLM respondeu: só a partir daqui algo é gravado.
        var savedConversationId = await SaveAsync(chat, reply, respondedAt, cancellationToken);

        return ChatResult.Success(savedConversationId, reply);
    }

    /// <summary>
    /// Envia uma mensagem do usuário e repassa a resposta da LLM em pedaços, conforme chegam.
    /// Faz a mesma preparação de <see cref="SendAsync"/> e só grava (perfil, conversa, pergunta e resposta)
    /// quando o stream termina com sucesso. Se a LLM falhar, se a resposta vier vazia ou se a operação
    /// for cancelada, nada é gravado.
    /// </summary>
    /// <param name="username">Username do usuário (será normalizado).</param>
    /// <param name="conversationId">Conversa existente (espaços nas pontas são removidos); <c>null</c>, vazio ou só com espaços inicia uma conversa nova.</param>
    /// <param name="message">Texto enviado pelo usuário; os espaços nas pontas são removidos antes de validar e gravar.</param>
    /// <param name="onDelta">Chamado para cada pedaço de texto da LLM, na ordem em que chega.</param>
    /// <param name="cancellationToken">Token para cancelar a operação (ex.: cliente desconectou); é repassado até a LLM.</param>
    /// <returns>
    /// O resultado com o identificador da conversa e a resposta completa, ou <see cref="ChatResult.NotFound"/>
    /// se a conversa informada não existir para esse usuário (nesse caso a LLM não é chamada).
    /// </returns>
    /// <exception cref="ArgumentException">Username ou mensagem vazios.</exception>
    /// <exception cref="LlmUnavailableException">A LLM não respondeu, falhou no meio ou devolveu resposta vazia; nada foi gravado.</exception>
    /// <exception cref="OperationCanceledException">A operação foi cancelada antes da gravação; nada foi gravado.</exception>
    public async Task<ChatResult> StreamAsync(string username, string? conversationId, string message,
        Func<string, CancellationToken, Task> onDelta, CancellationToken cancellationToken = default)
    {
        var chat = await PrepareAsync(username, conversationId, message, cancellationToken);
        if (chat is null)
        {
            return ChatResult.NotFound;
        }

        var reply = new StringBuilder();
        await foreach (var piece in _llm.StreamAsync(chat.LlmMessages, cancellationToken))
        {
            reply.Append(piece);
            await onDelta(piece, cancellationToken);
        }

        var text = reply.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new LlmUnavailableException("O Ollama devolveu uma resposta vazia.");
        }

        var respondedAt = DateTime.UtcNow;

        // O stream terminou com sucesso: só a partir daqui algo é gravado (se o cliente não tiver cancelado).
        cancellationToken.ThrowIfCancellationRequested();
        var savedConversationId = await SaveAsync(chat, text, respondedAt, cancellationToken);

        return ChatResult.Success(savedConversationId, text);
    }

    /// <summary>
    /// Preparação comum a <see cref="SendAsync"/> e <see cref="StreamAsync"/>: normaliza e valida os dados,
    /// busca a conversa informada (com o histórico) e o perfil, e monta em memória o contexto da LLM.
    /// Nada é gravado aqui.
    /// </summary>
    /// <param name="username">Username do usuário (será normalizado).</param>
    /// <param name="conversationId">Conversa existente (espaços nas pontas são removidos); <c>null</c>, vazio ou só com espaços inicia uma conversa nova.</param>
    /// <param name="message">Texto enviado pelo usuário (espaços nas pontas são removidos).</param>
    /// <param name="cancellationToken">Token para cancelar a operação.</param>
    /// <returns>Os dados preparados, ou <c>null</c> se a conversa informada não existir para esse usuário.</returns>
    /// <exception cref="ArgumentException">Username ou mensagem vazios.</exception>
    private async Task<PreparedChat?> PrepareAsync(string username, string? conversationId, string message, CancellationToken cancellationToken)
    {
        var receivedAt = DateTime.UtcNow;

        var user = UserProfile.NormalizeUsername(username);
        if (user.Length == 0)
        {
            throw new ArgumentException("O username é obrigatório.", nameof(username));
        }

        var text = message?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            throw new ArgumentException("A mensagem é obrigatória.", nameof(message));
        }

        Conversation? conversation = null;
        IReadOnlyList<ChatMessage> history = [];

        var requestedId = conversationId?.Trim();
        if (!string.IsNullOrEmpty(requestedId))
        {
            conversation = await _conversations.GetAsync(user, requestedId, cancellationToken);
            if (conversation is null)
            {
                return null;
            }

            // A mensagem nova ocupa uma posição da janela de HistoryLimit.
            var previousLimit = _options.HistoryLimit - 1;
            if (previousLimit > 0)
            {
                history = await _conversations.GetLastMessagesAsync(user, conversation.Id, previousLimit, cancellationToken);
            }
        }

        var profile = await _profiles.GetAsync(user, cancellationToken);

        var llmMessages = new List<LlmMessage>
        {
            new(ChatMessage.RoleSystem, _options.SystemPrompt)
        };

        var profileContext = profile is null ? null : BuildProfileContext(profile);
        if (profileContext is not null)
        {
            llmMessages.Add(new LlmMessage(ChatMessage.RoleSystem, profileContext));
        }

        llmMessages.AddRange(history.Select(m => new LlmMessage(m.Role, m.Content)));
        llmMessages.Add(new LlmMessage(ChatMessage.RoleUser, text));

        return new PreparedChat(user, text, receivedAt, conversation, profile, llmMessages);
    }

    /// <summary>
    /// Grava uma troca bem-sucedida: cria o perfil e a conversa se preciso, grava a pergunta e a resposta
    /// numa única operação e atualiza o <see cref="Conversation.UpdatedAt"/>.
    /// </summary>
    /// <param name="chat">Dados preparados por <see cref="PrepareAsync"/>.</param>
    /// <param name="reply">Resposta completa da LLM.</param>
    /// <param name="respondedAt">Momento em que a LLM terminou de responder (UTC).</param>
    /// <param name="cancellationToken">Token para cancelar a operação.</param>
    /// <returns>O identificador da conversa (existente ou recém-criada).</returns>
    private async Task<string> SaveAsync(PreparedChat chat, string reply, DateTime respondedAt, CancellationToken cancellationToken)
    {
        if (chat.Profile is null)
        {
            await _profiles.GetOrCreateAsync(chat.Username, cancellationToken);
        }

        var conversation = chat.Conversation
            ?? await _conversations.CreateAsync(chat.Username, BuildTitle(chat.Text), chat.ReceivedAt, cancellationToken);

        var newMessages = new List<ChatMessage>
        {
            new() { Role = ChatMessage.RoleUser, Content = chat.Text, CreatedAt = chat.ReceivedAt },
            new() { Role = ChatMessage.RoleAssistant, Content = reply, CreatedAt = respondedAt }
        };

        await _conversations.AddMessagesAsync(chat.Username, conversation.Id, newMessages, cancellationToken);
        await _conversations.TouchAsync(chat.Username, conversation.Id, cancellationToken);

        return conversation.Id;
    }

    /// <summary>
    /// Gera o título da conversa a partir dos primeiros caracteres da mensagem.
    /// </summary>
    private static string BuildTitle(string message)
    {
        var text = message.Trim();
        return text.Length <= TitleMaxLength ? text : text[..TitleMaxLength];
    }

    /// <summary>
    /// Monta a mensagem de sistema com o perfil global, contendo apenas os campos preenchidos.
    /// </summary>
    /// <returns>O texto do perfil, ou <c>null</c> se nenhum campo estiver preenchido.</returns>
    private static string? BuildProfileContext(UserProfile profile)
    {
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(profile.DisplayName))
        {
            lines.Add($"- Nome: {profile.DisplayName}");
        }

        if (!string.IsNullOrWhiteSpace(profile.Role))
        {
            lines.Add($"- Cargo: {profile.Role}");
        }

        var preferences = profile.Preferences.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (preferences.Count > 0)
        {
            lines.Add($"- Preferências: {string.Join("; ", preferences)}");
        }

        return lines.Count == 0
            ? null
            : "Perfil do usuário:\n" + string.Join("\n", lines);
    }

    /// <summary>
    /// Dados de uma troca já validados, com o contexto montado para a LLM.
    /// </summary>
    /// <param name="Username">Username normalizado.</param>
    /// <param name="Text">Mensagem do usuário, sem espaços nas pontas.</param>
    /// <param name="ReceivedAt">Momento em que a requisição chegou (UTC).</param>
    /// <param name="Conversation">Conversa existente, ou <c>null</c> para criar uma nova ao gravar.</param>
    /// <param name="Profile">Perfil do usuário, ou <c>null</c> se ainda não existir.</param>
    /// <param name="LlmMessages">Contexto enviado à LLM.</param>
    private sealed record PreparedChat(
        string Username,
        string Text,
        DateTime ReceivedAt,
        Conversation? Conversation,
        UserProfile? Profile,
        IReadOnlyList<LlmMessage> LlmMessages);
}

/// <summary>
/// Resultado de <see cref="ChatService.SendAsync"/>.
/// </summary>
/// <param name="Found">Indica se a conversa foi encontrada (ou criada).</param>
/// <param name="ConversationId">Identificador da conversa.</param>
/// <param name="Reply">Resposta da LLM.</param>
public sealed record ChatResult(bool Found, string ConversationId, string Reply)
{
    /// <summary>
    /// Resultado para conversa não encontrada para o usuário.
    /// </summary>
    public static ChatResult NotFound { get; } = new(false, string.Empty, string.Empty);

    /// <summary>
    /// Cria um resultado de sucesso.
    /// </summary>
    /// <param name="conversationId">Identificador da conversa.</param>
    /// <param name="reply">Resposta da LLM.</param>
    /// <returns>O resultado com <see cref="Found"/> = <c>true</c>.</returns>
    public static ChatResult Success(string conversationId, string reply) => new(true, conversationId, reply);
}
