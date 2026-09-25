using Caslu.IA.Application.Abstractions;
using Caslu.IA.Domain.Entities;
using MongoDB.Driver;

namespace Caslu.IA.Infrastructure.Persistence;

/// <summary>
/// Repositório de perfis globais na collection "users" (o username é o <c>_id</c>).
/// </summary>
public sealed class UserProfileRepository : IUserProfileRepository
{
    private readonly IMongoCollection<UserProfile> _users;

    /// <summary>
    /// Cria o repositório de perfis.
    /// </summary>
    /// <param name="database">Banco de dados da aplicação.</param>
    public UserProfileRepository(IMongoDatabase database)
    {
        _users = database.GetCollection<UserProfile>(MongoCollections.Users);
    }

    /// <inheritdoc />
    public async Task<UserProfile?> GetAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _users.Find(ForUser(username)).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserProfile> GetOrCreateAsync(string username, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var update = Builders<UserProfile>.Update
            .SetOnInsert(u => u.DisplayName, string.Empty)
            .SetOnInsert(u => u.Role, string.Empty)
            .SetOnInsert(u => u.Preferences, new List<string>())
            .SetOnInsert(u => u.OnboardingCompleted, false)
            .SetOnInsert(u => u.CreatedAt, now)
            .SetOnInsert(u => u.UpdatedAt, now);

        var options = new FindOneAndUpdateOptions<UserProfile>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        return await _users.FindOneAndUpdateAsync(ForUser(username), update, options, cancellationToken);
    }

    /// <summary>
    /// Único ponto de montagem de filtros deste repositório: sempre restringe ao username.
    /// </summary>
    /// <param name="username">Username do dono dos dados.</param>
    /// <param name="filter">Filtro adicional, combinado com o do username.</param>
    /// <returns>O filtro restrito ao usuário.</returns>
    /// <exception cref="ArgumentException">Username vazio.</exception>
    private static FilterDefinition<UserProfile> ForUser(string username, FilterDefinition<UserProfile>? filter = null)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("O username é obrigatório.", nameof(username));
        }

        var byUser = Builders<UserProfile>.Filter.Eq(u => u.Username, username);
        return filter is null ? byUser : Builders<UserProfile>.Filter.And(byUser, filter);
    }
}
