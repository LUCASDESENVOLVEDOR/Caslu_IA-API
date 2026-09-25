using Caslu.IA.Api.Contracts;
using Caslu.IA.Application.Abstractions;
using Caslu.IA.Application.Chat;
using Microsoft.AspNetCore.Mvc;

namespace Caslu.IA.Api.Controllers;

/// <summary>
/// Endpoint do chat (Nível 0): conversa com a LLM usando o perfil global e o histórico da conversa.
/// </summary>
[ApiController]
[Route("api/chat")]
public sealed class ChatController : ControllerBase
{
    private const int MessageMaxLength = 4000;

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
    /// 200 com a conversa e a resposta; 400 se os dados forem inválidos;
    /// 404 se a conversa não existir para o usuário; 503 se a LLM não responder.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SendAsync([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Requisição inválida.",
                detail: "O username é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Requisição inválida.",
                detail: "A mensagem é obrigatória.");
        }

        if (request.Message.Length > MessageMaxLength)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Requisição inválida.",
                detail: $"A mensagem deve ter no máximo {MessageMaxLength} caracteres.");
        }

        try
        {
            var result = await _chatService.SendAsync(request.Username, request.ConversationId, request.Message, cancellationToken);
            if (!result.Found)
            {
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Conversa não encontrada.",
                    detail: "A conversa informada não existe para este usuário.");
            }

            return Ok(new ChatResponse(result.ConversationId, result.Reply));
        }
        catch (LlmUnavailableException ex)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "LLM indisponível.",
                detail: ex.Message);
        }
    }
}
