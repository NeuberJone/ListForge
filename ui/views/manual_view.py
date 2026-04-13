from __future__ import annotations

import tkinter as tk

from ui import theme
from ui.widgets import make_card


class ManualSection(tk.Frame):
    def __init__(
        self,
        parent: tk.Misc,
        *,
        title: str,
        body: str,
        starts_open: bool = False,
    ) -> None:
        super().__init__(parent, bg=theme.active_theme().app_bg)
        self.title = title
        self.body = body
        self.is_open = starts_open

        self._build()

    def _build(self) -> None:
        t = theme.active_theme()

        self.card = make_card(self)
        self.card.pack(fill="x", expand=True)

        self.header = tk.Frame(self.card, bg=t.panel_bg)
        self.header.pack(fill="x")

        self.toggle_var = tk.StringVar()
        self._refresh_toggle_text()

        self.btn_toggle = tk.Button(
            self.header,
            textvariable=self.toggle_var,
            command=self.toggle,
            bg=t.panel_bg,
            fg=t.text,
            activebackground=t.panel_hover,
            activeforeground=t.text,
            relief="flat",
            bd=0,
            cursor="hand2",
            anchor="w",
            font=(theme.FONT_FAMILY, 10, "bold"),
            padx=14,
            pady=10,
        )
        self.btn_toggle.pack(fill="x")

        self.content = tk.Frame(self.card, bg=t.panel_bg)

        self.lbl_body = tk.Label(
            self.content,
            text=self.body,
            bg=t.panel_bg,
            fg=t.text_muted,
            justify="left",
            anchor="w",
            font=(theme.FONT_FAMILY, 10),
            wraplength=980,
        )
        self.lbl_body.pack(fill="x", padx=14, pady=(0, 14))

        if self.is_open:
            self.content.pack(fill="x")

    def _refresh_toggle_text(self) -> None:
        arrow = "▼" if self.is_open else "▶"
        self.toggle_var.set(f"{arrow} {self.title}")

    def toggle(self) -> None:
        self.is_open = not self.is_open
        self._refresh_toggle_text()

        if self.is_open:
            self.content.pack(fill="x")
        else:
            self.content.pack_forget()

    def refresh_theme(self) -> None:
        t = theme.active_theme()

        self.configure(bg=t.app_bg)
        self.header.configure(bg=t.panel_bg)
        self.content.configure(bg=t.panel_bg)

        self.btn_toggle.configure(
            bg=t.panel_bg,
            fg=t.text,
            activebackground=t.panel_hover,
            activeforeground=t.text,
        )

        self.lbl_body.configure(
            bg=t.panel_bg,
            fg=t.text_muted,
        )


class ManualView(tk.Frame):
    def __init__(self, parent: tk.Misc, controller) -> None:
        super().__init__(parent, bg=theme.active_theme().app_bg)
        self.controller = controller
        self.sections: list[ManualSection] = []

        self.grid_rowconfigure(0, weight=1)
        self.grid_columnconfigure(0, weight=1)

        self._build()

    def _build(self) -> None:
        t = theme.active_theme()

        outer = tk.Frame(self, bg=t.app_bg)
        outer.grid(row=0, column=0, sticky="nsew")
        outer.grid_rowconfigure(1, weight=1)
        outer.grid_columnconfigure(0, weight=1)

        header_card = make_card(outer)
        header_card.grid(row=0, column=0, sticky="ew", pady=(0, 10))

        header_inner = tk.Frame(header_card, bg=t.panel_bg)
        header_inner.pack(fill="both", expand=True, padx=16, pady=14)

        tk.Label(
            header_inner,
            text="Manual",
            bg=t.panel_bg,
            fg=t.text,
            font=(theme.FONT_FAMILY, 13, "bold"),
            anchor="w",
        ).pack(fill="x")

        tk.Label(
            header_inner,
            text=(
                "Este manual descreve o funcionamento atual do Texpad: como montar a entrada, "
                "como o programa interpreta cada linha, como extrair lista a partir de um link, "
                "como usar a saída organizada, o JSON e as configurações."
            ),
            bg=t.panel_bg,
            fg=t.text_muted,
            font=(theme.FONT_FAMILY, 10),
            justify="left",
            anchor="w",
            wraplength=980,
        ).pack(fill="x", pady=(6, 0))

        body = tk.Frame(outer, bg=t.app_bg)
        body.grid(row=1, column=0, sticky="nsew")
        body.grid_rowconfigure(0, weight=1)
        body.grid_columnconfigure(0, weight=1)

        self.canvas = tk.Canvas(
            body,
            bg=t.app_bg,
            highlightthickness=0,
            bd=0,
        )
        self.canvas.grid(row=0, column=0, sticky="nsew")

        self.scrollbar = tk.Scrollbar(body, orient="vertical", command=self.canvas.yview)
        self.scrollbar.grid(row=0, column=1, sticky="ns")

        self.canvas.configure(yscrollcommand=self.scrollbar.set)

        self.scroll_frame = tk.Frame(self.canvas, bg=t.app_bg)
        self.canvas_window = self.canvas.create_window((0, 0), window=self.scroll_frame, anchor="nw")

        self.scroll_frame.bind("<Configure>", self._on_frame_configure)
        self.canvas.bind("<Configure>", self._on_canvas_configure)

        self._build_sections()
        self._bind_mousewheel()

    def _build_sections(self) -> None:
        sections_data = [
            (
                "Visão geral",
                (
                    "O Texpad é um editor voltado para organizar listas de produção e transformar a entrada em "
                    "duas saídas principais: a Lista organizada e a Prévia JSON.\n\n"
                    "O fluxo mais comum é:\n"
                    "1. Abrir ou colar a lista na entrada.\n"
                    "2. Ajustar separador, limpeza e modo de texto, se necessário.\n"
                    "3. Processar a lista.\n"
                    "4. Conferir a Lista organizada.\n"
                    "5. Salvar a saída ou gerar o JSON.\n\n"
                    "Além disso, o programa também pode extrair uma lista a partir de um link HTTP(S) que retorna "
                    "um JSON compatível."
                ),
                True,
            ),
            (
                "Fluxo rápido de uso",
                (
                    "Fluxo recomendado para o dia a dia:\n\n"
                    "• Clique em Abrir Lista para carregar um arquivo de texto.\n"
                    "• Se a lista veio de fora e precisa de limpeza, use o painel Preparação da lista.\n"
                    "• Se a origem for um link, use Extrair lista do link e cole a URL na janela que abrir.\n"
                    "• Clique em Processar.\n"
                    "• Confira a aba Lista organizada.\n"
                    "• Se estiver tudo certo, use Copiar saída ou Salvar saída.\n"
                    "• Quando precisar do arquivo estruturado, use Gerar JSON.\n\n"
                    "Se algo não sair como esperado, revise primeiro o separador da entrada e os tamanhos usados."
                ),
                False,
            ),
            (
                "Interface do programa",
                (
                    "A interface é dividida em três áreas principais:\n\n"
                    "• Barra lateral: acesso às telas Editor, Configurações e Manual.\n"
                    "• Área central do Editor: onde ficam a entrada, os painéis retráteis e a saída.\n"
                    "• Rodapé do Editor: onde ficam os botões de limpar, copiar, salvar, gerar JSON e processar.\n\n"
                    "Na tela Editor, a coluna da esquerda é usada para entrada e edição da lista.\n"
                    "A coluna da direita mostra a saída processada.\n\n"
                    "A saída pode alternar entre:\n"
                    "• Lista organizada\n"
                    "• Prévia JSON\n\n"
                    "As áreas de texto possuem numeração de linhas, barras de rolagem vertical e horizontal e "
                    "suporte a colagem e edição manual."
                ),
                False,
            ),
            (
                "Arquivos, salvamento e backups",
                (
                    "Na barra superior do Editor existem ações ligadas a arquivo e edição:\n\n"
                    "• Abrir Lista: abre um arquivo de texto para colocar na entrada.\n"
                    "• Extrair lista do link: abre uma janela para você colar um link HTTP(S) que retorna JSON.\n"
                    "• Salvar Entrada: salva o conteúdo atual da entrada no arquivo atual.\n"
                    "• Salvar Entrada Como: salva a entrada em um novo arquivo.\n"
                    "• Desfazer: desfaz a última alteração feita na entrada.\n"
                    "• Abrir pasta de backups: abre a pasta onde os backups são guardados.\n\n"
                    "Quando você salva por cima de um arquivo já existente, o programa pode criar um backup antes "
                    "da gravação. Isso ajuda a recuperar versões anteriores da lista."
                ),
                False,
            ),
            (
                "Como montar a entrada",
                (
                    "A entrada é lida linha por linha.\n\n"
                    "Cada linha pode conter:\n"
                    "• nome\n"
                    "• número\n"
                    "• um ou mais tamanhos\n"
                    "• até dois campos extras de texto\n\n"
                    "Exemplos válidos:\n"
                    "JOÃO,10,P\n"
                    "JOÃO,10,P,M\n"
                    "JOÃO,APELIDO,10,P\n"
                    "JOÃO,APELIDO,10,P,O+\n"
                    "JUACA,JUSÉ,4-PP,BLG,8A\n\n"
                    "O separador padrão é a vírgula, mas você pode usar outro separador no painel Preparação da lista.\n"
                    "Linhas vazias são ignoradas."
                ),
                False,
            ),
            (
                "Como o editor interpreta cada linha",
                (
                    "A leitura da linha segue estas regras:\n\n"
                    "1. A linha é dividida usando o separador atual.\n"
                    "2. O primeiro valor numérico puro é tratado como Número.\n"
                    "3. Valores que forem reconhecidos como tamanho entram na lista de tamanhos.\n"
                    "4. O primeiro texto que não for número nem tamanho vira Nome.\n"
                    "5. Os dois próximos textos extras viram campos adicionais.\n\n"
                    "Na prática, esses campos extras costumam ser usados como:\n"
                    "• s2 = Apelido\n"
                    "• s3 = Tipo sanguíneo\n\n"
                    "Se a linha não tiver nenhum tamanho reconhecido, ela gera erro.\n"
                    "Se a linha tiver mais de 4 tokens de tamanho, ela também gera erro.\n\n"
                    "Importante: o programa não depende da posição fixa de cada parte. Ele decide pelo conteúdo.\n"
                    "Por isso, um valor que parece texto comum pode ser interpretado como tamanho se ele existir "
                    "no cadastro de tamanhos."
                ),
                False,
            ),
            (
                "Regras de quantidade e separação por gênero",
                (
                    "O programa aplica duas regras importantes durante o processamento:\n\n"
                    "1. Quantidade no tamanho\n"
                    "Se um tamanho vier no formato qtd-tamanho, ele é expandido em múltiplas linhas.\n\n"
                    "Exemplo:\n"
                    ",,3-P\n\n"
                    "vira:\n"
                    ",,P\n"
                    ",,P\n"
                    ",,P\n\n"
                    "2. Gêneros diferentes na mesma linha\n"
                    "Uma mesma linha não deve sair misturando tamanhos de grupos diferentes. "
                    "Se houver mistura de Masculino, Feminino e/ou Infantil, o programa separa em linhas diferentes.\n\n"
                    "Exemplo:\n"
                    "JUACA,JUSÉ,PP,BLP\n\n"
                    "vira:\n"
                    "JUACA,,PP,,JUSÉ\n"
                    "JUACA,,,BLP,JUSÉ\n\n"
                    "Com isso, a saída fica coerente com os grupos de tamanho e evita colunas misturadas."
                ),
                False,
            ),
            (
                "Preparação da lista",
                (
                    "O painel Preparação da lista serve para ajustar a entrada antes do processamento.\n\n"
                    "Campos e ações:\n\n"
                    "• Separador da entrada\n"
                    "Define como cada linha será dividida. O padrão é vírgula.\n\n"
                    "• Padrão (,)\n"
                    "Restaura rapidamente o separador para vírgula.\n\n"
                    "• Remover espaços desnecessários\n"
                    "Remove espaços extras ao redor de cada parte da linha, usando o separador atual como referência.\n\n"
                    "• Maiúsculas / minúsculas\n"
                    "Controla como textos comuns vão sair depois do processamento:\n"
                    "  - Original\n"
                    "  - Tudo maiúsculo\n"
                    "  - Tudo minúsculo\n\n"
                    "Observação importante: os campos de tamanho continuam sendo tratados como tamanhos válidos do sistema."
                ),
                False,
            ),
            (
                "Extrair lista do link",
                (
                    "O botão Extrair lista do link serve para quando a origem da lista não é um arquivo de texto, "
                    "mas sim um link HTTP(S) que retorna um JSON compatível.\n\n"
                    "Como funciona:\n"
                    "1. Clique em Extrair lista do link.\n"
                    "2. Cole o link na janela que abrir.\n"
                    "3. O programa baixa o JSON.\n"
                    "4. A lista é extraída automaticamente para a entrada.\n"
                    "5. Em seguida, o programa já processa o conteúdo.\n\n"
                    "Regras:\n"
                    "• O link precisa começar com http:// ou https://.\n"
                    "• O conteúdo retornado precisa ser um JSON válido.\n"
                    "• O JSON precisa conter uma estrutura compatível para extração da lista.\n\n"
                    "Erros comuns nessa etapa:\n"
                    "• link inválido\n"
                    "• link inacessível\n"
                    "• resposta que não é JSON\n"
                    "• JSON sem estrutura esperada"
                ),
                False,
            ),
            (
                "Localizar e substituir",
                (
                    "O painel Localizar / Localizar e substituir é usado para revisar e corrigir a entrada.\n\n"
                    "Campos:\n"
                    "• Localizar\n"
                    "• Substituir por\n"
                    "• Diferenciar maiúsculas/minúsculas\n\n"
                    "Botões:\n"
                    "• Localizar: encontra o próximo termo a partir da posição atual.\n"
                    "• Anterior: volta para a ocorrência anterior.\n"
                    "• Próximo: avança para a próxima ocorrência.\n"
                    "• Substituir: troca apenas a ocorrência selecionada.\n"
                    "• Substituir tudo: troca todas as ocorrências encontradas.\n"
                    "• Limpar destaque: remove a marcação visual da busca.\n\n"
                    "Esse painel é útil para corrigir nomes, apelidos, abreviações e padrões repetidos antes de processar."
                ),
                False,
            ),
            (
                "Saída organizada",
                (
                    "A Lista organizada é a principal saída visual do programa.\n\n"
                    "Ela é montada a partir da entrada já interpretada e normalizada.\n\n"
                    "Estrutura geral da saída:\n"
                    "• Nome\n"
                    "• Número\n"
                    "• colunas de tamanhos por grupo ativo\n"
                    "• campos extras, quando existirem\n\n"
                    "Os grupos de tamanho seguem a lógica do cadastro:\n"
                    "• Masculino\n"
                    "• Feminino\n"
                    "• Infantil\n\n"
                    "Se houver mistura de grupos na mesma linha original, o programa separa isso em linhas diferentes.\n"
                    "Se houver quantidade no formato 3-P, ele expande isso em várias linhas.\n\n"
                    "Botões relacionados:\n"
                    "• Copiar saída: copia o texto da saída organizada.\n"
                    "• Salvar saída: salva a saída em arquivo de texto.\n\n"
                    "Se a saída ainda estiver vazia, o programa tenta processar antes de copiar ou salvar."
                ),
                False,
            ),
            (
                "JSON e prévia JSON",
                (
                    "Além da Lista organizada, o programa também monta uma Prévia JSON.\n\n"
                    "Essa prévia é gerada a partir do conteúdo processado e mostra a estrutura que será exportada "
                    "quando você usar o botão Gerar JSON.\n\n"
                    "Opções relacionadas:\n"
                    "• Mostrar área de JSON\n"
                    "• Mostrar botão Gerar JSON\n"
                    "• Mostrar botão Copiar JSON\n\n"
                    "A área de JSON pode ficar visível ou oculta, dependendo das configurações.\n\n"
                    "Botões:\n"
                    "• Copiar JSON: copia a prévia atual.\n"
                    "• Gerar JSON: salva um arquivo .json no disco.\n\n"
                    "O JSON reflete o conteúdo depois das regras de processamento, incluindo expansão de quantidade "
                    "e separação por grupo quando necessário."
                ),
                False,
            ),
            (
                "Configurações",
                (
                    "A tela Configurações é dividida em cinco grupos principais:\n\n"
                    "1. Geral\n"
                    "• modo padrão de maiúsculas/minúsculas\n"
                    "• separador padrão da entrada\n\n"
                    "2. Saída\n"
                    "• usar pasta padrão para salvar a saída\n"
                    "• definir pasta padrão\n"
                    "• usar nome padrão da lista\n"
                    "• definir nome padrão\n\n"
                    "3. JSON\n"
                    "• mostrar área de JSON\n"
                    "• mostrar botão Gerar JSON\n"
                    "• mostrar botão Copiar JSON\n\n"
                    "4. Tamanhos\n"
                    "• cadastro separado por Masculino, Feminino e Infantil\n\n"
                    "5. Aparência\n"
                    "• seleção de tema da interface\n\n"
                    "Na parte inferior existem ações para:\n"
                    "• Restaurar padrões gerais\n"
                    "• Restaurar tamanhos padrão\n"
                    "• Salvar configurações"
                ),
                False,
            ),
            (
                "Tamanhos",
                (
                    "O reconhecimento de tamanhos depende do cadastro da tela Tamanhos.\n\n"
                    "Cada grupo possui:\n"
                    "• Tamanhos-base\n"
                    "• Prefixos\n"
                    "• Sufixos\n\n"
                    "Exemplos:\n"
                    "• Masculino: PP, P, M, G...\n"
                    "• Feminino: prefixo BL + base P = BLP\n"
                    "• Infantil: base 8 + sufixo A = 8A\n\n"
                    "Isso significa que o programa não reconhece apenas um texto fixo; ele reconhece o conjunto "
                    "montado a partir do cadastro.\n\n"
                    "Se um tamanho válido não estiver sendo lido corretamente, a primeira coisa a revisar é o "
                    "cadastro de tamanhos.\n\n"
                    "Também é esse cadastro que define o grupo do tamanho, e esse grupo influencia:\n"
                    "• a saída organizada\n"
                    "• a separação por linha\n"
                    "• o campo Gender no JSON"
                ),
                False,
            ),
            (
                "Mensagens de erro e revisão",
                (
                    "Alguns erros comuns do processamento:\n\n"
                    "• Sem TAM reconhecido\n"
                    "A linha não possui nenhum tamanho válido para o cadastro atual.\n\n"
                    "• Mais de 6 TAMs na linha\n"
                    "A linha excedeu o limite atual de tokens de tamanho aceitos.\n\n"
                    "• Nenhuma linha válida encontrada\n"
                    "A entrada não gerou linhas utilizáveis depois da leitura.\n\n"
                    "• Separador inválido\n"
                    "O separador informado não pôde ser usado corretamente.\n\n"
                    "Quando ocorre erro em uma linha específica, o programa tenta levar o cursor até a linha para "
                    "facilitar a correção."
                ),
                False,
            ),
            (
                "Dúvidas comuns",
                (
                    "1. Meu tamanho não foi reconhecido. O que revisar?\n"
                    "Revise o separador da entrada e o cadastro de tamanhos.\n\n"
                    "2. Usei 4-PP e saiu em várias linhas. Isso é erro?\n"
                    "Não. Essa é a regra correta: quantidade no tamanho é expandida em várias linhas.\n\n"
                    "3. Coloquei PP e BLP na mesma linha e o programa separou. Isso é esperado?\n"
                    "Sim. Tamanhos de grupos diferentes não devem ficar misturados na mesma linha de saída.\n\n"
                    "4. O link não funcionou. O que pode ser?\n"
                    "O link pode estar inacessível, pode não começar com http/https ou pode não retornar JSON válido.\n\n"
                    "5. A prévia JSON sumiu.\n"
                    "Verifique na tela Configurações > JSON se a área de JSON está habilitada.\n\n"
                    "6. Quero padronizar várias palavras de uma vez.\n"
                    "Use o painel Localizar e substituir antes de processar."
                ),
                False,
            ),
        ]

        self.sections.clear()

        for title, body, starts_open in sections_data:
            section = ManualSection(
                self.scroll_frame,
                title=title,
                body=body,
                starts_open=starts_open,
            )
            section.pack(fill="x", pady=(0, 10))
            self.sections.append(section)

    def _on_frame_configure(self, _event=None) -> None:
        self.canvas.configure(scrollregion=self.canvas.bbox("all"))

    def _on_canvas_configure(self, event) -> None:
        self.canvas.itemconfigure(self.canvas_window, width=event.width)

    def _bind_mousewheel(self) -> None:
        self.canvas.bind_all("<MouseWheel>", self._on_mousewheel)

    def _on_mousewheel(self, event) -> None:
        try:
            self.canvas.yview_scroll(int(-1 * (event.delta / 120)), "units")
        except Exception:
            pass

    def refresh_theme(self) -> None:
        t = theme.active_theme()

        self.configure(bg=t.app_bg)
        self.canvas.configure(bg=t.app_bg)
        self.scroll_frame.configure(bg=t.app_bg)

        for section in self.sections:
            section.refresh_theme()