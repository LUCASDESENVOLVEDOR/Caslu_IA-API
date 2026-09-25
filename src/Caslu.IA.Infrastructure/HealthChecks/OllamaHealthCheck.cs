using System.Net.Http.Json;
using Caslu.IA.Infrastructure.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Caslu.IA.Infrastructure.HealthChecks;

/// <summary>
/// Verifica a disponibilidade do servidor Ollama e se o modelo configurado está instalado,
/// consultando o endpoint <c>GET /api/tags</c>.
/// </summary>
public sealed class OllamaHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;

    /// <summary>
    /// Cria o health check do Ollama.
    /// </summary>
    /// <param name="httpClient">Cliente HTTP tipado, já configurado com a URL base e o tempo limite.</param>
    /// <param name="options">Opções do Ollama, usadas para obter o modelo esperado.</param>
    public OllamaHealthCheck(HttpClient httpClient, IOptions<OllamaOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    /// <summary>
    /// Consulta a lista de modelos do Ollama e verifica se o modelo configurado está presente.
    /// </summary>
    /// <param name="context">Contexto da execução do health check.</param>
    /// <param name="cancellationToken">Token para cancelar a verificação.</param>
    /// <returns>
    /// <see cref="HealthCheckResult.Healthy"/> se o modelo estiver na lista;
    /// <see cref="HealthCheckResult.Degraded"/> se o Ollama responder sem o modelo;
    /// <see cref="HealthCheckResult.Unhealthy"/> se o Ollama não responder ou exceder o tempo limite.
    /// </returns>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        TagsResponse? tags;

        try
        {
            tags = await _httpClient.GetFromJsonAsync<TagsResponse>("/api/tags", cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Ollama não respondeu dentro do tempo limite.");
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
        {
            return HealthCheckResult.Unhealthy($"Ollama indisponível: {ex.Message}");
        }

        var modelFound = tags?.Models?.Any(m =>
            string.Equals(m.Name, _options.Model, StringComparison.OrdinalIgnoreCase)) ?? false;

        return modelFound
            ? HealthCheckResult.Healthy($"Ollama respondeu e o modelo '{_options.Model}' está disponível.")
            : HealthCheckResult.Degraded($"Ollama respondeu, mas o modelo '{_options.Model}' não está instalado.");
    }

    /// <summary>
    /// Resposta do endpoint <c>/api/tags</c> do Ollama.
    /// </summary>
    /// <param name="Models">Modelos instalados no servidor.</param>
    private sealed record TagsResponse(List<TagModel>? Models);

    /// <summary>
    /// Modelo listado pelo endpoint <c>/api/tags</c> do Ollama.
    /// </summary>
    /// <param name="Name">Nome do modelo (ex.: qwen2.5:3b).</param>
    private sealed record TagModel(string? Name);
}
