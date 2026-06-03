# Changelog

Todas as mudanças relevantes do ListForge serão documentadas neste arquivo.

O formato segue uma estrutura simples inspirada em Keep a Changelog, com entradas agrupadas por versão.

## [2.1.23] - 2026-06-02

### Adicionado

- Configuração de qualidade de código com EditorConfig e orientações de formatação.
- Testes para listas grandes, cobrindo validação, processamento, ordenação, expansão de quantidades e geração de JSON.
- Documentação do fluxo de publicação de releases no GitHub com tag versionada e artefatos anexados.
- Script auxiliar para validar artefatos e preparar publicação de release no GitHub.
- Função para gerar pacote de suporte com logs recentes e informações técnicas seguras para diagnóstico.
- Workflow de CI no GitHub Actions para validar build e testes automaticamente.
- Testes de integração para validar o fluxo principal de pré-validação, processamento, ordenação, saída textual, JSON e Trial.

### Alterado

- Pacote de suporte agora permite controlar inclusão de logs recentes e reforça aviso de revisão antes do envio.
- Refatoradas responsabilidades do MainViewModel para serviços/helpers menores, mantendo o comportamento da interface.
- Extraído o fluxo principal de processamento e exportação para serviços testáveis, mantendo bindings e comandos existentes.
- Padronizado retorno de operações internas para separar mensagens ao usuário, detalhes técnicos e logging.
- Preservados fluxos de processamento, importação, configurações, logs, Trial e tela Sobre.

## [2.1.22] - 2026-06-02

### Adicionado

- Geração de `SHA256SUMS.txt` no script de release para validar os artefatos distribuídos.

## [2.1.21] - 2026-06-02

### Corrigido

- Revisada a exposição de detalhes internos do estado Trial em logs, documentação e informações de suporte.

## [2.1.20] - 2026-06-02

### Alterado

- Aprimorado o armazenamento interno de estado da versão Trial.
- Ajustados logs do Trial para evitar exposição de detalhes internos do armazenamento.

## [2.1.19] - 2026-06-02

### Adicionado

- Tela Sobre com versão, edição, status Trial, caminhos de configuração/logs e informações de contato.
- Ação para copiar informações do produto para suporte.

## [2.1.18] - 2026-06-02

### Adicionado

- Script `build-release.ps1` para automatizar atualização de versão, testes, publicação e geração dos artefatos de release.

## [2.1.17] - 2026-06-02

### Adicionado

- Opção de ordenação da lista processada nos modos Original, Crescente e Decrescente.
- Modo Original mantido como padrão para preservar a ordem da entrada.
- Ordenação por nome com desempate numérico por número quando aplicável.

## [2.1.16] - 2026-06-02

### Adicionado

- Pré-validação visual da entrada antes do processamento.
- Lista de problemas por linha para tamanho não reconhecido, linha sem tamanho e limite de mais de 6 tamanhos.
- Destaque na numeração das linhas problemáticas para facilitar revisão antes de consumir processamento Trial.

## [2.1.15] - 2026-05-31

### Adicionado

- Sistema interno de logs diários em `%APPDATA%\ListForge\logs`.
- Registro técnico de falhas de importação, OCR, salvamento de configurações, processamento e Trial.
- Botão em Configurações para abrir a pasta de logs.
- Suporte a diagnóstico sem exibir stack trace técnico ao usuário final.

## [2.1.14] - 2026-05-31

### Alterado

- Refatorado o núcleo de processamento para separar parsing, montagem de saída, geração de JSON, importação JSON e helpers de arquivo.
- Mantida compatibilidade com a API pública de `ListProcessor`.
- Preservado o comportamento existente validado pelos testes automatizados.

## [2.1.13] - 2026-05-31

### Adicionado

- Projeto `ListForge.Tests` com testes automatizados para o núcleo de processamento.
- Cobertura para ordem original, quantidades, campos extras, meião, JSON, validação de entrada, tamanhos e importação de texto simples.

### Alterado

- Removidas as opções visíveis de tabulação como separador nas instruções de uso.

## [2.1.12] - 2026-05-31

### Adicionado

- Controle de tamanho da fonte das listas na tela de Configurações.
- Atalho `Ctrl` + scroll sobre entrada ou saída para ajustar a fonte dos editores.
- Persistência do tamanho da fonte em `config.json`, com limite entre 8 e 32 px.

## [2.1.11] - 2026-05-31

### Alterado

- O processamento agora preserva a ordem original das linhas de entrada.
- Adicionada build Trial separada, identificada no título/interface e limitada por créditos de processamento.
- Cada processamento concluído com sucesso consome 1 crédito no Trial; entradas inválidas, erros de validação e cancelamentos não consomem crédito.
- A build completa continua sem consumo de créditos e sem dependência do controle Trial.

## [2.1.9] - 2026-05-27

### Alterado

- Removido o campo `Socks` da geração de JSON.
- Mantido o meião apenas na lista organizada, sem exportação para o objeto `orders`.
- Atualizada a documentação para refletir que o JSON não inclui meião.
- Ajustada a seção de screenshots do README para exibir as imagens reais do repositório.

## [2.1.8] - 2026-05-27

### Corrigido

- Removida coluna vazia residual entre os tamanhos e o apelido quando a linha possui campos extras.
- Mantidas colunas vazias internas do grupo de tamanho, sem adicionar preenchimento final antes de apelido ou tipo sanguíneo.
- Corrigida a saída de entradas como `1,Amanda C.,(Cardoso),2-BLM,2-BLP` para terminar em `BLM,BLP,(Cardoso)`.

## [2.1.7] - 2026-05-27

### Corrigido

- Removida a coluna vazia de tipo sanguíneo quando a lista possui apenas apelido como campo extra.
- Mantida a ordem dos campos extras no final da saída: apelido primeiro e tipo sanguíneo somente quando existir terceira string.
- Corrigida a saída de linhas como `Amanda C.,1,BLM,BLP,(Cardoso)`, sem vírgulas extras antes ou depois do apelido.

## [2.1.6] - 2026-05-27

### Corrigido

- Ajustada a saída textual para tratar a segunda string como apelido e a terceira string como tipo sanguíneo.
- Quando houver campos extras, a lista organizada passa a reservar apelido e tipo sanguíneo no final da linha, nessa ordem.
- Evitado que uma linha com apenas apelido seja interpretada como tipo sanguíneo por falta da coluna final vazia.

## [2.1.5] - 2026-05-26

### Corrigido

- Ajustado o alinhamento de colunas para linhas que possuem apenas um grupo de tamanho.
- Mantida a separação por grupo quando a mesma linha original mistura tamanhos masculinos, femininos ou infantis.
- Corrigida a saída de listas em que linhas femininas isoladas estavam herdando uma coluna vazia inicial de linhas masculinas/femininas mistas.

## [2.1.4] - 2026-05-26

### Corrigido

- Ajustado o processamento para manter a largura global das colunas de tamanho em toda a lista processada.
- Corrigida a saída de linhas com menos tamanhos quando outras linhas da mesma lista exigem colunas adicionais do mesmo grupo.
- Preservadas colunas vazias antes de campos extras, como apelido ou observação, em saídas com grupos de tamanho assimétricos.

## [2.1.3] - 2026-05-26

### Corrigido

- Corrigida a expansão de quantidades repetidas dentro do mesmo grupo de tamanho.
- Ajustado o processamento para que entradas como `2-BLG,2-BLG` gerem linhas com os tamanhos lado a lado, em vez de multiplicar linhas de forma indevida.

## [2.1.2] - 2026-05-21

### Alterado

- Versão de referência para distribuição com artefatos versionados.
- Documentação oficial atualizada para apresentar o ListForge como produto desktop.
- Licença proprietária adicionada ao projeto.
