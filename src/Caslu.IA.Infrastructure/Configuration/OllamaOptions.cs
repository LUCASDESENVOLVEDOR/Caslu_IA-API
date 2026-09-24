namespace Caslu.IA.Infrastructure.Configuration;

/// <summary>
/// Opções de acesso ao servidor Ollama, carregadas da seção "Ollama" da configuração.
/// </summary>
public sealed class OllamaOptions
{
    /// <summary>
    /// Nome da seção de configuração que contém estas opções.
    /// </summary>
    public const string SectionName = "Ollama";

    /// <summary>
    /// URL base do servidor Ollama (ex.: http://localhost:11434).
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Identificador do modelo utilizado nas requisições (ex.: qwen2.5:3b).
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Tempo limite, em segundos, das requisições ao servidor Ollama.
    /// </summary>
    public int TimeoutSeconds { get; set; }
}
