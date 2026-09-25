using Caslu.IA.Domain.Entities;

namespace Caslu.IA.Application.Abstractions;

/// <summary>
/// Acesso aos perfis globais dos usuários (camada 1 da memória).
/// </summary>
public interface IUserProfileRepository
{
    /// <summary>
    /// Obtém o perfil do usuário.
    /// </summary>
    /// <param name="username">Username normalizado do dono do perfil.</param>
    /// <param name="cancellationToken">Token para cancelar a operação.</param>
    /// <returns>O perfil encontrado, ou <c>null</c> se não existir.</returns>
    Task<UserProfile?> GetAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém o perfil do usuário, criando-o se não existir
    /// (<see cref="UserProfile.OnboardingCompleted"/> = <c>false</c> e demais campos vazios).
    /// </summary>
    /// <param name="username">Username normalizado do dono do perfil.</param>
    /// <param name="cancellationToken">Token para cancelar a operação.</param>
    /// <returns>O perfil existente ou recém-criado.</returns>
    Task<UserProfile> GetOrCreateAsync(string username, CancellationToken cancellationToken = default);
}
