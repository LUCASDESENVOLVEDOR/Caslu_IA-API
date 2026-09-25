using Caslu.IA.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;

namespace Caslu.IA.Infrastructure.Persistence;

/// <summary>
/// Nomes das collections do MongoDB usadas pela aplicação.
/// </summary>
internal static class MongoCollections
{
    /// <summary>
    /// Perfis globais dos usuários (camada 1).
    /// </summary>
    public const string Users = "users";

    /// <summary>
    /// Conversas dos usuários (camada 2).
    /// </summary>
    public const string Conversations = "conversations";

    /// <summary>
    /// Mensagens das conversas (camada 2).
    /// </summary>
    public const string Messages = "messages";
}

/// <summary>
/// Mapeamento das entidades do Domain para o MongoDB, sem atributos de persistência no Domain.
/// </summary>
internal static class MongoMappings
{
    /// <summary>
    /// Registra os mapeamentos das entidades. Pode ser chamado mais de uma vez.
    /// </summary>
    public static void Register()
    {
        BsonClassMap.TryRegisterClassMap<UserProfile>(cm =>
        {
            cm.AutoMap();
            cm.MapIdMember(u => u.Username);
            cm.SetIgnoreExtraElements(true);
        });

        BsonClassMap.TryRegisterClassMap<Conversation>(cm =>
        {
            cm.AutoMap();
            cm.MapIdMember(c => c.Id)
                .SetSerializer(new StringSerializer(BsonType.ObjectId))
                .SetIdGenerator(StringObjectIdGenerator.Instance);
            cm.SetIgnoreExtraElements(true);
        });

        BsonClassMap.TryRegisterClassMap<ChatMessage>(cm =>
        {
            cm.AutoMap();
            cm.MapIdMember(m => m.Id)
                .SetSerializer(new StringSerializer(BsonType.ObjectId))
                .SetIdGenerator(StringObjectIdGenerator.Instance);
            cm.MapMember(m => m.ConversationId)
                .SetSerializer(new StringSerializer(BsonType.ObjectId));
            cm.SetIgnoreExtraElements(true);
        });
    }
}
