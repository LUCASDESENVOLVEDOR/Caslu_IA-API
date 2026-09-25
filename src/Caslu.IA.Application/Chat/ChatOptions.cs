namespace Caslu.IA.Application.Chat;

/// <summary>
/// Opções do chat, carregadas da seção "Chat" da configuração.
/// </summary>
public sealed class ChatOptions
{
    /// <summary>
    /// Nome da seção de configuração que contém estas opções.
    /// </summary>
    public const string SectionName = "Chat";

    /// <summary>
    /// Quantidade máxima de mensagens da conversa enviadas à LLM (janela de histórico).
    /// </summary>
    public int HistoryLimit { get; set; }

    /// <summary>
    /// Instrução de sistema enviada no início de toda requisição à LLM.
    /// </summary>
    public string SystemPrompt { get; set; } = string.Empty;
}
