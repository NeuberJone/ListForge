# ListForge — Porta C# / WPF

## Pré-requisitos

| Ferramenta | Versão mínima |
|---|---|
| .NET SDK | 8.0 |
| Visual Studio | 2022 (ou `dotnet CLI`) |
| Windows | 10 / 11 x64 |

---

## Estrutura do projeto

```
ListForge/
├── App.xaml / App.xaml.cs          ← ponto de entrada, AppUserModelID
├── ListForge.csproj
│
├── Config/
│   └── ConfigManager.cs            ← config.json / sizes.json / backups
│
├── Core/
│   ├── FileImporter.cs             ← leitura de .txt, .pdf, .docx, .xlsx, OCR
│   ├── ListProcessor.cs            ← parse, sort, build output, export
│   └── SizeHelper.cs               ← índice de tamanhos, validação, formatação
│
├── Models/
│   ├── AppConfig.cs
│   ├── ParsedRow.cs
│   └── SizeConfig.cs
│
├── ViewModels/
│   ├── MainViewModel.cs            ← toda lógica de UI (equivale aos *_runtime.py)
│   └── RelayCommand.cs
│
├── UI/
│   ├── Controls/
│   │   └── SegmentedControl.cs     ← controle customizado (equivale ao widget Python)
│   ├── Themes/
│   │   ├── DarkTheme.xaml          ← ListForge Dark
│   │   └── SisBoltTheme.xaml       ← SisBolt Dark
│   └── Views/
│       ├── MainWindow.xaml/.cs     ← shell com sidebar e barra de status
│       ├── EditorView.xaml/.cs     ← editor + saída + busca + preparação
│       ├── SettingsView.xaml/.cs   ← todas as configurações
│       └── ManualView.xaml/.cs     ← documentação interna
│
└── tesseract/                      ← COPIAR do repositório original
    ├── tesseract.exe
    └── tessdata/
        ├── por.traineddata
        └── eng.traineddata
```

---

## Configuração do Tesseract

Copie a pasta `tesseract/` do repositório original para dentro do projeto.  
O `.csproj` já está configurado para incluí-la no output:

```xml
<Content Include="tesseract\**\*.*">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
```

O caminho é resolvido automaticamente em runtime pela ordem:
1. Variável de ambiente `TESSERACT_CMD`
2. `<pasta do exe>/tesseract/tessdata/`  ← padrão do bundle
3. `C:\Program Files\Tesseract-OCR\tessdata\`

---

## Build

```bash
# Restaurar pacotes
dotnet restore ListForge.csproj

# Build debug
dotnet build -c Debug

# Build release / publicar autocontido
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## Pacotes NuGet utilizados

| Pacote | Finalidade |
|---|---|
| `Newtonsoft.Json` | Serialização / deserialização de config e JSON de saída |
| `Tesseract` 5.x | OCR via Tesseract bundled |
| `PdfPig` | Extração de texto de PDFs |
| `DocumentFormat.OpenXml` | Leitura de `.docx` |
| `ClosedXML` | Leitura de `.xlsx` / `.xlsm` |

---

## Diferenças em relação à versão Python

| Python (Tkinter) | C# (WPF) |
|---|---|
| `SegmentedControl` widget customizado | `SegmentedControl` WPF customizado (UI/Controls) |
| Temas via dicionários Python | ResourceDictionaries XAML trocáveis em runtime |
| `listforge_core.py` | `Core/ListProcessor.cs` |
| `listforge_sizes.py` | `Core/SizeHelper.cs` |
| `listforge_config.py` | `Config/ConfigManager.cs` |
| `ui/controllers/*_runtime.py` | `ViewModels/MainViewModel.cs` |
| `ui/views/*_view.py` | `UI/Views/*.xaml` + `*.xaml.cs` |
| `ui/shell.py` | `UI/Views/MainWindow.xaml` |

---

## Dados de configuração

Salvos em `%APPDATA%\ListForge\`:

```
config.json    ← preferências gerais
sizes.json     ← grupos de tamanhos
backups\       ← backups automáticos das listas
```
