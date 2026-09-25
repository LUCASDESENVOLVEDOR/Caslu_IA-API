# Changelog

Versionamento: V<versão>.<melhoria>.<bugs>

## [V1.03.001] - 2026-09-25 18:05:02
### Correções do chat
- ChatService só grava depois da resposta da LLM: se a LLM falhar, nada é gravado (nem perfil, nem conversa, nem mensagens) e a API continua retornando 503
- Conversa informada e histórico lidos antes da chamada à LLM; contexto montado em memória (SystemPrompt, perfil, histórico e mensagem nova), com a janela HistoryLimit contando a mensagem nova
- Conversa nova criada só após a resposta; mensagem do usuário e resposta gravadas numa única operação (AddMessagesAsync com InsertMany); AddMessageAsync removido
- CreatedAt da mensagem do usuário = chegada da requisição; CreatedAt da resposta = momento em que a LLM respondeu
- conversationId vazio ou só com espaços é tratado como null (conversa nova)
- Mensagem passa por Trim antes da validação e da gravação; o limite de 4000 caracteres vale para o texto sem espaços nas pontas

## [V1.03.000] - 2026-09-25 17:50:07
### Chat Nível 0 com memória em camadas
- Domain: entidades UserProfile (camada 1, perfil global; username normalizado), Conversation e ChatMessage (camada 2)
- Application: IUserProfileRepository, IConversationRepository (username obrigatório em todos os métodos), ILlmClient, ChatOptions e ChatService
- ChatService: garante o perfil, cria ou busca a conversa do usuário, grava as mensagens e monta o contexto (SystemPrompt, perfil com os campos preenchidos, últimas HistoryLimit mensagens)
- Infrastructure: repositórios MongoDB nas collections users, conversations e messages, com filtro por username centralizado em ForUser
- Índices criados na inicialização: conversations (Username, UpdatedAt desc) e messages (Username, ConversationId, CreatedAt)
- OllamaLlmClient com HttpClient tipado próprio (POST /api/chat, stream false, timeout de OllamaOptions.TimeoutSeconds)
- Seção "Chat" no appsettings.json (HistoryLimit = 20 e SystemPrompt) com ValidateOnStart
- Api: POST /api/chat com 400 (dados inválidos), 404 (conversa não encontrada para o usuário) e 503 (LLM indisponível)

## [V1.02.000] - 2026-09-25 17:16:12
### Health check
- FrameworkReference Microsoft.AspNetCore.App na Infrastructure (IHealthCheck e AddHttpClient)
- MongoHealthCheck: executa { ping: 1 } no MongoDB; Unhealthy com a mensagem do erro, sem expor a connection string
- OllamaHealthCheck: HttpClient tipado (BaseUrl de OllamaOptions, timeout de 5 s) consultando GET /api/tags; Healthy com o modelo instalado, Degraded sem o modelo, Unhealthy sem resposta ou em timeout
- Health checks registrados em AddInfrastructure: "mongodb" (tag db) e "llm" (tag llm)
- Endpoint /health com resposta JSON (status, totalDurationMs e checks com name, status, durationMs e description)

## [V1.01.001] - 2026-09-24 16:56:45
### Repositório
- .gitattributes com '* text=auto' para normalizar finais de linha e eliminar os avisos LF/CRLF do Git

## [V1.01.000] - 2026-09-24 16:43:19
### Configuração
- Pacotes MongoDB.Driver e Microsoft.Extensions.Options.ConfigurationExtensions na Infrastructure
- Classes de opções MongoDbOptions e OllamaOptions com validação na inicialização (ValidateOnStart)
- Registro de IMongoClient e IMongoDatabase via AddInfrastructure
- appsettings.json com DatabaseName e configurações do Ollama (sem segredos)
- User Secrets inicializado no projeto da API

### Documentação
- README.md com badges, diagramas Mermaid, camadas, princípios, configuração e roadmap

## [V1.00.000] - 2026-09-24 16:35:37
### Estrutura
- Solução Caslu.IA criada em .NET 8 com arquitetura em camadas
- Projetos: Caslu.IA.Api (Web API com controllers), Caslu.IA.Application, Caslu.IA.Domain, Caslu.IA.Infrastructure
- Referências: Api → Application, Infrastructure; Infrastructure → Application, Domain; Application → Domain
- Arquivos de exemplo do template removidos
- CLAUDE.md com as regras do projeto
