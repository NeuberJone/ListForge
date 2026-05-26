# ListForge

**ListForge** é uma aplicação desktop oficial para Windows, desenvolvida em **C#**, **.NET 8** e **WPF**, voltada para edição, padronização, organização e exportação de listas de produção.

O projeto foi criado para reduzir retrabalho em operações que recebem listas em formatos variados, com nomes, números, tamanhos e informações extras fora de padrão. A aplicação centraliza a preparação da lista, valida tamanhos configuráveis, organiza a saída textual e gera uma estrutura JSON pronta para integração com outros fluxos.

![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-2563EB?style=for-the-badge&logo=windows)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)
![WPF](https://img.shields.io/badge/UI-WPF-0F172A?style=for-the-badge)
![Version](https://img.shields.io/badge/version-2.1.4-16A34A?style=for-the-badge)

---

## Screenshots

As imagens abaixo demonstram o fluxo principal do ListForge. Para manter o repositório organizado, salve as capturas em `docs/screenshots/` usando os nomes sugeridos.

| Tela | Demonstra | Arquivo sugerido |
|---|---|---|
| Editor principal | Entrada da lista, saída processada e visão geral da aplicação | `docs/screenshots/01-editor-principal.png` |
| Processamento com JSON | Resultado textual e prévia JSON gerada | `docs/screenshots/02-json-preview.png` |
| Configurações | Separador padrão, tema, opções de JSON e pasta de saída | `docs/screenshots/03-configuracoes.png` |
| Grupos de tamanho | Configuração de tamanhos masculinos, femininos, infantis e meião | `docs/screenshots/04-grupos-de-tamanho.png` |

> Use dados fictícios nas capturas para evitar exposição de clientes, pedidos, nomes reais ou informações internas de produção.

## Visão geral

O ListForge trabalha como uma estação de preparação de listas. O usuário pode colar dados manualmente, abrir arquivos, extrair conteúdo de documentos, reconhecer texto em imagens por OCR, limpar separadores, processar os registros e salvar o resultado.

A interface é organizada em áreas de edição, saída, JSON, configurações e manual. As preferências do usuário são persistidas localmente e incluem separador padrão, modo de capitalização, pasta de saída, nome padrão da lista, tema visual e grupos de tamanho.

## Problema que o projeto resolve

Listas de produção costumam chegar por mensagens, planilhas, PDFs, documentos, imagens ou links de pedidos. Esses dados frequentemente precisam ser revisados antes de seguir para produção: nomes podem vir fora de ordem, tamanhos podem aparecer em formatos diferentes, quantidades podem estar misturadas com tamanhos e campos extras podem precisar acompanhar a linha final.

O ListForge resolve esse processo com uma ferramenta única para:

- padronizar linhas de entrada;
- validar tamanhos reconhecidos;
- separar nome, número, tamanhos e campos auxiliares;
- expandir quantidades por tamanho;
- organizar a saída de forma previsível;
- gerar texto e JSON;
- manter backups de arquivos sobrescritos.

## Principais recursos

- Editor de entrada com numeração de linhas.
- Painéis separados para entrada, saída e JSON.
- Abertura e salvamento de arquivos de texto.
- Busca, substituição e destaque de ocorrências.
- Limpeza de espaços ao redor do separador.
- Processamento com separador configurável.
- Capitalização em modo original, maiúsculo ou minúsculo.
- Aplicação em lote de tamanho e meião, útil para listas de uniformes esportivos e preenchimento de informações repetidas.
- Validação de tamanhos por grupos configuráveis.
- Geração de saída textual.
- Geração e cópia de JSON.
- Backups automáticos ao sobrescrever arquivos.
- Temas visuais selecionáveis.
- Configurações persistentes por usuário.

## Importação de arquivos

O núcleo de leitura de arquivos está em `Core/FileImporter.cs`. Os formatos reconhecidos pelo projeto são:

| Tipo | Extensões |
|---|---|
| Texto | `.txt`, `.csv` |
| PDF | `.pdf` |
| Word | `.docx`, `.doc` |
| Excel | `.xlsx`, `.xlsm`, `.xls` |
| Imagens | `.png`, `.jpg`, `.jpeg`, `.bmp`, `.tif`, `.tiff`, `.webp` |
| JSON por link | URLs `http://` ou `https://` |

Arquivos de texto são lidos com tentativas de codificação em UTF-8 com BOM, UTF-8, Windows-1252 e ISO-8859-1. PDFs são lidos com PdfPig. Documentos Word usam DocumentFormat.OpenXml. Planilhas usam ClosedXML.

## OCR para imagens

O OCR é feito com Tesseract em português e inglês (`por+eng`). A aplicação tenta reconhecer texto por linha de comando quando encontra `tesseract.exe` e usa o wrapper C# do Tesseract como alternativa interna.

O reconhecimento procura o Tesseract nesta ordem:

1. caminho definido na variável de ambiente `TESSERACT_CMD`;
2. pasta `tesseract` junto ao executável da aplicação;
3. instalações do sistema em `C:\Program Files\Tesseract-OCR` ou `C:\Program Files (x86)\Tesseract-OCR`.

A pasta `tesseract/tessdata` deve acompanhar builds distribuídos quando o reconhecimento por OCR for necessário.

## Processamento de listas

O processamento principal está em `Core/ListProcessor.cs`. Cada linha é interpretada em partes separadas pelo separador ativo. O algoritmo identifica:

- nome;
- número;
- um ou mais tamanhos;
- até dois campos extras;
- tamanhos com quantidade no formato `2-G`, `3-M` ou equivalente válido.

Após a leitura, as linhas são ordenadas por nome e número. A saída textual distribui os tamanhos por grupos reconhecidos e mantém campos extras quando presentes.

## Suporte a quantidades por tamanho

Tamanhos podem vir com quantidade usando o formato `quantidade-tamanho`.

Exemplo:

```text
ANA,10,2-G
BRUNO,7,M
CARLA,12,3-BLP
```

No processamento, quantidades maiores que uma unidade são expandidas em linhas equivalentes para a saída e para o JSON.

## Grupos de tamanho configuráveis

Os tamanhos ficam em `sizes.json` e são representados por `Models/SizeConfig.cs`. O padrão do sistema inclui quatro grupos:

| Grupo | Uso |
|---|---|
| Masculino | tamanhos base como `PP`, `P`, `M`, `G`, `GG`, `XG` |
| Feminino | tamanhos base combinados com prefixos, como `BLP` |
| Infantil | tamanhos numéricos e sufixos, como `8A` |
| Meião | opções como `JUVENIL`, `ADULTO` e `INFANTIL` |

Cada grupo permite configurar tamanhos base, prefixos e sufixos. O índice final de tamanhos é montado em `Core/SizeHelper.cs`.

## Separadores personalizados

O separador padrão é vírgula, mas pode ser alterado no editor ou nas configurações. O valor `\t`, `TAB` ou `tab` é tratado como tabulação.

O mesmo separador é usado para limpar espaços, interpretar a entrada e montar a saída textual.

## Geração de JSON

O ListForge gera uma prévia JSON com o objeto `orders`. A estrutura inclui campos como:

- `Name`;
- `Nickname`;
- `Number`;
- `BloodType`;
- `Gender`;
- `ShortSleeve`;
- `LongSleeve`;
- `Short`;
- `Pants`;
- `Tanktop`;
- `Vest`;
- `Socks`.

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

| Tema | Arquivo |
|---|---|
| ListForge Dark | `UI/Themes/DarkTheme.xaml` |
| ListForge Light | `UI/Themes/LightTheme.xaml` |
| SISBolt | `UI/Themes/SisBoltTheme.xaml` |

Os temas são carregados por `ResourceDictionary` e aplicados pela janela principal em tempo de execução.

## Estrutura do projeto

```text
ListForge/
├─ App.xaml
├─ App.xaml.cs
├─ GlobalUsings.cs
├─ ListForge.csproj
├─ README.md
├─ LICENSE.md
├─ Assets/
│  └─ logo.ico
├─ Config/
│  └─ ConfigManager.cs
├─ Core/
│  ├─ FileImporter.cs
│  ├─ ListProcessor.cs
│  └─ SizeHelper.cs
├─ Models/
│  ├─ AppConfig.cs
│  ├─ ParsedRow.cs
│  └─ SizeConfig.cs
├─ ViewModels/
│  ├─ MainViewModel.cs
│  └─ RelayCommand.cs
├─ UI/
│  ├─ Controls/
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

- `App.xaml` inicia a aplicação e declara recursos globais.
- `UI/Views` contém janelas e telas.
- `UI/Controls` contém controles reutilizáveis.
- `UI/Themes` contém os dicionários de estilo.
- `ViewModels/MainViewModel.cs` centraliza estado, comandos e integração entre UI, configuração e processamento.
- `Core/FileImporter.cs` concentra leitura de arquivos, OCR e normalização de textos importados.
- `Core/ListProcessor.cs` concentra interpretação, ordenação, geração de saída e JSON.
- `Core/SizeHelper.cs` concentra validação e montagem dos grupos de tamanho.
- `Config/ConfigManager.cs` gerencia configurações, tamanhos e backups.
- `Models` contém os objetos de configuração e linhas processadas.

## Dados de configuração

As configurações são salvas em uma pasta gravável por usuário. A aplicação tenta usar os seguintes locais, nessa ordem:

1. `%APPDATA%\ListForge`
2. pasta de dados de aplicativo do usuário
3. `%LOCALAPPDATA%\ListForge`
4. pasta base da aplicação
5. pasta atual do processo
6. pasta temporária do Windows

Arquivos principais:

| Arquivo ou pasta | Função |
|---|---|
| `config.json` | preferências gerais da aplicação |
| `sizes.json` | grupos de tamanho válidos |
| `backups/` | cópias automáticas de arquivos sobrescritos |

## Tesseract OCR

O projeto inclui binários do Tesseract na pasta `tesseract`. O arquivo `ListForge.csproj` marca esse conteúdo como `Content` com `CopyToOutputDirectory` em modo `PreserveNewest`, garantindo que os dados necessários ao OCR sejam copiados para a saída de build.

Idiomas incluídos em `tesseract/tessdata`:

- `por.traineddata`;
- `eng.traineddata`;
- `osd.traineddata`.

## Pré-requisitos para desenvolvimento

| Ferramenta | Recomendação |
|---|---|
| Sistema operacional | Windows 10 ou Windows 11 x64 |
| .NET SDK | 8.0 ou superior |
| IDE | Visual Studio 2022 ou editor compatível |
| Inno Setup | 6.x ou 7.x para gerar instalador |

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

## Publicação

O projeto está configurado para Windows x64 e versão `2.1.4`.

Publicação instalável:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o bin\Release\dist\2.1.4\ListForge-Installable
```

Publicação em arquivo único:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o bin\Release\dist\2.1.4\ListForge-Portable-OneFile
```

Instalador:

```text
installer\ListForge.iss
```

O script do Inno Setup usa a saída `bin\Release\dist\2.1.4\ListForge-Installable` e gera o instalador em:

```text
bin\Release\dist\2.1.4\Installer
```

## Dependências principais

| Dependência | Uso |
|---|---|
| Newtonsoft.Json | leitura e geração de JSON |
| Tesseract | OCR de imagens |
| PdfPig | extração de texto de PDFs |
| DocumentFormat.OpenXml | extração de texto de documentos Word |
| ClosedXML | extração de texto de planilhas Excel |
| Inno Setup | geração do instalador Windows |

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
ANA,14,BLG,JUVENIL
```

Saída textual com separador vírgula:

```text
ANA,14,BLG,JUVENIL
JOAO,10,G
MARIA,2,M
PEDRO,8,P
PEDRO,8,P
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
      "ShortSleeve": "BLG",
      "LongSleeve": "",
      "Short": "",
      "Pants": "",
      "Tanktop": "",
      "Vest": "",
      "Socks": "JUVENIL"
    }
  ],
  "unique_name_chars": "",
  "unique_nickname_chars": ""
}
```

## Boas práticas

- Revise os grupos de tamanho antes de processar listas com novos padrões.
- Use separadores consistentes na entrada.
- Prefira uma linha por pessoa ou item de produção.
- Use o formato `quantidade-tamanho` para quantidades repetidas.
- Mantenha a pasta `tesseract/tessdata` junto ao executável quando OCR for necessário.
- Gere uma saída nova em vez de sobrescrever arquivos sem revisar o backup.
- Atualize a versão em `ListForge.csproj` e `installer/ListForge.iss` antes de distribuir uma nova build.
- Não versione `bin/`, `obj/`, `.vs/`, instaladores gerados ou arquivos temporários.

## Roadmap

- Testes automatizados para `ListProcessor` e `SizeHelper`.
- Logs internos para erros de leitura e OCR.
- Prévia visual mais detalhada do JSON.
- Modelos configuráveis de exportação.
- Validações adicionais para arquivos de entrada.
- Ferramenta de diagnóstico para Tesseract.

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

Produto desktop oficial em desenvolvimento ativo e mantido como aplicação Windows para preparação e exportação de listas de produção.
