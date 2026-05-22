# ListForge

> Organize listas de pedidos, valide tamanhos, importe arquivos e gere saídas prontas para uso em poucos cliques.

**ListForge** é um aplicativo desktop para Windows feito em **C# + WPF**. Ele nasceu para transformar listas bagunçadas em listas organizadas, revisáveis e exportáveis, com suporte a temas, importação de múltiplos formatos e geração de JSON.

![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-2563EB?style=for-the-badge&logo=windows)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)
![WPF](https://img.shields.io/badge/UI-WPF-0F172A?style=for-the-badge)
![Version](https://img.shields.io/badge/version-2.1.2-16A34A?style=for-the-badge)

---

## Sumário

- [Sobre o Projeto](#sobre-o-projeto)
- [Principais Recursos](#principais-recursos)
- [Fluxo de Uso](#fluxo-de-uso)
- [Temas](#temas)
- [Formatos Suportados](#formatos-suportados)
- [Instalação e Execução](#instalação-e-execução)
- [Gerando Versões de Distribuição](#gerando-versões-de-distribuição)
- [Criando Instalador](#criando-instalador)
- [Configurações e Dados Locais](#configurações-e-dados-locais)
- [Estrutura do Projeto](#estrutura-do-projeto)
- [Stack Técnica](#stack-técnica)
- [Roadmap](#roadmap)

---

## Sobre o Projeto

O **ListForge** foi criado para acelerar o trabalho de quem recebe listas em formatos variados e precisa transformar esse conteúdo em uma saída limpa, padronizada e confiável.

Ele ajuda em tarefas como:

- limpar listas copiadas de mensagens, planilhas, PDFs ou imagens;
- separar nomes, tamanhos e quantidades;
- adicionar tamanho ou meião em lote antes de processar;
- validar tamanhos conhecidos;
- organizar a saída em ordem previsível;
- gerar prévia JSON;
- manter backups automáticos das listas editadas;
- alternar entre temas visuais para diferentes ambientes de uso.

Esta versão é uma reimplementação em **C# / WPF** de uma versão anterior feita em Python/Tkinter.

---

## Principais Recursos

### Editor de listas

- Editor com numeração de linhas.
- Entrada e saída lado a lado.
- Atalhos e comandos rápidos.
- Busca e substituição.
- Destaque de resultado encontrado.
- Adição de tamanho e/ou meião em todas as linhas ou na seleção atual.
- ComboBox de meião alimentado pelos tamanhos cadastrados nas configurações.
- Caixas de seleção para controlar se o botão aplica meião, tamanho, ou ambos.
- Botões para limpar, copiar e salvar saídas.

### Processamento

- Separador configurável.
- Capitalização configurável.
- Organização automática da lista.
- Tratamento de meião em coluna própria, acompanhando a primeira linha do kit.
- Validação de linhas inválidas.
- Navegação até a linha com erro.
- Geração de saída em texto.
- Geração de prévia JSON.

### Configurações

- Pasta padrão de saída.
- Nome padrão de lista.
- Separador padrão.
- Modo padrão de capitalização.
- Controle de exibição da aba JSON.
- Controle de botões de JSON.
- Grupos de tamanhos editáveis: masculino, feminino, infantil e meião.

### Segurança de edição

- Backup automático antes de sobrescrever arquivos existentes.
- Configurações salvas por usuário.
- Fallback de diretório de configuração caso `%APPDATA%` esteja indisponível.

---

## Fluxo de Uso

1. Abra ou cole uma lista no painel **Entrada / edição**.
2. Ajuste o separador, se necessário.
3. Clique em **Processar**.
4. Revise a saída no painel **Saída**.
5. Copie, salve ou gere JSON.
6. Ajuste tamanhos e preferências na tela **Configurações**.

Exemplo de entrada:

```text
JOÃO,10,G
MARIA,2,M
PEDRO,PP
ANA,1,GG
JOANA,10,M,BLM,JUVENIL
```

O ListForge interpreta, organiza e prepara a saída de acordo com os tamanhos configurados.

---

## Temas

O aplicativo possui três temas:

| Tema | Descrição |
|---|---|
| `ListForge Dark` | Tema escuro padrão do ListForge |
| `ListForge Light` | Tema claro para uso em ambientes iluminados |
| `SISBolt` | Tema baseado na paleta original do SISBolt |

Os temas são aplicados em runtime por meio de `ResourceDictionary` do WPF.

Arquivos:

```text
UI/Themes/DarkTheme.xaml
UI/Themes/LightTheme.xaml
UI/Themes/SisBoltTheme.xaml
```

---

## Formatos Suportados

| Tipo | Extensões |
|---|---|
| Texto | `.txt`, `.csv`, `.list` |
| PDF | `.pdf` |
| Word | `.docx` |
| Excel | `.xlsx`, `.xlsm` |
| Imagens | `.png`, `.jpg`, `.jpeg`, `.bmp`, `.tif`, `.tiff`, `.webp` |
| JSON por link | URLs `http://` ou `https://` |

> Para imagens, o app usa OCR via Tesseract.

---

## Instalação e Execução

### Requisitos para desenvolvimento

| Ferramenta | Versão recomendada |
|---|---|
| Windows | 10 ou 11 x64 |
| .NET SDK | 8.0+ |
| Visual Studio | 2022 ou superior |
| Inno Setup | 6.x ou 7.x, opcional para instalador |

### Rodar pelo terminal

Na raiz do projeto:

```powershell
dotnet run
```

### Compilar em Debug

```powershell
dotnet build
```

Executável de debug:

```text
bin\Debug\net8.0-windows\ListForge.exe
```

---

## Gerando Versões de Distribuição

As versões finais são geradas em:

```text
bin\Release\dist\2.1.2\
```

### Versão instalável

Pasta usada como base para o instalador. Ela já inclui o runtime necessário para Windows x64.

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o bin\Release\dist\2.1.2\ListForge-Installable
```

Saída:

```text
bin\Release\dist\2.1.2\ListForge-Installable\
```

### Versão portátil one-file

Gera um único `.exe`, parecido com o modelo de distribuição comum em apps Python empacotados.

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o bin\Release\dist\2.1.2\ListForge-Portable-OneFile
```

Saída:

```text
bin\Release\dist\2.1.2\ListForge-Portable-OneFile\ListForge-2.1.2-Portable-OneFile.exe
```

Observações:

- O arquivo fica maior porque inclui runtime e dependências.
- Bibliotecas nativas podem ser extraídas temporariamente pelo runtime na primeira execução.
- O `.pdb` gerado é opcional e pode ser ignorado na distribuição.

---

## Criando Instalador

O projeto já inclui um script para **Inno Setup**:

```text
installer\ListForge.iss
```

### Passo a passo

1. Gere a versão instalável:

   ```powershell
   dotnet publish -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o bin\Release\dist\2.1.2\ListForge-Installable
   ```

2. Instale o Inno Setup:

   ```text
   https://jrsoftware.org/isdl.php
   ```

3. Abra:

   ```text
   installer\ListForge.iss
   ```

4. Clique em **Build > Compile**.

O instalador será gerado em:

```text
bin\Release\dist\2.1.2\Installer\
```

O script configura:

- nome do app;
- versão `2.1.2`;
- instalação em `Program Files`;
- atalho no Menu Iniciar;
- opção de atalho na Área de Trabalho;
- ícone do instalador;
- execução do app ao finalizar.

---

## Ícone

O ícone do aplicativo fica em:

```text
Assets\logo.ico
```

Referência no projeto:

```xml
<ApplicationIcon>Assets\logo.ico</ApplicationIcon>
```

---

## Configurações e Dados Locais

O ListForge salva as configurações do usuário em uma pasta gravável. A ordem de tentativa é:

1. `%APPDATA%\ListForge`
2. `%LOCALAPPDATA%\ListForge`
3. Pasta do app
4. Pasta temporária do Windows

Arquivos principais:

```text
config.json
sizes.json
backups\
```

### Arquivos de configuração

| Arquivo | Uso |
|---|---|
| `config.json` | Preferências gerais do app |
| `sizes.json` | Grupos de tamanhos válidos |
| `backups\` | Backups automáticos de listas sobrescritas |

---

## Estrutura do Projeto

```text
ListForge/
├─ App.xaml
├─ App.xaml.cs
├─ GlobalUsings.cs
├─ ListForge.csproj
├─ README.md
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
│     ├─ MainWindow.xaml
│     ├─ EditorView.xaml
│     ├─ SettingsView.xaml
│     ├─ ManualView.xaml
│     └─ InputDialog.xaml.cs
└─ installer/
   └─ ListForge.iss
```

---

## Stack Técnica

| Área | Tecnologia |
|---|---|
| Linguagem | C# |
| UI | WPF |
| Runtime | .NET 8 |
| JSON | Newtonsoft.Json |
| PDF | PdfPig |
| Word | DocumentFormat.OpenXml |
| Excel | ClosedXML |
| OCR | Tesseract |
| Instalador | Inno Setup |

---

## Diferenças em Relação à Versão Python

| Python / Tkinter | C# / WPF |
|---|---|
| Temas em dicionários Python | Temas em `ResourceDictionary` XAML |
| Widgets customizados Tkinter | Controles customizados WPF |
| Scripts de runtime de UI | `MainViewModel` com comandos |
| Views em Python | Views em XAML |
| Empacotamento estilo one-file | `dotnet publish` self-contained / single-file |

---

## Boas Práticas do Repositório

O `.gitignore` evita versionar:

- `bin/`
- `obj/`
- `.vs/`
- builds publicados;
- instaladores gerados;
- arquivos `.pdb`;
- arquivos temporários.

Devem ser versionados:

- código fonte;
- temas;
- `Assets/logo.ico`;
- `installer/ListForge.iss`;
- documentação.

---

## Roadmap

Ideias para próximas versões:

- exportação com modelos configuráveis;
- preview visual da estrutura JSON;
- importação avançada de planilhas;
- logs de erro dentro do app;
- atualização automática;
- instalador com verificação do .NET Runtime;
- testes automatizados para o processamento de listas.

---

## Licença

Defina uma licença antes de publicar o projeto publicamente, por exemplo:

- MIT;
- Apache 2.0;
- Proprietária / uso interno.
