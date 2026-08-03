# Changelog

Todas as mudanças relevantes do ListForge serão documentadas neste arquivo.

O formato segue uma estrutura simples inspirada em Keep a Changelog, com entradas agrupadas por versão.

## [2.1.36] - 2026-07-30

### Adicionado

- Modo Forja opcional, com faíscas ao digitar na entrada, saída editável, JSON editável e campos editáveis das Configurações, além de aquecimento na janela do Editor e impacto visual ao processar.
- Botão principal passa de **Processar** para **Forjar** quando o Modo Forja está ativo.
- Efeitos visuais do Modo Forja simplificados com brilho no campo ativo, faíscas curtas, limite de partículas por camada, limpeza automática de efeitos e rajada pequena próxima ao botão ao forjar.
- Configurações individuais para ativar ou desativar calor, faíscas e impacto do Modo Forja.
- Persistência, importação e exportação das preferências do Modo Forja no `config.json`.
- Testes para garantir que o Modo Forja vem desativado por padrão e não altera o resultado do processamento.

## [2.1.35] - 2026-07-29

### Adicionado

- Opções **Importar configurações** e **Exportar configurações** na tela de Configurações, usando JSON versionado com preferências seguras do usuário.
- Pacote de suporte passa a incluir `lista-entrada.txt`, `lista-saida.txt` e `configuracoes.json`, além dos diagnósticos já existentes.
- Acesso ao pacote de suporte movido para o menu lateral, disponível em qualquer tela.
- Testes para importação/exportação de configurações, pacote de suporte com snapshot da sessão e reinicialização sem arquivo atual.

### Corrigido

- Nova execução do ListForge não restaura automaticamente o arquivo atual da sessão anterior.
- Campo legado de arquivo aberto é ignorado ao carregar configurações antigas e não é salvo novamente.

## [2.1.34] - 2026-07-29

### Alterado

- Saída organizada e prévia JSON agora podem ser editadas de forma protegida durante a sessão, com aplicação validada ou descarte para voltar ao último resultado válido.
- Importação e processamento de listas com cabeçalhos de peça reconhecidos preservam o tipo de peça correto no JSON, sem depender de uma linha fixa.
- Atualizado o ícone do aplicativo usado no executável e no instalador.
- A tela Sobre mantém o resultado da última verificação de atualização e exibe `Baixar agora` quando uma nova versão já foi encontrada.
- A verificação automática dentro do intervalo de 24 horas passa a mostrar um status útil, como aplicativo atualizado ou atualização disponível.
- Documentadas as etapas obrigatórias de tag versionada e publicação de Release no fluxo de distribuição.

### Testes

- Adicionados testes para preservação de seções por cabeçalho de peça, edição temporária da saída/JSON e referência do ícone oficial.

## [2.1.33] - 2026-07-24

### Adicionado

- Adicionado menu em `Extrair lista do link` para criar uma nova lista ou adicionar os registros extraídos à lista atual.
- Adicionados testes para extração por link, preservação de duplicidades e prévia sem consumo de crédito Trial.

### Alterado

- Meião e aplicação em lote de tamanho agora ficam disponíveis no Editor mesmo com Lista avançada desligada.
- Extração por link valida o conteúdo antes de substituir ou adicionar à entrada atual.

## [2.1.32] - 2026-07-24

### Adicionado

- Adicionado suporte a manifest HTTPS público (`update.json`) para verificação de atualizações sem depender da API do GitHub.

### Alterado

- Script de release passa a gerar `update.json` quando uma URL pública é informada.

## [2.1.31] - 2026-07-22

### Adicionado

- Criada pasta Release com artefatos prontos para anexar no GitHub Release.

### Alterado

- Movidos os controles de atualização manual para a tela Sobre, junto das informações de versão e suporte.

## [2.1.30] - 2026-07-22

### Corrigido

- Corrigido o binding do progresso de download de atualizações na tela Configurações para evitar erro ao abrir o aplicativo.

## [2.1.29] - 2026-07-22

### Adicionado

- Verificação manual e automática de atualizações pela Release estável mais recente do GitHub.
- Seção Atualizações nas Configurações, com versão instalada, tipo de distribuição, preferência de verificação ao iniciar, status e progresso.
- Download validado do instalador com SHA-256 antes de executar atualização na distribuição instalável.
- Testes automatizados para consulta de Release, seleção de asset, validação de hash, downloads parciais, cancelamento, tipo de distribuição e regras do instalador.

### Alterado

- Scripts de release passam a marcar o tipo de distribuição gerada e a validar `SHA256SUMS.txt` para publicação no GitHub.
- Instalador preserva atualização no mesmo local da instalação existente, sem criar instalação paralela por versão.

## [2.1.28] - 2026-07-10

### Alterado

- Melhorada a alternância da Lista avançada com switch animado no Editor.

## [2.1.27] - 2026-07-10

### Corrigido

- JSON agora mantém a quantidade junto ao tamanho nos campos de peça, usando o formato `quantidade-tamanho`, sem alterar a lista organizada.

## [2.1.26] - 2026-07-10

### Adicionado

- Botão Salvar avançado no modo avançado do Editor.
- Exportação conjunta da entrada, saída e JSON com o mesmo nome base.
- Opção de exportação avançada em arquivos soltos ou arquivo ZIP.
- Testes automatizados para validar nomes, ZIP, validações e preservação dos créditos Trial no Salvar avançado.

## [2.1.25] - 2026-07-10

### Adicionado

- Switch Lista avançada movido para o Editor, substituindo a opção da tela Configurações.
- Lista avançada passa a concentrar opções extras da lista e recursos avançados de JSON.

## [2.1.24] - 2026-07-09

### Adicionado

- Edição avançada do JSON para mapear a ordem dos tamanhos da lista para tipos de peça específicos.
- Configuração de ordem personalizada entre Manga Curta, Manga Longa, Short, Calça, Regata e Colete.
- Testes automatizados para validar mapeamento avançado, quantidades, ordenação, erros de configuração e preservação da saída textual.

### Alterado

- Simplificada a ativação dos recursos avançados em uma única opção chamada Lista avançada.
- Movida a seleção dos tipos de peça da tela Configurações para a barra lateral do Editor.
- Ocultados tipos de peça já escolhidos nas demais posições da lista avançada.

### Corrigido

- Corrigida duplicação de opções e perda de seleção nos seletores da lista avançada.
- Ajustada a lista avançada para manter no mesmo registro JSON os tamanhos do mesmo gênero.
- Ajustado o modo básico do JSON para distribuir tamanhos na ordem padrão dos campos de peça.

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

- Atualizada documentação técnica para refletir arquitetura atual, testes, CI, release, suporte e serviços internos.
- Pacote de suporte agora permite controlar inclusão de logs recentes e reforça aviso de revisão antes do envio.
- Separada a lógica de licença/Trial em uma camada de serviço, preparando evolução futura sem alterar o comportamento atual.
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

- Aprimorado o controle local da versão Trial sem alterar o fluxo do usuário.
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
