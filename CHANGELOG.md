# Changelog

Versionamento: V<versão>.<melhoria>.<bugs>

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
