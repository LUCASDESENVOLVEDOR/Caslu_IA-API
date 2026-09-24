namespace Caslu.IA.Infrastructure.Configuration;

/// <summary>
/// Opções de conexão com o MongoDB, carregadas da seção "MongoDb" da configuração.
/// </summary>
public sealed class MongoDbOptions
{
    /// <summary>
    /// Nome da seção de configuração que contém estas opções.
    /// </summary>
    public const string SectionName = "MongoDb";

    /// <summary>
    /// String de conexão do MongoDB. Deve ser fornecida por User Secrets ou variável de ambiente.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Nome do banco de dados utilizado pela aplicação.
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;
}
