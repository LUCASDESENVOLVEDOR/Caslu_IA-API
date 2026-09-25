namespace Caslu.IA.Domain.Entities;

/// <summary>
/// Perfil global do usuário (camada 1 da memória): quem ele é, válido para todas as conversas.
/// </summary>
public sealed class UserProfile
{
    /// <summary>
    /// Identificador do usuário (chave do documento), normalizado por <see cref="NormalizeUsername"/>.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Nome de exibição do usuário.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Cargo ou função do usuário na empresa.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Preferências declaradas pelo usuário (ex.: "respostas curtas").
    /// </summary>
    public List<string> Preferences { get; set; } = [];

    /// <summary>
    /// Indica se o usuário já concluiu o onboarding do perfil.
    /// </summary>
    public bool OnboardingCompleted { get; set; }

    /// <summary>
    /// Data de criação do perfil (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Data da última atualização do perfil (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Normaliza um username: remove espaços nas pontas e converte para minúsculas.
    /// </summary>
    /// <param name="username">Username informado.</param>
    /// <returns>O username normalizado, ou string vazia se for nulo.</returns>
    public static string NormalizeUsername(string? username) =>
        (username ?? string.Empty).Trim().ToLowerInvariant();
}
