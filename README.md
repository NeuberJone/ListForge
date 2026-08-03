# ListForge

**ListForge** é uma aplicação desktop para Windows, desenvolvida em **C#**, **.NET 8** e **WPF**, voltada para edição, padronização, organização e exportação de listas de produção.

O projeto foi criado para reduzir retrabalho em operações que recebem listas em formatos variados, com nomes, números, tamanhos e informações extras fora de padrão. A aplicação centraliza a preparação de listas, valida tamanhos configuráveis, organiza a saída textual e gera uma estrutura JSON pronta para integração com outros fluxos.

![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-2563EB?style=for-the-badge\&logo=windows)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge\&logo=dotnet)
![WPF](https://img.shields.io/badge/UI-WPF-0F172A?style=for-the-badge)
![Version](https://img.shields.io/badge/version-2.1.38-16A34A?style=for-the-badge)
[![CI](https://github.com/NeuberJone/ListForge/actions/workflows/ci.yml/badge.svg)](https://github.com/NeuberJone/ListForge/actions/workflows/ci.yml)

---

## Em resumo

* Transforma listas despadronizadas em saídas organizadas e previsíveis.
* Interpreta nomes, números, tamanhos e campos auxiliares.
* Valida tamanhos configuráveis por grupos.
* Expande quantidades por tamanho, como `2-G` ou `3-M`.
* Importa conteúdo de texto, planilhas, PDFs, documentos, imagens e links JSON.
* Gera saída textual e prévia JSON.
* Mantém configurações por usuário e backups automáticos.
* Possui versão completa e build Trial com limite de processamentos.
* Verifica atualizações por manifest HTTPS público, com download validado por SHA-256.
* Inclui testes automatizados e testes de integração para proteger regras críticas do núcleo e do fluxo principal.
* Registra logs internos diários para suporte e diagnóstico.
* Possui tela Sobre com versão, edição, caminhos e informações de suporte.

## Público-alvo

O ListForge foi pensado para fluxos de produção que recebem listas de nomes, números, tamanhos e informações auxiliares em formatos variados, especialmente em operações de uniformes, sublimação, personalização, preparação de pedidos e ambientes onde listas precisam ser revisadas antes de seguir para produção.

O projeto também serve como demonstração técnica de uma aplicação desktop real, com interface WPF, arquitetura organizada, persistência de configurações, importação de múltiplos formatos, OCR, geração de JSON, instalador e controle de versão.

## Screenshots

As imagens abaixo demonstram o fluxo principal do ListForge.

### Editor principal

Entrada da lista, saída processada e visão geral da aplicação.

![Editor principal](docs/screenshots/01-editor-principal.png)

### Processamento com JSON

Resultado textual e prévia JSON gerada.

![Processamento com JSON](docs/screenshots/02-json-preview.png)

### Configurações

Separador padrão, tema, opções de JSON, pasta de saída e preferências de exibição.

![Configurações](docs/screenshots/03-configuracoes.png)

### Grupos de tamanho

Configuração de tamanhos masculinos, femininos, infantis e meião.

![Grupos de tamanho](docs/screenshots/04-grupos-de-tamanho.png)

## Visão geral

O ListForge trabalha como uma estação de preparação de listas. O usuário pode colar dados manualmente, abrir arquivos, extrair conteúdo de documentos, reconhecer texto em imagens por OCR, limpar separadores, processar os registros e salvar o resultado.

A interface é organizada em áreas de entrada, saída, prévia JSON, configurações e manual. As preferências do usuário são persistidas localmente e incluem separador padrão, modo de capitalização, pasta de saída, nome padrão da lista, perfis de trabalho, tema visual, tamanho da fonte dos editores, verificação de atualizações e grupos de tamanho.

## Problema que o projeto resolve

Listas de produção costumam chegar por mensagens, planilhas, PDFs, documentos, imagens ou links de pedidos. Esses dados frequentemente precisam ser revisados antes de seguir para produção: nomes podem vir fora de ordem, tamanhos podem aparecer em formatos diferentes, quantidades podem estar misturadas com tamanhos e campos extras podem precisar acompanhar a linha final.

O ListForge resolve esse processo com uma ferramenta única para:

* padronizar linhas de entrada;
* validar tamanhos reconhecidos;
* separar nome, número, tamanhos e campos auxiliares;
* expandir quantidades por tamanho;
* preservar ou organizar a saída conforme o fluxo de produção;
* gerar texto e JSON;
* manter backups de arquivos sobrescritos;
* reduzir retrabalho manual na preparação das listas.

## Principais recursos

### Edição e preparação

* Editor de entrada com numeração de linhas.
* Painéis separados para entrada, saída e JSON.
* Abertura e salvamento de arquivos de texto.
* Busca, substituição e destaque de ocorrências.
* Limpeza de espaços ao redor do separador.
* Capitalização em modo original, maiúsculo ou minúsculo.
* Tamanho de fonte ajustável para entrada, saída e prévia JSON.
* Atalho com `Ctrl` + scroll do mouse para aumentar ou diminuir a fonte dos editores.
* Switch **Lista avançada** no editor para ligar ou desligar recursos avançados sem sair da tela principal.
* Extração de lista por link com opções para criar nova lista ou adicionar à lista atual.

### Processamento

* Processamento com separador configurável.
* Pré-validação visual da entrada antes do processamento.
* Ordenação opcional da lista processada em modo Original, Crescente ou Decrescente.
* Validação de tamanhos por grupos configuráveis.
* Preservação de seções com diferentes tipos de peça quando a entrada traz cabeçalhos reconhecidos.
* Expansão de quantidades por tamanho.
* Aplicação em lote de tamanho e meião.
* Interpretação de até dois campos extras, como apelido e tipo sanguíneo.
* Preservação da ordem original de entrada.

### Exportação e segurança

* Geração de saída textual.
* Geração, cópia e prévia de JSON.
* **Salvar avançado** para exportar entrada, saída e JSON em conjunto.
* Backups automáticos ao sobrescrever arquivos.
* Logs internos diários para diagnóstico técnico.
* Configurações persistentes por usuário.
* Temas visuais selecionáveis.
* Verificação manual e automática de atualizações na distribuição instalável.
* Versão Trial com limite de processamentos concluídos com sucesso.

## Importação de arquivos

O núcleo de leitura de arquivos está em `Core/FileImporter.cs`. Os formatos reconhecidos pelo projeto são:

| Tipo          | Extensões                                                 |
| ------------- | --------------------------------------------------------- |
| Texto         | `.txt`, `.csv`                                            |
| PDF           | `.pdf`                                                    |
| Word          | `.docx`, `.doc`                                           |
| Excel         | `.xlsx`, `.xlsm`, `.xls`                                  |
| Imagens       | `.png`, `.jpg`, `.jpeg`, `.bmp`, `.tif`, `.tiff`, `.webp` |
| JSON por link | URLs `http://` ou `https://`                              |

Arquivos de texto são lidos com tentativas de codificação em UTF-8 com BOM, UTF-8, Windows-1252 e ISO-8859-1. PDFs são lidos com PdfPig. Documentos Word usam DocumentFormat.OpenXml. Planilhas usam ClosedXML.

## OCR para imagens

O OCR é feito com Tesseract em português e inglês (`por+eng`). A aplicação tenta reconhecer texto por linha de comando quando encontra `tesseract.exe` e usa o wrapper C# do Tesseract como alternativa interna.

O reconhecimento procura o Tesseract nesta ordem:

1. caminho definido na variável de ambiente `TESSERACT_CMD`;
2. pasta `tesseract` junto ao executável da aplicação;
3. instalações do sistema em `C:\Program Files\Tesseract-OCR` ou `C:\Program Files (x86)\Tesseract-OCR`.

A pasta `tesseract/tessdata` deve acompanhar builds distribuídos quando o reconhecimento por OCR for necessário.

## Processamento de listas

O processamento principal é exposto por `Core/ListProcessor.cs`, que funciona como fachada de compatibilidade para o restante da aplicação. A lógica interna fica separada em arquivos menores: `ListParser.cs`, `ListOutputBuilder.cs`, `JsonOrderBuilder.cs`, `JsonListImporter.cs` e `FileNameHelper.cs`.

Cada linha é interpretada em partes separadas pelo separador ativo. O algoritmo identifica:

* nome;
* número;
* um ou mais tamanhos;
* até dois campos extras: apelido e tipo sanguíneo;
* tamanhos com quantidade no formato `2-G`, `3-M` ou equivalente válido.

Após a leitura, as linhas mantêm a mesma ordem da entrada. A saída textual distribui os tamanhos por grupos reconhecidos, preserva colunas vazias internas quando necessário, compacta colunas finais antes dos campos extras e formata esses campos no final como apelido seguido de tipo sanguíneo quando esse terceiro campo existir.

Por padrão, o ListForge preserva a ordem original da lista informada. Caso uma ordenação adicional seja implementada ou habilitada no fluxo, ela deve ser tratada como uma opção explícita do usuário, sem alterar a responsabilidade principal do processamento: interpretar corretamente a entrada.

## Ordenação da lista

No painel Preparação da lista, a opção Ordenação controla a ordem usada depois da leitura da entrada e antes da montagem da Lista organizada e do JSON.

Opções disponíveis:

* Original: mantém a ordem digitada ou importada. É o padrão ao abrir o app.
* Crescente: ordena por nome de A-Z e, em nomes iguais, por número crescente.
* Decrescente: ordena por nome de Z-A e, em nomes iguais, por número decrescente.

Quando o número pode ser lido como valor numérico, a comparação é numérica. Assim, `2` vem antes de `10` no modo Crescente. Se o número não for numérico, o ListForge usa comparação textual como alternativa. A ordenação escolhida afeta a saída textual e a prévia/geração de JSON.

## Pré-validação da entrada

Antes de processar a lista, o ListForge faz uma pré-validação das linhas preenchidas. Quando encontra problemas, o processamento é interrompido e a aplicação mostra um resumo como:

```text
Linha 7: tamanho não reconhecido
Linha 12: sem tamanho
Linha 18: mais de 6 tamanhos
```

As linhas apontadas ficam destacadas na numeração da entrada para facilitar a revisão. Erros de pré-validação não consomem crédito da versão Trial, porque o processamento final não é executado.

## Prévia de impacto do processamento

O botão **Processar** abre uma prévia antes de executar o processamento final. Essa prévia usa a mesma pipeline oficial do processamento e mostra um resumo do impacto da lista atual: total de registros analisados, registros válidos, registros com possíveis problemas, registros inválidos, distribuição de tamanhos, distribuição de tipos de peça, destino previsto de saída, perfil de trabalho ativo, estado da Lista avançada e avisos não bloqueantes.

Se houver edição pendente na Lista organizada ou na Prévia JSON, o ListForge pede para aplicar, descartar ou cancelar antes de montar a prévia. A análise não consome crédito Trial. O crédito só é consumido quando o usuário confirma em **Processar agora** e o processamento termina com sucesso.

O botão **Processar rápido** mantém o comportamento direto anterior, sem abrir a prévia. Ele também respeita a mesma regra de Trial: erro de entrada, validação inválida, cancelamento e falhas anteriores à conclusão não consomem crédito.

## Suporte a quantidades por tamanho

Tamanhos podem vir com quantidade usando o formato `quantidade-tamanho`.

Exemplo:

```text
ANA,10,2-G
BRUNO,7,M
CARLA,12,3-BLP
```

No processamento, quantidades maiores que uma unidade são expandidas em linhas equivalentes para a saída e para o JSON.

## Versão Trial

A build Trial é gerada separadamente da versão completa e aparece como `ListForge Trial` no título/interface e no nome do executável.

O Trial possui limite de processamentos de listas. Cada processamento concluído com sucesso consome 1 crédito. Erro de entrada, validação inválida, falha antes do processamento final ou cancelamento do usuário não consomem crédito.

A versão completa não consome créditos e não depende do controle Trial. O limite padrão do Trial é 10 processamentos e pode ser ajustado pela variável de ambiente `LISTFORGE_TRIAL_PROCESSING_LIMIT`.

## Grupos de tamanho configuráveis

Os tamanhos ficam em `sizes.json` e são representados por `Models/SizeConfig.cs`. O padrão do sistema inclui quatro grupos:

| Grupo     | Uso                                                |
| --------- | -------------------------------------------------- |
| Masculino | tamanhos base como `PP`, `P`, `M`, `G`, `GG`, `XG` |
| Feminino  | tamanhos base combinados com prefixos, como `BLP`  |
| Infantil  | tamanhos numéricos e sufixos, como `8A`            |
| Meião     | opções como `JUVENIL`, `ADULTO` e `INFANTIL`       |

Cada grupo permite configurar tamanhos base, prefixos e sufixos. O índice final de tamanhos é montado em `Core/SizeHelper.cs`.

## Separadores personalizados

O separador padrão é vírgula, mas pode ser alterado no editor ou nas configurações para outro caractere simples usado no fluxo da lista.

O mesmo separador é usado para limpar espaços, interpretar a entrada e montar a saída textual.

## Testes automatizados

O projeto de testes fica em `ListForge.Tests` e cobre partes rápidas e determinísticas do núcleo, sem depender de OCR.

A suíte inclui:

* testes unitários do processamento, tamanhos, ordenação, importação, logs, busca/substituição, Trial/licença e serviços internos;
* testes de integração do fluxo principal sem abrir a UI WPF;
* testes da prévia de impacto, cobrindo análise sem consumo Trial e confirmação com consumo apenas após sucesso;
* testes de entradas grandes com 1.000 linhas, cobrindo validação, processamento, ordenação, expansão de quantidades e JSON;
* testes do atualizador com HTTP simulado, validação de assets, hashes, downloads parciais, cancelamento, tipo de distribuição e script do instalador.

Para rodar os testes na raiz do projeto:

```powershell
dotnet test
```

## Qualidade de código

O projeto usa `.editorconfig` para manter indentação, organização de `using`, preferências simples de C# e regras conservadoras dos analyzers do .NET.

Comandos recomendados antes de enviar alterações:

```powershell
dotnet format ListForge.slnx
dotnet test
```

## Logs internos

O ListForge cria logs diários para ajudar no suporte e diagnóstico de erros em versões distribuídas. Os arquivos ficam em:

```text
%APPDATA%\ListForge\logs
```

Na prática, a aplicação usa o diretório gravável resolvido por `ConfigManager.AppDir` e cria a subpasta `logs`. O nome do arquivo segue o padrão:

```text
listforge-YYYY-MM-DD.log
```

Os logs registram falhas técnicas de importação de arquivos, OCR, salvamento de configurações, processamento de listas, licença/Trial e exceções inesperadas da aplicação. As entradas incluem data/hora, nível, versão, edição, contexto, mensagem, exceção e stack trace quando houver.

Por padrão, o conteúdo completo das listas processadas não é registrado. Caminhos de arquivos podem aparecer no log quando ajudam no diagnóstico. A tela Configurações possui o botão **Abrir pasta de logs**.

## Tela Sobre

A tela Sobre exibe informações úteis para identificação da instalação e suporte:

* produto e versão atual, obtida da metadata do assembly;
* edição Completo ou Trial;
* status da versão Trial, com créditos restantes e limite de processamentos quando aplicável;
* campo Licenciado para, preparado para uso futuro;
* autor e contato;
* pasta de configuração e pasta de logs usadas pelo aplicativo;
* resumo curto de licença/propriedade;
* seção Atualizações, com verificação manual, preferência de verificação ao iniciar, status, botão de download quando há versão disponível e progresso.

Ela também possui ações para copiar as informações do produto para suporte, verificar atualizações, baixar a atualização disponível, gerar pacote de suporte, abrir a pasta de configuração e abrir a pasta de logs.

## Sessão e arquivo atual

Cada nova execução do ListForge começa como uma nova sessão, sem arquivo atual definido automaticamente. O usuário precisa abrir, importar ou salvar uma lista explicitamente para que um arquivo passe a ser tratado como atual naquela sessão.

As preferências permanentes continuam sendo carregadas normalmente, como tema, tamanho da fonte, separador padrão, Modo Forja, opções de processamento e atualização. O caminho do arquivo aberto anteriormente não é restaurado como arquivo ativo e não é salvo novamente no `config.json`.

## Modo Forja

O **Modo Forja** é um recurso visual opcional inspirado em aço, calor, brasas, faíscas e impacto. Quando ativado, o campo ativo ganha um brilho quente, e o editor adiciona faíscas curtas enquanto o usuário digita na entrada, na saída editável, na prévia JSON editável e nos campos editáveis das Configurações. Ao processar a lista, o botão recebe brilho, pulso e uma rajada pequena de faíscas próxima ao botão.

O recurso vem desativado por padrão e pode ser ligado em **Configurações > Modo Forja**. Também é possível controlar individualmente os efeitos:

* Calor;
* Faíscas;
* Impacto.

O efeito de calor acontece como uma sobreposição rápida na janela do Editor após processamento bem-sucedido. O Modo Forja não altera entrada, saída, JSON, importação, exportação, Trial ou qualquer regra de processamento. Quando desligado, nenhum efeito visual é exibido.

## Perfis de trabalho

Os **Perfis de trabalho** salvam conjuntos reutilizáveis de configurações para alternar rapidamente entre fluxos diferentes de lista.

Cada perfil guarda configurações de trabalho como separador, capitalização, ordenação do Editor, Lista avançada, ordem dos tipos de peça do JSON, tipo de Salvar avançado, pasta padrão de saída e nome padrão da lista.

O perfil protegido **Padrão** é criado automaticamente em instalações novas ou existentes. Em instalações atualizadas, ele parte das configurações atuais do usuário para preservar o comportamento anterior.

No Editor, o seletor **Perfil de trabalho** permite trocar rapidamente o perfil ativo. Em Configurações > Perfis de trabalho, é possível criar perfil, salvar alterações no perfil, descartar alterações, renomear, duplicar, excluir e restaurar o perfil Padrão.

Ao trocar de perfil, o ListForge aplica as configurações de trabalho sem apagar a entrada atual, a saída, a prévia JSON ou arquivos abertos. Se houver alterações não salvas no perfil ativo, o usuário pode salvar, descartar ou cancelar a troca.

Os perfis são incluídos na exportação/importação de configurações e no pacote de suporte por meio de `configuracoes.json`. Eles não armazenam conteúdo de listas, saída organizada, JSON real, licença, estado Trial, tokens, senhas ou chaves.

## Importar e exportar configurações

A tela **Configurações** possui as ações **Importar configurações** e **Exportar configurações**.

Ao exportar, o ListForge gera um arquivo JSON UTF-8 com estrutura versionada para diagnóstico, conferência ou backup manual de preferências.

O nome sugerido segue o formato:

```text
ListForge-Configuracoes-X.Y.Z-AAAA-MM-DD-HHmmss.json
```

O arquivo inclui preferências permitidas, como opções visuais, Modo Forja, processamento, Lista avançada, Perfis de trabalho, exportação avançada, atualização e tamanhos configurados.

Ao importar, o ListForge aplica apenas os campos permitidos desse JSON e atualiza a tela imediatamente. O arquivo atual, a entrada, a saída organizada e a prévia JSON da sessão aberta não são alterados.

Não são importados nem exportados: arquivo atual, conteúdo da entrada, saída organizada, JSON de listas, estado temporário de edição, dados internos de licença/Trial, tokens, senhas, chaves ou credenciais.

## Pacote de suporte

O menu lateral possui a ação **Gerar pacote de suporte**, que cria um arquivo `.zip` para diagnóstico técnico em qualquer tela.

O pacote inclui informações do produto, resumo seguro de configurações, tamanhos configurados, entrada atual, saída atual, logs recentes permitidos e uma exportação das configurações atuais. O menu lateral deixa a ação **Gerar pacote de suporte** disponível em qualquer tela.

Novos arquivos incluídos no ZIP:

```text
lista-entrada.txt
lista-saida.txt
configuracoes.json
```

Se não houver entrada ou saída na sessão atual, os arquivos correspondentes são incluídos vazios e o pacote não reutiliza conteúdo de sessões anteriores.

O pacote nunca inclui JSON de listas reais, arquivos externos de listas do usuário, dados internos de licença/Trial, tokens, senhas, chaves, build/dist ou repositório Git. Quando os logs são incluídos, o ListForge limita a seleção aos arquivos recentes permitidos.

Antes da geração, o ListForge avisa que logs podem conter caminhos de arquivos. Ao gerar o pacote, escolha a pasta de destino e revise o ZIP antes de enviar para suporte.

## Atualizações do aplicativo

A tela Sobre possui a seção **Atualizações**, com versão instalada, tipo de distribuição, opção **Verificar atualizações ao iniciar**, botão **Verificar agora**, botão **Baixar agora** quando uma atualização foi encontrada, status da última verificação e progresso de download.

Quando a verificação automática encontra uma nova versão, o status permanece visível na tela Sobre e o usuário pode baixar depois pelo botão **Baixar agora**, sem precisar verificar novamente. Se a última verificação concluiu que o ListForge já está na versão mais recente, o status mostra que o aplicativo está atualizado em vez de exibir apenas o aviso de intervalo de 24 horas.

A verificação usa um manifest HTTPS público (`update.json`) com a versão mais recente, URL do instalador e SHA-256 esperado. O endereço padrão aponta para o repositório público de releases do ListForge e pode ser ajustado pela variável de ambiente `LISTFORGE_UPDATE_API_URL` quando houver necessidade de teste ou ambiente controlado.

Na distribuição completa instalável, o ListForge pode verificar, baixar, validar e iniciar o instalador da nova versão. A validação exige SHA-256 informado pela Release ou pelo arquivo `SHA256SUMS.txt`; se a integridade não puder ser confirmada, o instalador não é executado. O download é feito primeiro como arquivo parcial e só fica pronto para execução depois da validação.

Nas versões portáteis e Trial, o ListForge não inicia instalador automaticamente. Quando existe uma versão nova, ele informa a disponibilidade e pode abrir a página da Release para o usuário baixar manualmente. Em desenvolvimento, a verificação automática não inicia instalador.

O instalador usa atualização no mesmo local da instalação existente, sem criar uma instalação paralela por versão. A verificação de atualização não altera créditos Trial e não participa do processamento das listas.

## Tamanho da fonte dos editores

O tamanho da fonte dos editores de Entrada / edição, Saída e Prévia JSON pode ser ajustado nas Configurações, na seção Exibição.

Também é possível alterar rapidamente pelo editor: posicione o mouse sobre a entrada ou saída, segure `Ctrl` e role o scroll do mouse. `Ctrl` + scroll para cima aumenta a fonte; `Ctrl` + scroll para baixo diminui. O valor é aplicado aos três editores ao mesmo tempo, respeita o intervalo de 8 a 32 px e é salvo em `config.json`.

## Lista avançada no editor

O Editor possui um switch animado **Lista avançada** na barra superior. Ele substitui a antiga opção de Configurações e permite ligar ou desligar recursos avançados sem sair da tela principal.

Com **Lista avançada** desligada, o ListForge mantém visíveis os controles principais de entrada, saída, preparação, processamento, cópia, salvamento e aplicação em lote de tamanho/meião. Ao ligar, o editor mostra opções extras, como a seleção avançada de tipos de peça para o JSON na barra lateral. Alternar essa opção não altera as regras de processamento por si só e não consome créditos Trial.

## Extrair lista do link

O botão **Extrair lista do link** abre um menu com duas opções:

* **Criar nova lista**: valida o link e substitui a entrada atual somente se a extração terminar com sucesso.
* **Adicionar à lista atual**: valida o link e adiciona os registros extraídos ao final da entrada atual, preservando duplicidades.

O link precisa começar com `http://` ou `https://` e retornar um JSON compatível. Falhas de URL, acesso, JSON inválido ou validação da lista não alteram a entrada atual e não consomem créditos Trial.

## Salvar avançado

Com **Lista avançada** ligada, o Editor mostra o botão **Salvar avançado** no rodapé. Ele exporta, de uma vez, o texto atual da entrada, a saída processada e o JSON atual, usando o mesmo nome base informado pelo usuário.

Antes de usar, processe a lista para revisar a saída. O botão não processa automaticamente e não consome créditos Trial.

Na tela **Configurações > Exportação avançada**, escolha o tipo de exportação:

* **Arquivos soltos**: gera `nome-entrada.txt`, `nome-saida.txt` e `nome.json` na pasta escolhida.
* **Arquivo ZIP**: gera `nome.zip` contendo somente `nome-entrada.txt`, `nome-saida.txt` e `nome.json`.

O nome base é sanitizado para evitar caracteres inválidos em arquivos. Se já existir algum destino com o mesmo nome, o ListForge usa uma variação versionada, preservando os arquivos anteriores.

## Geração de JSON

O ListForge gera uma prévia JSON com o objeto `orders`. A estrutura inclui campos como:

* `Name`;
* `Nickname`;
* `Number`;
* `BloodType`;
* `Gender`;
* `ShortSleeve`;
* `LongSleeve`;
* `Short`;
* `Pants`;
* `Tanktop`;
* `Vest`.

O meião é mantido na lista organizada, mas não é exportado como campo `Socks` no JSON.

Nos campos de peça do JSON, o tamanho sempre usa o formato `quantidade-tamanho`. Quando a entrada não traz quantidade explícita, o JSON usa quantidade `1`.

Exemplos:

```json
{
  "ShortSleeve": "1-PP",
  "LongSleeve": "2-M",
  "Tanktop": "3-P"
}
```

Essa regra vale somente para o JSON. A lista organizada mantém o formato textual do editor.

No modo básico, os tamanhos de cada linha seguem a ordem padrão dos campos do JSON: o primeiro tamanho vai para `ShortSleeve`, o segundo para `LongSleeve`, depois `Short`, `Pants`, `Tanktop` e `Vest`. Tamanhos do mesmo gênero ficam no mesmo registro; gêneros diferentes continuam sendo separados quando necessário.

Quando a lista importada possui cabeçalhos de peça reconhecidos, como `ShortSleeve`, `LongSleeve`, `Short`, `Pants`, `Tanktop` ou `Vest`, cada seção mantém o tipo de peça detectado. Isso permite que uma mesma lista tenha uma primeira seção de manga curta e uma segunda seção de manga longa, short, calça, regata ou colete sem reclassificar os registros anteriores.

## Edição protegida da saída e do JSON

Em **Configurações > Saída**, a opção **Permitir edição da saída e do JSON nesta sessão** libera edição manual da Lista organizada e da Prévia JSON.

Essa opção é temporária: ela começa sempre desativada ao abrir o ListForge e não é gravada em `config.json`. Com a opção desligada, saída e JSON permanecem somente leitura, mas seleção, cópia e rolagem continuam funcionando.

Quando a edição está ativa, use **Aplicar alterações** para validar o conteúdo editado e sincronizar novamente modelo, Lista organizada e JSON. Use **Descartar alterações** para voltar ao último estado válido. JSON inválido ou lista editada com erro não substituem o último estado válido e não consomem crédito Trial.

### Edição avançada do JSON

No switch da barra superior do Editor, a opção **Lista avançada** habilita os recursos avançados de JSON. Com ela ativa, a barra lateral mostra os seletores de tipos de peça usados para distribuir os tamanhos de cada linha entre os campos do JSON.

O primeiro tipo escolhido é aplicado ao primeiro tamanho encontrado, o segundo tipo ao segundo tamanho, e assim sucessivamente. Tipos já escolhidos deixam de aparecer nas demais posições. Essa opção é útil quando uma mesma linha possui tamanhos de peças diferentes. Quando os tamanhos pertencem ao mesmo gênero, o JSON mantém os campos no mesmo registro; a divisão em registros separados é usada para gêneros diferentes ou para quantidades expandidas.

Exemplo:

```text
JOAO,10,3-P,4-G,20-M
```

Ordem avançada:

```text
1. Regata
2. Manga Curta
3. Short
```

Resultado conceitual:

```text
3-P será usado como Regata
4-G será usado como Manga Curta
20-M será usado como Short
```

Quando a opção está desativada, o ListForge mantém o comportamento padrão. A saída textual continua seguindo o fluxo normal; a ordem avançada altera apenas a montagem dos campos do JSON.

O JSON é encapsulado com metadados básicos como `title`, `order_number`, `client_name`, `unique_name_chars` e `unique_nickname_chars`.

## Backups automáticos

Antes de sobrescrever um arquivo existente, a aplicação cria uma cópia na pasta de backups. O nome do backup usa o nome do arquivo original e um carimbo de data e hora.

Exemplo:

```text
lista_20260525_143012.txt
```

Os backups ficam no diretório configurado por `ConfigManager.BackupDir`.

## Temas visuais

O ListForge possui três temas:

| Tema            | Arquivo                       |
| --------------- | ----------------------------- |
| ListForge Dark  | `UI/Themes/DarkTheme.xaml`    |
| ListForge Light | `UI/Themes/LightTheme.xaml`   |
| SISBolt         | `UI/Themes/SisBoltTheme.xaml` |

Os temas são carregados por `ResourceDictionary` e aplicados pela janela principal em tempo de execução.

## Estrutura do projeto

```text
ListForge/
├─ .editorconfig
├─ .github/
│  └─ workflows/
│     └─ ci.yml
├─ App.xaml
├─ App.xaml.cs
├─ build-release.ps1
├─ create-github-release.ps1
├─ GlobalUsings.cs
├─ ListForge.csproj
├─ ListForge.slnx
├─ README.md
├─ LICENSE.md
├─ CHANGELOG.md
├─ Assets/
│  └─ logo.ico
├─ Config/
│  └─ ConfigManager.cs
├─ Core/
│  ├─ AppLogger.cs
│  ├─ FileImporter.cs
│  ├─ FileNameHelper.cs
│  ├─ JsonPieceMappingOptions.cs
│  ├─ JsonListImporter.cs
│  ├─ JsonOrderBuilder.cs
│  ├─ ListOutputBuilder.cs
│  ├─ ListParser.cs
│  ├─ ListProcessor.cs
│  ├─ ListRowSorter.cs
│  ├─ ListSortMode.cs
│  ├─ OperationResult.cs
│  ├─ PieceTypeMapper.cs
│  ├─ SizeHelper.cs
│  ├─ TextSearchHelper.cs
│  └─ TrialManager.cs
├─ Models/
│  ├─ AppConfig.cs
│  ├─ DistributionKind.cs
│  ├─ ParsedRow.cs
│  ├─ SizeConfig.cs
│  └─ UpdateReleaseInfo.cs
├─ Services/
│  ├─ AboutService.cs
│  ├─ AdvancedSaveService.cs
│  ├─ DistributionInfoService.cs
│  ├─ FileImportService.cs
│  ├─ FolderService.cs
│  ├─ GitHubUpdateService.cs
│  ├─ ILicenseService.cs
│  ├─ JsonPieceMappingService.cs
│  ├─ LocalTrialLicenseService.cs
│  ├─ OutputExportService.cs
│  ├─ ProcessingWorkflowService.cs
│  ├─ SupportPackageService.cs
│  ├─ UpdateInstallerService.cs
│  └─ UpdateProcessLauncher.cs
├─ ListForge.Tests/
│  ├─ AdvancedSaveServiceTests.cs
│  ├─ FileImporterTests.cs
│  ├─ FileImportServiceTests.cs
│  ├─ AppLoggerTests.cs
│  ├─ JsonPieceMappingTests.cs
│  ├─ LargeInputTests.cs
│  ├─ ListForge.Tests.csproj
│  ├─ LocalTrialLicenseServiceTests.cs
│  ├─ MainFlowIntegrationTests.cs
│  ├─ ListProcessorTests.cs
│  ├─ OperationResultTests.cs
│  ├─ OutputExportServiceTests.cs
│  ├─ SupportPackageServiceTests.cs
│  ├─ TextSearchHelperTests.cs
│  ├─ TrialManagerTests.cs
│  ├─ UpdateServiceTests.cs
│  └─ SizeHelperTests.cs
├─ ViewModels/
│  ├─ AsyncRelayCommand.cs
│  ├─ MainViewModel.cs
│  └─ RelayCommand.cs
├─ UI/
│  ├─ Controls/
│  │  ├─ AnimatedToggleSwitch.xaml
│  │  ├─ LineNumberedTextBox.cs
│  │  ├─ SegmentedControl.cs
│  │  └─ SegmentedControl.xaml
│  ├─ Themes/
│  │  ├─ DarkTheme.xaml
│  │  ├─ LightTheme.xaml
│  │  └─ SisBoltTheme.xaml
│  └─ Views/
│     ├─ EditorView.xaml
│     ├─ EditorView.xaml.cs
│     ├─ InputDialog.xaml.cs
│     ├─ MainWindow.xaml
│     ├─ MainWindow.xaml.cs
│     ├─ ManualView.xaml
│     ├─ ManualView.xaml.cs
│     ├─ SettingsView.xaml
│     └─ SettingsView.xaml.cs
├─ installer/
│  └─ ListForge.iss
└─ tesseract/
   ├─ tesseract.exe
   ├─ tessdata/
   │  ├─ eng.traineddata
   │  ├─ osd.traineddata
   │  └─ por.traineddata
   └─ bibliotecas nativas do Tesseract
```

## Arquitetura

O projeto segue uma organização simples baseada em WPF e MVVM:

* `App.xaml` inicia a aplicação e declara recursos globais.
* `UI/Views` contém janelas e telas.
* `UI/Controls` contém controles reutilizáveis.
* `UI/Themes` contém os dicionários de estilo.
* `ViewModels/MainViewModel.cs` coordena estado, comandos e integração entre UI, configuração e processamento.
* `.github/workflows/ci.yml` valida automaticamente restore, build e testes em Windows a cada push ou pull request para `main`.
* `build-release.ps1` automatiza a geração de artefatos versionados e marca a distribuição como instalável, portátil ou Trial.
* `create-github-release.ps1` valida artefatos locais, exige `SHA256SUMS.txt` e prepara a publicação manual ou via GitHub CLI.
* `test-release.ps1` valida os artefatos locais de uma versão, confere hashes, nomes versionados, release notes e manifest de atualização.
* `docs/SMOKE_TEST.md` descreve o roteiro manual de smoke test antes de publicar uma release.
* `TestAssets/Samples` contém listas de exemplo para validar fluxo principal, erro de entrada, Lista avançada, JSON, dados completos e volume manual.
* `Services` contém serviços extraídos do ViewModel para importação, exportação, processamento, licença, suporte, perfis de trabalho, informações da tela Sobre e abertura de pastas.
* `Services/GitHubUpdateService.cs`, `Services/UpdateInstallerService.cs`, `Services/UpdateProcessLauncher.cs` e `Services/DistributionInfoService.cs` concentram consulta de atualização por manifest/GitHub, validação de instalador, abertura segura de processos e identificação da distribuição atual.
* `Services/ILicenseService.cs` e `Services/LocalTrialLicenseService.cs` separam a lógica de licença/Trial do fluxo principal, preservando o comportamento local atual.
* `Services/WorkProfileService.cs` gerencia criação, validação, aplicação e persistência dos Perfis de trabalho.
* `Core/FileImporter.cs` concentra leitura de arquivos, OCR e normalização de textos importados.
* `Core/OperationResult.cs` padroniza retornos de operações internas, separando mensagem ao usuário, detalhe técnico, exceção e código de erro.
* `Services/FileImportService.cs`, `Services/OutputExportService.cs`, `Services/AdvancedSaveService.cs`, `Services/ProcessingWorkflowService.cs`, `Services/ProcessingPreviewService.cs` e `Services/SupportPackageService.cs` retornam resultados padronizados ou objetos de fluxo para facilitar testes, mensagens amigáveis e logging técnico.
* `Core/AppLogger.cs` registra logs internos diários para suporte e diagnóstico.
* `Core/ListProcessor.cs` funciona como fachada de compatibilidade para as chamadas públicas de processamento.
* `Core/ListParser.cs` concentra separadores, limpeza por separador, parsing de linha e preservação da ordem de entrada.
* `Core/ListOutputBuilder.cs` concentra a explosão de tamanhos, distribuição de grupos, meião e montagem da saída textual.
* `Core/JsonOrderBuilder.cs` concentra a montagem de `orders`, prévia JSON, exportação JSON e aplicação da ordem avançada de tipos de peça.
* `Core/JsonListImporter.cs` concentra a extração de lista textual a partir de JSON.
* `Core/PieceTypeMapper.cs` e `Services/JsonPieceMappingService.cs` concentram os tipos de peça disponíveis, normalização da ordem personalizada e validação do mapeamento avançado do JSON.
* `Core/FileNameHelper.cs` concentra sanitização de nomes e caminhos versionados.
* `Core/SizeHelper.cs` concentra validação e montagem dos grupos de tamanho.
* `Core/TextSearchHelper.cs` concentra busca e substituição de texto usada pelo editor.
* `Config/ConfigManager.cs` gerencia configurações, tamanhos, backups e caminhos graváveis.
* `Models` contém os objetos de configuração e linhas processadas.
* `ListForge.Tests` contém testes unitários, testes de integração e cobertura de entradas grandes.

## Decisões técnicas

* **WPF** foi escolhido para entregar uma aplicação desktop nativa para Windows.
* **MVVM** organiza a separação entre interface, estado e comandos.
* A API pública de processamento é mantida em `Core/ListProcessor.cs`, enquanto as responsabilidades internas são separadas por área para reduzir acoplamento e facilitar testes.
* A leitura de arquivos e OCR foi separada em `Core/FileImporter.cs`, reduzindo acoplamento com a tela principal.
* As configurações são salvas em uma pasta gravável por usuário, evitando depender da pasta do executável.
* Os tamanhos são configuráveis via `sizes.json`, permitindo adaptação a diferentes padrões de produção.
* A camada de licença foi organizada para separar a lógica de Trial do fluxo principal e preparar evolução futura sem alterar o comportamento atual.
* A verificação de atualizações usa manifest HTTPS público, valida o instalador por SHA-256 e respeita o tipo de distribuição antes de iniciar qualquer instalador.
* O editor com numeração de linhas foi implementado como controle reutilizável em `UI/Controls/LineNumberedTextBox.cs`.
* O logging interno usa arquivos locais diários, sem dependências externas, e falhas ao escrever logs são ignoradas de forma segura.

## Dados de configuração

As configurações são salvas em uma pasta gravável por usuário. A aplicação tenta usar os seguintes locais, nessa ordem:

1. `%APPDATA%\ListForge`
2. pasta de dados de aplicativo do usuário
3. `%LOCALAPPDATA%\ListForge`
4. pasta base da aplicação
5. pasta atual do processo
6. pasta temporária do Windows

Arquivos principais:

| Arquivo ou pasta   | Função                                      |
| ------------------ | ------------------------------------------- |
| `config.json`      | preferências gerais da aplicação            |
| `sizes.json`       | grupos de tamanho válidos                   |
| `backups/`         | cópias automáticas de arquivos sobrescritos |
| `logs/`            | logs internos diários para diagnóstico      |

## Tesseract OCR

O projeto inclui binários do Tesseract na pasta `tesseract`. O arquivo `ListForge.csproj` marca esse conteúdo como `Content` com `CopyToOutputDirectory` em modo `PreserveNewest`, garantindo que os dados necessários ao OCR sejam copiados para a saída de build.

Idiomas incluídos em `tesseract/tessdata`:

* `por.traineddata`;
* `eng.traineddata`;
* `osd.traineddata`.

## Pré-requisitos para desenvolvimento

| Ferramenta          | Recomendação                            |
| ------------------- | --------------------------------------- |
| Sistema operacional | Windows 10 ou Windows 11 x64            |
| .NET SDK            | 8.0 ou superior                         |
| IDE                 | Visual Studio 2022 ou editor compatível |
| Inno Setup          | 6.x ou 7.x para gerar instalador        |

## Como executar em desenvolvimento

Na raiz do projeto:

```powershell
dotnet restore
dotnet build
dotnet run
```

Executável gerado em Debug:

```text
bin\Debug\net8.0-windows\ListForge.exe
```

## Integração contínua

O repositório possui workflow de CI em `.github/workflows/ci.yml`. Ele roda em `windows-latest` para push e pull request na branch `main`.

O workflow executa:

```powershell
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

Esse fluxo não gera instalador, onefile ou artefatos de release.

## Smoke test de release

Antes de publicar uma versão, use o roteiro em `docs/SMOKE_TEST.md`. Ele combina validação automática dos artefatos com uma conferida manual curta no aplicativo.

Validação automática:

```powershell
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
.\test-release.ps1 -Version X.Y.Z
```

O script `test-release.ps1` confere os executáveis versionados, a pasta `Release`, `SHA256SUMS.txt`, `RELEASE_NOTES_X.Y.Z.txt`, hashes dos artefatos principais e `update.json`. Para uma build local sem manifest de atualização, use `-SkipUpdateManifest`.

As entradas de teste ficam em `TestAssets\Samples` e cobrem lista válida simples, erro de entrada, Lista avançada, dados completos e uma base para teste manual de volume.

## Build e distribuição

O projeto está configurado para Windows x64 e versão `2.1.38`.

### Script de release

Para reduzir inconsistências entre versão do projeto, instalador, documentação e artefatos finais, use o script de release na raiz do repositório:

```powershell
.\build-release.ps1 -Version X.Y.Z
```

O script:

* atualiza versões em `ListForge.csproj`, `installer/ListForge.iss` e trechos de build/distribuição do `README.md`;
* roda `dotnet restore`, `dotnet build`, `dotnet test` e `dotnet build -c Release`;
* publica a versão instalável;
* publica o onefile oficial/completo;
* publica o onefile Trial;
* gera o instalador com Inno Setup;
* gera `SHA256SUMS.txt` com os hashes dos artefatos principais;
* cria a pasta `Release` com os arquivos prontos para anexar no GitHub;
* coloca todos os artefatos em `bin\Release\dist\X.Y.Z`.

Por padrão, o script não sobrescreve uma pasta de versão já existente. Para recriar somente a pasta da versão atual, use `-Force`. Ele nunca apaga o `dist` inteiro nem pastas de versões anteriores.

Se o Inno Setup não estiver em um caminho comum, informe o compilador manualmente:

```powershell
.\build-release.ps1 -Version X.Y.Z -InnoSetupPath "C:\Program Files\Inno Setup 7\ISCC.exe"
```

Para gerar também o `update.json` da fonte pública de atualização, informe a URL base onde os arquivos serão publicados:

```powershell
.\build-release.ps1 -Version X.Y.Z -ReleaseBaseUrl "https://pub-62303cd1120248b08beb3454fe0c6316.r2.dev"
```

### Comandos manuais

Publicação instalável:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:ListForgeDistribution=Installed -p:DebugType=None -p:DebugSymbols=false -o bin\Release\dist\2.1.38\ListForge-Installable
```

Publicação em arquivo único:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:ListForgeDistribution=PortableOneFile -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o bin\Release\dist\2.1.38\ListForge-Portable-OneFile
```

Publicação Trial em arquivo único:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:ListForgeDistribution=TrialPortableOneFile -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:DefineConstants=TRIAL_BUILD -p:DebugType=None -p:DebugSymbols=false -o bin\Release\dist\2.1.38\ListForge-Trial-OneFile
```

Instalador:

```text
installer\ListForge.iss
```

O script do Inno Setup usa a saída `bin\Release\dist\2.1.38\ListForge-Installable` e gera o instalador em:

```text
bin\Release\dist\2.1.38\Installer
```

Após confirmar os artefatos obrigatórios, o script gera:

```text
bin\Release\dist\2.1.38\SHA256SUMS.txt
```

Esse arquivo lista os checksums SHA256 dos executáveis principais usando caminhos relativos à pasta da versão.

O script também cria:

```text
bin\Release\dist\2.1.38\Release
```

Essa pasta contém os arquivos planos para anexar no GitHub Release:

* `ListForge-Setup-X.Y.Z.exe`;
* `ListForge-Trial-vX.Y.Z.exe`;
* `ListForge-vX.Y.Z.exe`;
* `SHA256SUMS.txt`;
* `RELEASE_NOTES_X.Y.Z.txt`;
* `update.json`, quando uma URL pública for informada ao script de release.

Para que a verificação de atualização instalada funcione sem depender da API do GitHub, publique também o `update.json` em uma URL HTTPS acessível. O instalador deve manter o nome exato `ListForge-Setup-X.Y.Z.exe`, com SHA-256 correspondente no manifest ou em `SHA256SUMS.txt`.

## Publicação de release no GitHub

Antes de publicar uma release no GitHub, gere e confira a distribuição local:

```powershell
.\build-release.ps1 -Version X.Y.Z
```

Depois confira os artefatos em `bin\Release\dist\X.Y.Z` e os anexos prontos em `bin\Release\dist\X.Y.Z\Release`.

Artefatos esperados:

* `ListForge-Installable\ListForge.exe`;
* `ListForge-Portable-OneFile\ListForge-vX.Y.Z.exe`;
* `ListForge-Trial-OneFile\ListForge-Trial-vX.Y.Z.exe`;
* `Installer\ListForge-Setup-X.Y.Z.exe`;
* `SHA256SUMS.txt`;
* `Release\ListForge-Setup-X.Y.Z.exe`;
* `Release\ListForge-Trial-vX.Y.Z.exe`;
* `Release\ListForge-vX.Y.Z.exe`;
* `Release\SHA256SUMS.txt`;
* `Release\update.json`, quando houver URL pública de atualização.

O fluxo recomendado é criar uma tag versionada depois que a distribuição local estiver validada. A tag deve seguir exatamente o padrão `vX.Y.Z`, apontar para o commit que gerou os artefatos e ser enviada ao repositório remoto antes da publicação da Release:

```powershell
git tag vX.Y.Z
git push origin vX.Y.Z
```

Em seguida, crie a Release no GitHub usando essa mesma tag `vX.Y.Z`. A Release deve ter título `ListForge X.Y.Z`, notas revisadas com base em `Release\RELEASE_NOTES_X.Y.Z.txt` e anexos oficiais vindos somente de `bin\Release\dist\X.Y.Z\Release`:

* `Release\ListForge-Setup-X.Y.Z.exe`;
* `Release\ListForge-Trial-vX.Y.Z.exe`;
* `Release\ListForge-vX.Y.Z.exe`;
* `Release\SHA256SUMS.txt`;
* `Release\RELEASE_NOTES_X.Y.Z.txt`;
* `Release\update.json`, quando houver manifest público de atualização.

Se usar um servidor próprio ou R2 para atualização automática, publique também os arquivos necessários dessa pasta na URL configurada pelo aplicativo, mantendo os mesmos nomes usados no manifest.

O script auxiliar abaixo valida os artefatos locais, preserva `RELEASE_NOTES_X.Y.Z.txt` quando ele já estiver preenchido, inclui `update.json` quando existir e mostra os comandos de tag/publicação sem criar uma Release automaticamente:

```powershell
.\create-github-release.ps1 -Version X.Y.Z
```

Para publicar usando GitHub CLI, revise os dados exibidos, confirme que a tag já existe e execute explicitamente:

```powershell
.\create-github-release.ps1 -Version X.Y.Z -Create
```

O script não armazena tokens nem credenciais. Se usar `gh`, ele depende apenas da autenticação local já configurada.

## Dependências principais

| Dependência            | Uso                                  |
| ---------------------- | ------------------------------------ |
| Newtonsoft.Json        | leitura e geração de JSON            |
| Tesseract              | OCR de imagens                       |
| PdfPig                 | extração de texto de PDFs            |
| DocumentFormat.OpenXml | extração de texto de documentos Word |
| ClosedXML              | extração de texto de planilhas Excel |
| Inno Setup             | geração do instalador Windows        |

## Fluxo básico de uso

1. Abra o ListForge.
2. Cole uma lista ou abra um arquivo compatível.
3. Ajuste o separador de entrada quando necessário.
4. Use a limpeza de espaços para padronizar a entrada.
5. Aplique tamanho ou meião em lote, se necessário.
6. Clique em processar.
7. Revise a saída textual.
8. Gere ou copie o JSON quando a área estiver habilitada.
9. Salve a saída no local desejado.

## Exemplos de entrada e saída

Entrada simples:

```text
JOAO,10,G
MARIA,2,M
PEDRO,8,2-P
ANA,14,BLG
```

Saída textual com separador vírgula:

```text
JOAO,10,G
MARIA,2,M
PEDRO,8,P
PEDRO,8,P
ANA,14,BLG
```

Prévia JSON simplificada:

```json
{
  "title": "List",
  "order_number": 0,
  "client_name": "",
  "orders": [
    {
      "Name": "ANA",
      "Nickname": "",
      "Number": "14",
      "BloodType": "",
      "Gender": "FE",
      "ShortSleeve": "1-BLG",
      "LongSleeve": "",
      "Short": "",
      "Pants": "",
      "Tanktop": "",
      "Vest": ""
    }
  ],
  "unique_name_chars": "",
  "unique_nickname_chars": ""
}
```

## Boas práticas

* Revise os grupos de tamanho antes de processar listas com novos padrões.
* Use separadores consistentes na entrada.
* Prefira uma linha por pessoa ou item de produção.
* Use o formato `quantidade-tamanho` para quantidades repetidas.
* Mantenha a pasta `tesseract/tessdata` junto ao executável quando OCR for necessário.
* Gere uma saída nova em vez de sobrescrever arquivos sem revisar o backup.
* Atualize a versão em `ListForge.csproj` e `installer/ListForge.iss` antes de distribuir uma nova build.
* Não versione `bin/`, `obj/`, `.vs/`, instaladores gerados ou arquivos temporários.

## Roadmap

* Logs internos para erros de leitura e OCR.
* Prévia visual mais detalhada do JSON.
* Modelos configuráveis de exportação.
* Validações adicionais para arquivos de entrada.
* Ferramenta de diagnóstico para Tesseract.

## Changelog

As alterações relevantes por versão são documentadas em `CHANGELOG.md`.

## Licença

O ListForge é um software proprietário desenvolvido por Neuber Jone.

Este repositório é disponibilizado publicamente apenas para fins de portfólio, avaliação técnica e demonstração. O acesso ao código-fonte não concede permissão para uso comercial, uso interno em empresas, cópia, modificação, redistribuição, revenda, hospedagem, criação de derivados ou exploração comercial do software.

O uso comercial, implantação privada, distribuição de executáveis, licenciamento mensal, suporte, manutenção ou customização do ListForge dependem de autorização prévia e de uma licença ou contrato comercial específico.

A aquisição de uma licença comercial não transfere a propriedade intelectual, o código-fonte, a marca, a identidade visual ou os direitos de exploração comercial do software, salvo acordo escrito em sentido contrário.

Consulte também o arquivo `LICENSE.md`.

## Autor

Desenvolvido por **Neuber Jone**.

## Status

Produto desktop em desenvolvimento ativo, mantido como aplicação Windows para preparação, padronização e exportação de listas de produção.
