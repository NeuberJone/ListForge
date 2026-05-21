# ListForge

**ListForge** é uma ferramenta desktop para edição, padronização, organização e exportação de listas de produção.

O sistema foi desenvolvido para transformar listas recebidas em diferentes formatos em uma saída limpa, previsível e pronta para uso em fluxos de produção, reduzindo retrabalho manual, erros de digitação, inconsistências de tamanho e perda de tempo na preparação dos dados.

---

## Visão geral

O ListForge centraliza em uma única aplicação as tarefas mais comuns de tratamento de listas:

* abrir, editar e salvar listas;
* importar dados a partir de arquivos de texto, planilhas, documentos, PDFs e imagens;
* organizar nomes, números, tamanhos e informações adicionais;
* reconhecer grupos de tamanho configuráveis;
* limpar espaçamentos e padronizar separadores;
* localizar e substituir informações no editor;
* gerar saída organizada em texto;
* gerar JSON compatível com fluxos estruturados de produção;
* manter backups automáticos dos arquivos editados;
* salvar preferências de uso, tema, separadores e tamanhos.

A proposta do ListForge é ser uma ferramenta objetiva: o operador cola, abre ou importa uma lista, confere o conteúdo, processa os dados e exporta o resultado com menos etapas manuais.

---

## Principais recursos

### Editor de listas

O editor permite trabalhar diretamente com o conteúdo da lista antes do processamento. É possível colar texto, abrir arquivos compatíveis, limpar espaços, localizar termos, substituir valores e salvar alterações.

Recursos disponíveis:

* abertura de arquivos de entrada;
* salvamento da lista atual;
* salvamento como novo arquivo;
* limpeza de espaços baseada no separador configurado;
* busca avançada no texto;
* substituição individual ou em massa;
* indicação da lista atualmente carregada;
* barra de status com retorno das operações.

---

### Importação de arquivos

O ListForge consegue importar conteúdo a partir de diferentes formatos usados no dia a dia.

Formatos suportados:

| Tipo    | Extensões                                                 |
| ------- | --------------------------------------------------------- |
| Texto   | `.txt`, `.csv`, `.list`                                   |
| PDF     | `.pdf`                                                    |
| Word    | `.doc`, `.docx`                                           |
| Excel   | `.xls`, `.xlsx`, `.xlsm`                                  |
| Imagens | `.png`, `.jpg`, `.jpeg`, `.bmp`, `.tif`, `.tiff`, `.webp` |

Ao importar arquivos que não são texto puro, o ListForge extrai o conteúdo e envia para o editor, permitindo que o usuário revise a lista antes de processar.

---

### OCR para imagens

O ListForge possui suporte a OCR para leitura de listas em imagem.

Esse recurso é útil quando a lista chega como captura de tela, imagem exportada, foto ou arquivo escaneado. O texto reconhecido é enviado para o editor para conferência antes do processamento.

Idiomas utilizados no OCR:

* Português;
* Inglês.

O OCR ajuda a reduzir digitação manual, mas não substitui a conferência humana. Sempre revise o conteúdo importado antes de exportar a lista final.

---

### Processamento de listas

O processamento interpreta linhas de entrada contendo nome, número, tamanho e informações adicionais.

O ListForge identifica tamanhos válidos, separa os campos principais, organiza os registros e monta uma saída padronizada.

Exemplo de entrada:

```txt
JOÃO,10,G
MARIA,7,BLM
PEDRO,15,12A
```

Exemplo de saída organizada:

```txt
JOÃO,10,G
MARIA,7,,BLM
PEDRO,15,,,12A
```

A saída final pode variar conforme os grupos de tamanho configurados e os campos adicionais presentes na lista.

---

### Suporte a quantidades por tamanho

O ListForge aceita tamanhos com quantidade informada no formato `quantidade-tamanho`.

Exemplo:

```txt
JOÃO,10,3-G
```

Esse formato permite representar múltiplas peças do mesmo tamanho sem repetir manualmente a mesma linha na entrada.

---

### Grupos de tamanho configuráveis

O sistema trabalha com grupos de tamanho para organizar corretamente a saída.

Grupos padrão:

| Grupo     | Exemplos                                               |
| --------- | ------------------------------------------------------ |
| Masculino | `PP`, `P`, `M`, `G`, `GG`, `XG`, `XGG`, `XXGG`, `XLGG` |
| Feminino  | `BLPP`, `BLP`, `BLM`, `BLG`, `BLGG`, `BLXG`            |
| Infantil  | `2A`, `4A`, `6A`, `8A`, `10A`, `12A`, `14A`, `16A`     |

Os grupos podem ser configurados na tela de configurações por meio de:

* tamanhos-base;
* prefixos;
* sufixos.

Isso permite adaptar o ListForge ao padrão usado por cada operação, cliente ou fluxo de produção.

---

### Separadores personalizados

O usuário pode definir o separador de entrada utilizado para interpretar as listas.

Separadores comuns:

* vírgula: `,`;
* ponto e vírgula: `;`;
* tabulação: `\t`.

O separador padrão pode ser salvo nas configurações para uso recorrente.

---

### Padronização de caixa

O ListForge permite controlar a forma como os textos serão exportados.

Opções disponíveis:

| Opção          | Resultado                                          |
| -------------- | -------------------------------------------------- |
| Original       | Mantém o texto como foi informado                  |
| Tudo maiúsculo | Converte nomes e campos adicionais para maiúsculas |
| Tudo minúsculo | Converte nomes e campos adicionais para minúsculas |

---

### Geração de JSON

Além da saída em texto, o ListForge pode gerar uma estrutura JSON para integração com fluxos que utilizam dados estruturados.

A geração de JSON pode ser habilitada ou ocultada nas configurações, permitindo usar a aplicação em um modo simples ou em um modo mais completo.

Campos usados na estrutura de saída:

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

---

### Extração por link

O ListForge permite extrair listas a partir de um link que retorne JSON válido.

Esse recurso é útil quando a lista está disponível em uma origem externa ou serviço intermediário, permitindo trazer o conteúdo diretamente para o editor e processá-lo dentro da aplicação.

---

### Backups automáticos

Ao salvar alterações em arquivos já existentes, o ListForge cria backups automáticos antes de sobrescrever o conteúdo.

Os backups são armazenados na pasta de dados da aplicação, preservando versões anteriores da lista e reduzindo o risco de perda de informação.

---

### Temas visuais

O ListForge possui suporte a temas visuais selecionáveis em tempo de execução.

Temas disponíveis:

* ListForge Dark;
* ListForge Light;
* SISBolt.

A preferência de tema é salva automaticamente nas configurações do usuário.

---

## Estrutura do projeto

```txt
ListForge/
├── App.xaml
├── App.xaml.cs
├── GlobalUsings.cs
├── ListForge.csproj
│
├── Assets/
│   └── recursos visuais da aplicação
│
├── Config/
│   └── ConfigManager.cs
│
├── Core/
│   ├── FileImporter.cs
│   ├── ListProcessor.cs
│   └── SizeHelper.cs
│
├── Models/
│   ├── AppConfig.cs
│   ├── ParsedRow.cs
│   └── SizeConfig.cs
│
├── UI/
│   ├── Controls/
│   ├── Themes/
│   └── Views/
│
├── ViewModels/
│   ├── MainViewModel.cs
│   └── RelayCommand.cs
│
├── installer/
│   └── arquivos auxiliares de instalação
│
└── tesseract/
    ├── tesseract.exe
    └── tessdata/
        ├── por.traineddata
        └── eng.traineddata
```

---

## Arquitetura

O ListForge segue uma organização simples, separando interface, regras de negócio, modelos, configuração e importação de arquivos.

| Camada        | Responsabilidade                                        |
| ------------- | ------------------------------------------------------- |
| `UI/Views`    | Telas da aplicação                                      |
| `UI/Themes`   | Temas visuais em XAML                                   |
| `UI/Controls` | Controles customizados                                  |
| `ViewModels`  | Estado da interface e comandos de interação             |
| `Core`        | Processamento de listas, importação e regras de tamanho |
| `Models`      | Estruturas de dados da aplicação                        |
| `Config`      | Persistência de preferências, tamanhos e backups        |

Essa separação facilita manutenção, evolução da interface e ajuste das regras de processamento sem misturar responsabilidades.

---

## Dados de configuração

As configurações do usuário são salvas em uma pasta própria da aplicação.

Local padrão:

```txt
%APPDATA%\ListForge\
```

Arquivos e pastas principais:

```txt
config.json     Preferências gerais da aplicação
sizes.json      Configuração dos grupos de tamanho
backups\        Backups automáticos das listas editadas
```

Caso o local padrão não esteja disponível, a aplicação tenta utilizar outros diretórios graváveis do usuário ou do ambiente de execução.

---

## Configurações disponíveis

O usuário pode ajustar:

* exibição da seção de JSON;
* exibição dos botões de gerar e copiar JSON;
* pasta padrão de saída;
* nome padrão da lista;
* modo padrão de caixa do texto;
* separador padrão de entrada;
* tema visual;
* grupos de tamanho;
* prefixos e sufixos de tamanho.

---

## Tesseract OCR

Para que a leitura de imagens funcione corretamente, a pasta `tesseract/` deve estar disponível junto ao executável ou dentro do projeto durante o desenvolvimento.

Estrutura esperada:

```txt
tesseract/
├── tesseract.exe
└── tessdata/
    ├── por.traineddata
    └── eng.traineddata
```

O ListForge procura o OCR nesta ordem:

1. caminho definido pela variável de ambiente `TESSERACT_CMD`;
2. pasta `tesseract/` ao lado do executável;
3. instalação local em `C:\Program Files\Tesseract-OCR\`;
4. instalação local em `C:\Program Files (x86)\Tesseract-OCR\`.

---

## Pré-requisitos para desenvolvimento

| Ferramenta    | Versão recomendada                         |
| ------------- | ------------------------------------------ |
| Windows       | 10 ou 11 x64                               |
| .NET SDK      | 8.0 ou superior                            |
| Visual Studio | 2022 ou superior                           |
| Tesseract OCR | Incluso no projeto ou instalado no sistema |

---

## Como executar em desenvolvimento

Restaure os pacotes:

```bash
dotnet restore ListForge.csproj
```

Compile em modo Debug:

```bash
dotnet build ListForge.csproj -c Debug
```

Execute a aplicação:

```bash
dotnet run --project ListForge.csproj
```

---

## Publicação

Para gerar uma versão Release para Windows x64:

```bash
dotnet publish ListForge.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

A saída publicada será gerada dentro da pasta `bin/Release/`.

Antes de distribuir, confira se os arquivos necessários do OCR foram incluídos corretamente no pacote final.

---

## Dependências principais

| Dependência            | Finalidade                 |
| ---------------------- | -------------------------- |
| Newtonsoft.Json        | Leitura e geração de JSON  |
| Tesseract              | OCR de imagens             |
| PdfPig                 | Extração de texto de PDFs  |
| DocumentFormat.OpenXml | Leitura de documentos Word |
| ClosedXML              | Leitura de planilhas Excel |

---

## Fluxo básico de uso

1. Abra, cole ou importe uma lista.
2. Confira o conteúdo no editor.
3. Ajuste o separador, se necessário.
4. Use a limpeza de espaços quando a lista vier desalinhada.
5. Processe a lista.
6. Confira a saída organizada.
7. Copie ou salve o resultado.
8. Gere JSON quando o fluxo exigir dados estruturados.

---

## Exemplo de entrada

```txt
JOAO,10,G
MARIA,7,BLM
PEDRO,15,12A
ANA,22,2-G
```

---

## Exemplo de saída textual

```txt
ANA,22,G
ANA,22,G
JOAO,10,G
MARIA,7,,BLM
PEDRO,15,,,12A
```

A disposição das colunas depende dos grupos de tamanho ativos e dos campos extras encontrados na lista.

---

## Exemplo de JSON gerado

```json
{
  "title": "List",
  "order_number": 0,
  "client_name": "",
  "orders": [
    {
      "Name": "JOAO",
      "Nickname": "",
      "Number": "10",
      "BloodType": "",
      "Gender": "MA",
      "ShortSleeve": "G",
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

---

## Observações sobre entrada de dados

Para obter melhores resultados, use uma linha por item e mantenha os campos principais separados de forma consistente.

Formato recomendado:

```txt
Nome,Número,Tamanho
```

Também são aceitos campos adicionais após os campos principais, conforme a necessidade do fluxo.

Exemplo:

```txt
Nome,Número,Tamanho,Apelido,TipoSanguíneo
```

Quando uma linha contém tamanho inválido ou não reconhecido, o ListForge informa o erro e indica a linha problemática sempre que possível.

---

## Boas práticas

* Revise listas importadas de PDF, Word, Excel ou imagem antes de processar.
* Configure os grupos de tamanho antes de usar o sistema em produção.
* Use nomes de arquivo claros ao salvar saídas.
* Mantenha o OCR junto ao executável quando distribuir a aplicação.
* Faça testes com listas reais antes de liberar uma nova versão para operadores.

---

## Roadmap sugerido

Possíveis evoluções futuras:

* instalador automatizado para distribuição interna;
* assinatura digital do executável;
* atualização automática;
* perfis de configuração por cliente ou setor;
* histórico de listas processadas;
* validação visual de linhas com erro;
* exportação em formatos adicionais;
* integração direta com outros sistemas de produção.

---

## Licença

Este projeto é distribuído sob licença proprietária, salvo autorização expressa em contrário.

O código-fonte, os executáveis, os recursos visuais e os arquivos auxiliares não podem ser copiados, revendidos, redistribuídos ou modificados por terceiros sem permissão do autor.

Antes de publicar, vender ou transferir o projeto para outra empresa, revise esta seção conforme o modelo comercial escolhido.

---

## Autor

Desenvolvido por **Neuber Jone**.

---

## Status

O ListForge está em desenvolvimento ativo e deve ser validado com listas reais antes de uso definitivo em produção.