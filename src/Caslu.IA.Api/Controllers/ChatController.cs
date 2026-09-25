using System.Text.Encodings.Web;
using System.Text.Json;
using Caslu.IA.Api.Contracts;
using Caslu.IA.Application.Abstractions;
using Caslu.IA.Application.Chat;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Caslu.IA.Api.Controllers;

/// <summary>
/// Endpoints do chat (Nível 0): conversa com a LLM usando o perfil global e o histórico da conversa,
/// com resposta completa (<c>POST /api/chat</c>) ou em streaming SSE (<c>POST /api/chat/stream</c>).
/// </summary>
[ApiController]
[Route("api/chat")]
public sealed class ChatController : ControllerBase
{
    private const int MessageMaxLength = 4000;
    private const string LlmUnavailableTitle = "LLM indisponível.";

    /// <summary>
    /// JSON dos eventos SSE: sempre em uma linha (quebras de linha do texto ficam escapadas) e sem escapar acentos.
    /// </summary>
    private static readonly JsonSerializerOptions EventJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ChatService _chatService;

    /// <summary>
    /// Cria o controller do chat.
    /// </summary>
    /// <param name="chatService">Serviço que orquestra a troca de mensagens.</param>
    public ChatController(ChatService chatService)
    {
        _chatService = chatService;
    }

    /// <summary>
    /// Envia uma mensagem e devolve a resposta da assistente.
    /// </summary>
    /// <param name="request">Username, conversa (opcional) e mensagem.</param>
    /// <param name="cancellationToken">Cancelado quando o cliente encerra a requisição.</param>
    /// <returns>
    /// 200 com a conversa e a resposta; 400 se os dados forem inválidos (a mensagem é validada já sem
    /// espaços nas pontas); 404 se a conversa não existir para o usuário; 503 se a LLM não responder
    /// (nesse caso nada é gravado).
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SendAsync([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        var invalid = Validate(request, out var username, out var message);
        if (invalid is not null)
        {
            return invalid;
        }

        try
        {
            var result = await _chatService.SendAsync(username, request.ConversationId, message, cancellationToken);
            if (!result.Found)
            {
                return ConversationNotFound();
            }

            return Ok(new ChatResponse(result.ConversationId, result.Reply));
        }
        catch (LlmUnavailableException ex)
        {
            return LlmUnavailable(ex.Message);
        }
    }

    /// <summary>
    /// Envia uma mensagem e devolve a resposta da assistente em streaming (Server-Sent Events).
    /// </summary>
    /// <remarks>
    /// Validações (400), conversa inexistente (404) e LLM indisponível antes do primeiro pedaço (503) voltam como
    /// ProblemDetails, sem abrir o stream. Depois que a LLM começa a responder, os eventos são <c>delta</c>
    /// (<c>{"text":"..."}</c>), <c>done</c> (<c>{"conversationId":"..."}</c>) e <c>error</c>
    /// (<c>{"title":"...","detail":"..."}</c>). Nada é gravado se o stream não terminar com sucesso.
    /// </remarks>
    /// <param name="request">Username, conversa (opcional) e mensagem, como em <c>POST /api/chat</c>.</param>
    /// <param name="cancellationToken">
    /// Cancelado quando o cliente encerra a requisição (<see cref="HttpContext.RequestAborted"/>); é repassado até o Ollama.
    /// </param>
    /// <returns>O stream de eventos, ou ProblemDetails (400, 404 ou 503) antes de o stream começar.</returns>
    [HttpPost("stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> StreamAsync([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        var invalid = Validate(request, out var username, out var message);
        if (invalid is not null)
        {
            return invalid;
        }

        var started = false;

        async Task WriteDeltaAsync(string text, CancellationToken ct)
        {
            if (!started)
            {
                // O stream só abre quando a LLM aceitou a requisição e começou a responder.
                StartEventStream();
                started = true;
            }

            await WriteEventAsync("delta", new { text }, ct);
        }

        try
        {
            var result = await _chatService.StreamAsync(username, request.ConversationId, message, WriteDeltaAsync, cancellationToken);
            if (!result.Found)
            {
                return ConversationNotFound();
            }

            await WriteEventAsync("done", new { conversationId = result.ConversationId }, cancellationToken);
        }
        catch (LlmUnavailableException ex) when (!started)
        {
            return LlmUnavailable(ex.Message);
        }
        catch (LlmUnavailableException ex)
        {
            await WriteEventAsync("error", new { title = LlmUnavailableTitle, detail = ex.Message }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // O cliente desconectou: nada foi gravado e não há para quem responder.
        }

        return new EmptyResult();
    }

    /// <summary>
    /// Validações comuns aos dois endpoints: username obrigatório; mensagem obrigatória e com até
    /// 4000 caracteres, contados já sem os espaços nas pontas.
    /// </summary>
    /// <param name="request">Corpo da requisição.</param>
    /// <param name="username">Username informado (o serviço normaliza).</param>
    /// <param name="message">Mensagem sem os espaços nas pontas.</param>
    /// <returns>O ProblemDetails de 400, ou <c>null</c> se a requisição for válida.</returns>
    private ObjectResult? Validate(ChatRequest request, out string username, out string message)
    {
        username = request.Username ?? string.Empty;
        message = request.Message?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Requisição inválida.",
                detail: "O username é obrigatório.");
        }

        if (message.Length == 0)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Requisição inválida.",
                detail: "A mensagem é obrigatória.");
        }

        if (message.Length > MessageMaxLength)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Requisição inválida.",
                detail: $"A mensagem deve ter no máximo {MessageMaxLength} caracteres.");
        }

        return null;
    }

    /// <summary>
    /// ProblemDetails 404 para conversa que não existe para o usuário.
    /// </summary>
    /// <returns>O resultado 404.</returns>
    private ObjectResult ConversationNotFound() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Conversa não encontrada.",
            detail: "A conversa informada não existe para este usuário.");

    /// <summary>
    /// ProblemDetails 503 para LLM indisponível.
    /// </summary>
    /// <param name="detail">Motivo da falha, sem detalhes internos.</param>
    /// <returns>O resultado 503.</returns>
    private ObjectResult LlmUnavailable(string detail) =>
        Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: LlmUnavailableTitle, detail: detail);

    /// <summary>
    /// Prepara a resposta como stream SSE: status 200, cabeçalhos do stream e sem buffer.
    /// </summary>
    private void StartEventStream()
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
    }

    /// <summary>
    /// Escreve um evento SSE (<c>event</c> e <c>data</c> com JSON de uma linha) e faz flush.
    /// </summary>
    /// <param name="eventName">Nome do evento: <c>delta</c>, <c>done</c> ou <c>error</c>.</param>
    /// <param name="data">Conteúdo do evento, serializado em JSON.</param>
    /// <param name="cancellationToken">Token para cancelar a escrita.</param>
    /// <returns>Uma tarefa que representa a escrita.</returns>
    private async Task WriteEventAsync(string eventName, object data, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(data, EventJsonOptions);
        await Response.WriteAsync($"event: {eventName}\ndata: {json}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
