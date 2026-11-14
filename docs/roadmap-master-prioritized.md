# Roadmap mestre priorizado — ListForge

**Atualizado:** 2026-11-13  
**Repositório:** NeuberJone/ListForge (não ListForgeV2)  
**Versão de referência documentada:** 2.1.43 (.NET 8 / WPF / Windows x64)

> Documento de planejamento. Status derivados do README, CHANGELOG e inspeção pontual de `Core/JsonListImporter.cs`, `Core/FileImporter.cs`, `ListForge.csproj` e instalador. Não foram executados build, testes ou smoke tests nesta atualização.  
> **[x]** implementado/documentado · **[~]** existente, requer verificação/aceitação · **[ ]** pendente.

## Prioridades e ordem de implantação

| Ordem | Prioridade | Entrega | Critério de conclusão |
|---|---|---|---|
| 0 | P0 | Baseline 2.1.43: testes, builds e distribuição | Processamento, JSON, Trial, OCR, updater e instalador aprovados |
| 1 | P1 | **Abrir lista de arquivo JSON local** | .json aberto no editor sem perda de dados; criar/adicionar; testes |
| 2 | P1 | Corrigir e robustecer interpretação JSON | Quantidades em múltiplos campos, cabeçalhos, metadados, zero à esquerda |
| 3 | P1 | Contrato de produção versionado para PXToolBox | Interoperabilidade sem quebrar JSON legado |
| 4 | P1 | Extração incremental do núcleo reutilizável | Core sem WPF, build/testes/standalone intactos |
| 5 | P1 | Qualidade da importação e OCR | Diagnóstico, prévia, erros e regressões |
| 6 | P1 | Integração com montagem do PXToolBox | Fluxo lista → contrato → peças com validação |
| 7 | P2 | Perfis/modelos de produção e UX | Configuração reutilizável sem duplicar perfis existentes |
| 8 | P2 | Comparar versões distintas de listas | Mudanças de pessoas, tamanhos, números e quantidades |
| 9 | P2 | Recuperação de sessão, retenção e backups | Privacidade, consentimento e recuperação verificáveis |
| 10 | P2 | Licenciamento comercial independente | Edições e licenças separadas de canais de distribuição |
| 11 | P3 | Integração PX Ecosystem Manager | Atualização central sem retirar updater standalone |
| 12 | P3 | Sincronização opcional de perfis | Segurança e conflitos definidos; offline-first mantido |

## 0 — P0: estabilizar a base atual

- [x] Editor WPF, núcleo de processamento, separadores, capitalização, grupos de tamanho, quantidades e Lista avançada documentados.
- [x] Saída textual, geração/edição protegida de JSON, prévia de impacto, comparação semântica entrada/saída e histórico documentados.
- [x] Importação TXT/CSV, PDF, Word, Excel, imagens com Tesseract e JSON por link documentados.
- [x] Perfis, temas, Modo Forja, backups, pacote de suporte e edição Trial documentados.
- [x] Instalador Inno Setup, portátil, manifesto HTTPS, SHA-256, fallback GitHub e publicação R2 documentados.
- [~] Rodar testes e builds limpos de edição completa, portátil e Trial; conferir versão 2.1.43 e distribuição.
- [~] Validar OCR com dependências nativas, arquivos reais e instalações limpas.
- [~] Testar atualização, rollback, cancelamento, integridade, instalação e preservação de dados.
- [~] Cobrir listas mistas, múltiplas peças do mesmo tamanho, campos vazios, duplicidades, Unicode, separadores e números com zero à esquerda.

**Gate:** baseline reproduzível antes de refatoração estrutural.

## 1 — P1: importar arquivos JSON locais (nova prioridade imediata)

**Objetivo:** no comando **Abrir arquivo**, incluir `.json` e permitir carregar uma lista do disco sem depender de URL.

- [ ] Adicionar filtro `*.json` ao seletor de arquivos e roteamento para importação JSON local.
- [ ] Reutilizar `Core/JsonListImporter.cs` (método `ExtractListTextFromJsonData`) em vez de criar parser paralelo.
- [ ] Ler arquivo local com codificação adequada, validar JSON e suportar objeto com `orders` ou array de registros, conforme contrato já aceito.
- [ ] Oferecer **Criar nova lista** e **Adicionar à lista atual**, preservando duplicidades intencionalmente.
- [ ] Exibir prévia/avisos antes de substituir a entrada; falha ou cancelamento não pode alterar a lista existente nem consumir crédito Trial.
- [ ] Preservar Name, Number, Nickname, BloodType, Gender e tipos de peça compatíveis; não perder zeros à esquerda do número.
- [ ] Quando disponíveis, conservar metadados do pedido (project, title, client_name, order_number) em contexto apropriado, sem inserir campos extras incorretamente nas linhas.
- [ ] Não executar processamento automaticamente sem decisão explícita; preservar o fluxo de revisão.
- [ ] Testar arquivos válidos/invalidos, vazios, UTF-8 BOM, permissões, JSON grande, esquema incompatível e adição com seleção atual.

**Gate:** arquivo JSON local abre e se comporta como JSON por link, com testes automatizados e smoke na UI.

## 2 — P1: qualidade do importador JSON e compatibilidade

- [x] Importação de JSON por link e conversão para lista documentadas.
- [~] Inspecionar algoritmo atual de expansão de `quantidade-tamanho`: ele expande a primeira ocorrência encontrada e interrompe a busca; verificar impacto quando vários campos de peça contêm quantidades simultaneamente.
- [ ] Definir semântica correta para múltiplas quantidades e peças no mesmo registro: **não** multiplicar combinações cartesianas nem descartar peças; manter representação fiel ao contrato.
- [ ] Validar `Gender`, `Socks` legado e cabeçalhos de Lista avançada sem deslocar colunas.
- [ ] Tratar campos opcionais, valores nulos, formatos inesperados e número como string.
- [ ] Adicionar testes de round-trip JSON → editor → processamento → JSON, incluindo formatos atuais do SISBolt.
- [ ] Reutilizar a mesma pipeline para arquivo local e URL; distinguir apenas a origem dos bytes.

**Gate:** nenhum campo de peça, quantidade ou identidade se perde silenciosamente.

## 3 — P1: contrato versionado para PXToolBox

- [ ] Manter JSON atual compatível com consumidores existentes.
- [ ] Definir versão opcional de contrato de produção (identificadores, revisão, pedido, cliente, tipos, tamanhos, quantidades, origem e validações).
- [ ] Documentar compatibilidade de versões, campos obrigatórios/opcionais e exemplos reais.
- [ ] Reutilizar núcleo do ListForge no PXToolBox sem criar duas implementações divergentes.
- [ ] Não acoplar o ListForge ao CorelDRAW, ao Nexor ou aos bancos de dados de outros produtos.

## 4 — P1: arquitetura incremental

- [x] `ListProcessor.cs` como fachada e lógica em `ListParser`, `ListOutputBuilder`, `JsonOrderBuilder` e `JsonListImporter` documentadas.
- [ ] Auditar dependências reais de WPF, importadores, configuração, logs e empacotamento.
- [ ] Extrair somente componentes que tenham consumidores concretos; evitar criar projetos vazios.
- [ ] Preservar namespaces públicos, serialização, versões, build x64, Trial e instalador.
- [ ] Executar testes por etapa e manter rollback simples.

## 5 — P1: importação/OCR

- [x] Tesseract por+eng, fallback e importação de arquivos documentados.
- [ ] Diagnóstico de dependências e idiomas OCR, com mensagem acionável.
- [ ] Prévia de extração e avisos de baixa confiança; não aceitar automaticamente dados incertos.
- [ ] Melhorar suporte a PDFs digitalizados, tabelas e importações grandes mediante testes.
- [ ] Tratar `.doc` e `.xls` legados conforme suporte real, sem prometer compatibilidade não testada.

## 6 — P1: integração operacional PX

- [ ] Enviar lista estruturada ao contrato de montagem e receber erros de validação.
- [ ] Preservar identidades de pedido, peça e revisão entre ListForge e PXToolBox.
- [ ] Permitir revisão manual antes de criar objetos no CorelDRAW.
- [ ] Garantir funcionamento independente do ListForge.

## 7 — P2: produtividade e perfis

- [x] Perfis de trabalho, exportação/importação de configurações, temas e Modo Forja documentados.
- [ ] Verificar se perfis existentes já cobrem modelos de produção; ampliar em vez de duplicar.
- [ ] Criar presets opcionais por tipo de produção e compartilhamento controlado entre operadores.
- [ ] Melhorar fluxo de edição, validação e exportação sem poluir interface.

## 8 — P2: comparação entre versões de listas

- [x] Comparação semântica **entrada versus saída do mesmo processamento** documentada.
- [ ] Comparar **arquivo anterior versus arquivo atualizado** de um pedido.
- [ ] Destacar inclusões, exclusões, mudanças de tamanho/número/peça/quantidade e duplicidades.
- [ ] Preservar privacidade: não armazenar listas completas por padrão.

## 9 — P2: recuperação e segurança de dados

- [x] Histórico de metadados, backups ao sobrescrever e pacote de suporte documentados.
- [ ] Recuperação de sessão opcional após crash, com proteção de dados pessoais.
- [ ] Política de retenção, limpeza e limites para backups; não persistir listas sensíveis sem escolha do usuário.
- [ ] Testar restauração e corrupção de arquivos.

## 10 — P2: licenciamento e comercialização

- [x] Trial por créditos de processamento documentado.
- [ ] Definir licença por usuário/dispositivo/edição e modo offline seguro.
- [ ] Separar licença, edição (completa/Trial) e canal (instalada/portátil).
- [ ] Validar instalação/atualização sem perda de licença ou configuração.

## 11 — P3: PX Ecosystem Manager

- [ ] Integrar instalação, atualização, reparação e remoção opcionais via Manager.
- [ ] Reutilizar manifestos/hashes existentes onde viável; não remover o atualizador standalone.
- [ ] Evitar duplicidade de downloads e conflitos entre instalações.
- [ ] Preservar independência do ListForge e funcionamento offline.

## 12 — P3: backlog condicionado a valor

- [ ] Sincronização opcional de perfis, com resolução de conflitos e segurança.
- [ ] Modelos configuráveis adicionais de exportação.
- [ ] Importação em lote apenas se houver demanda e regras de validação definidas.
- [ ] Diagnósticos e métricas opt-in, sem coletar conteúdo pessoal das listas.

## Próxima execução recomendada

1. **Branch:** `feat/local-json-list-import`. Implementar abertura `.json` local, compartilhando parser com URL, com testes de não perda de dados.
2. **Branch:** `fix/json-import-multiple-piece-quantities`. Definir e corrigir semântica de quantidades em múltiplas peças antes de prometer round-trip.
3. **Branch:** `feat/versioned-production-contract`. Estabelecer contrato PX e testes de compatibilidade.
4. **Branch:** `refactor/extract-listforge-core`. Apenas após baseline e mapa de dependências.

**Sugestão de commit para a primeira entrega:** `feat: import lists from local JSON files`.

## Regras de acompanhamento

- Não marcar [x] por proposta ou código não validado; registrar separadamente implementação, testes e smoke manual.
- Manter CHANGELOG e README como fontes de histórico; este arquivo organiza prioridades.
- Commits em inglês; nenhuma alteração de código, release ou publicação automática sem autorização.
