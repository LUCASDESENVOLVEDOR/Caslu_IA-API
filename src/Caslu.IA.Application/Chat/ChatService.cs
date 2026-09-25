using Caslu.IA.Application.Abstractions;
using Caslu.IA.Domain.Entities;

namespace Caslu.IA.Application.Chat;

/// <summary>
/// Orquestra uma troca de mensagens do chat: resolve a conversa, monta em memória o contexto
/// enviado à LLM e só grava perfil, conversa e mensagens depois que a LLM responde.
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
    /// <param name="conversationId">Conversa existente; <c>null</c>, vazio ou só com espaços inicia uma conversa nova.</param>
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

        if (!string.IsNullOrWhiteSpace(conversationId))
        {
            conversation = await _conversations.GetAsync(user, conversationId, cancellationToken);
            if (conversation is null)
            {
                return ChatResult.NotFound;
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

        var reply = await _llm.CompleteAsync(llmMessages, cancellationToken);
        var respondedAt = DateTime.UtcNow;

        // A LLM respondeu: só a partir daqui algo é gravado.
        if (profile is null)
        {
            await _profiles.GetOrCreateAsync(user, cancellationToken);
        }

        conversation ??= await _conversations.CreateAsync(user, BuildTitle(text), cancellationToken);

        var newMessages = new List<ChatMessage>
        {
            new() { Role = ChatMessage.RoleUser, Content = text, CreatedAt = receivedAt },
            new() { Role = ChatMessage.RoleAssistant, Content = reply, CreatedAt = respondedAt }
        };

        await _conversations.AddMessagesAsync(user, conversation.Id, newMessages, cancellationToken);
        await _conversations.TouchAsync(user, conversation.Id, cancellationToken);

        return ChatResult.Success(conversation.Id, reply);
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
