from __future__ import annotations

import tkinter as tk

from ui import theme
from ui.widgets import make_card, make_title_label


class ManualView(tk.Frame):
    def __init__(self, parent: tk.Misc, controller) -> None:
        super().__init__(parent, bg=theme.active_theme().app_bg)
        self.controller = controller
        self._build()

    def _build(self) -> None:
        t = theme.active_theme()

        outer = tk.Frame(self, bg=t.app_bg)
        outer.pack(fill="both", expand=True)

        card = make_card(outer)
        card.pack(fill="both", expand=True)

        make_title_label(card, "Manual do Texpad").pack(fill="x", padx=18, pady=(16, 10))

        holder = tk.Frame(card, bg=t.editor_bg)
        holder.pack(fill="both", expand=True, padx=18, pady=(0, 18))
        holder.grid_rowconfigure(0, weight=1)
        holder.grid_columnconfigure(0, weight=1)

        self.txt_manual = tk.Text(
            holder,
            wrap="word",
            font=(theme.FONT_FAMILY, 10),
            bg=t.editor_bg,
            fg=t.text,
            insertbackground=t.text,
            selectbackground=t.selection,
            selectforeground="#FFFFFF",
            relief="flat",
            bd=0,
            padx=12,
            pady=12,
        )
        scroll = tk.Scrollbar(holder, command=self.txt_manual.yview)
        self.txt_manual.configure(yscrollcommand=scroll.set)

        self.txt_manual.grid(row=0, column=0, sticky="nsew")
        scroll.grid(row=0, column=1, sticky="ns")

        self._load_manual_text()
        self.txt_manual.configure(state="disabled")

    def _load_manual_text(self) -> None:
        manual_text = (
            "O que é o Texpad\n"
            "Texpad é uma ferramenta para organizar listas de entrada, padronizar texto e gerar saída estruturada.\n\n"
            "Fluxo básico\n"
            "1. Abra ou cole uma lista na área de entrada.\n"
            "2. Ajuste o separador da entrada, se necessário.\n"
            "3. Use localizar e substituir quando precisar.\n"
            "4. Clique em Processar.\n"
            "5. Copie ou salve a saída organizada.\n\n"
            "Entrada\n"
            "A entrada pode ser aberta de arquivo ou colada manualmente.\n"
            "A área de entrada possui numeração de linhas para facilitar localizar erros.\n\n"
            "Separador\n"
            "O separador padrão é vírgula.\n"
            'Você pode usar "\\t" para tabulação.\n\n'
            "Maiúsculas e minúsculas\n"
            "O modo de texto afeta apenas strings comuns.\n"
            "Campos de tamanho continuam sempre em maiúsculas.\n\n"
            "Localizar e substituir\n"
            "O editor possui painel retrátil para localizar, navegar entre ocorrências e substituir texto.\n\n"
            "Saída organizada\n"
            "A saída fica na área da direita e também mostra numeração de linhas.\n"
            "Você pode copiar ou salvar a saída.\n\n"
            "JSON\n"
            "As opções de JSON ficam ocultas por padrão e podem ser ativadas nas configurações.\n\n"
            "Configurações\n"
            "Você pode ajustar pasta padrão, nome padrão, tema da interface e cadastro de tamanhos.\n\n"
            "Tamanhos\n"
            "Os grupos Masculino, Feminino e Infantil podem ser configurados separadamente.\n"
            "Cada grupo aceita tamanhos-base, prefixos e sufixos.\n\n"
            "Temas\n"
            "O Texpad possui tema próprio e um tema inspirado na interface do SisBolt.\n"
        )

        self.txt_manual.delete("1.0", "end")
        self.txt_manual.insert("1.0", manual_text)

    def refresh(self) -> None:
        pass