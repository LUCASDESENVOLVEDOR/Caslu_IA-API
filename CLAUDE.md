# Regras do projeto Caslu IA

## Modo de trabalho
- Faça somente o que foi pedido. Ideias e melhorias vão numa seção SUGESTÕES, sem implementar.
- Não crie, renomeie ou mova arquivos fora do escopo pedido. Não adicione dependências sem pedido explícito.
- Preserve estilo, formatação e imports existentes. Mudança mínima necessária.
- Em caso de dúvida, faça no máximo 1 pergunta objetiva e pare.
- Antes de qualquer commit, mostre git status e git diff e espere confirmação.

## Código
- .NET 8, C#, arquitetura em camadas: Domain ← Application ← Infrastructure ← Api.
- Domain não depende de nenhuma camada. Application depende só de Domain.
- Todo tipo e membro público deve ter comentário de documentação XML (///).
- Segredos (senhas, connection strings) nunca no repositório: usar User Secrets / variáveis de ambiente.

## Princípios da IA
- A LLM nunca acessa bancos diretamente; toda ação passa pela API.
- A LLM apenas propõe tools (function calling); a API valida e executa.
- O histórico das conversas pertence à aplicação (MongoDB).

## Changelog
- Toda mudança entra no CHANGELOG.md com data e hora obtidas via PowerShell:
  Get-Date -Format "yyyy-MM-dd HH:mm:ss"
- Versionamento V<versão>.<melhoria>.<bugs> (ex.: V1.00.000). O segundo número sobe a cada
  grande melhoria; o terceiro conta correções de bug até a próxima melhoria.

## Formato de resposta
1) RESUMO  2) ALTERAÇÕES (por arquivo)  3) NOTAS  4) SUGESTÕES  5) CHECKLIST
