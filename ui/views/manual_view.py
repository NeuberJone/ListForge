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
                "Esta tela é a base do manual do programa. "
                "O conteúdo abaixo ainda pode ser refinado conforme a interface e o fluxo forem sendo fechados."
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
                "Aqui vai entrar a explicação geral do programa: para que ele serve, qual é o fluxo principal "
                "e o que o usuário consegue fazer com a entrada, a saída organizada e o JSON.",
                True,
            ),
            (
                "Interface do programa",
                "Aqui vamos explicar a interface principal, incluindo menu lateral, editor, configurações e manual. "
                "Também é o lugar certo para descrever rapidamente a função de cada área da tela.",
                False,
            ),
            (
                "Como montar a entrada",
                "Esta seção vai explicar como montar a lista de entrada, quais separadores podem ser usados "
                "e como evitar erros de leitura antes de processar.",
                False,
            ),
            (
                "Como o editor interpreta cada linha",
                "Aqui vai entrar a regra real de leitura da linha. "
                "Essa seção deve ser escrita por último, depois de confirmar exatamente como o parser trata "
                "nome, apelido, tipo sanguíneo, tamanhos e demais campos.",
                False,
            ),
            (
                "Preparação da lista",
                "Esta seção vai explicar o separador da entrada, o botão de padrão, a remoção de espaços "
                "desnecessários e o modo de maiúsculas/minúsculas.",
                False,
            ),
            (
                "Localizar e substituir",
                "Aqui vai a explicação do campo Localizar, do campo Substituir por, da opção de diferenciar "
                "maiúsculas/minúsculas e dos botões Localizar, Anterior, Próximo, Substituir, Substituir tudo "
                "e Limpar destaque.",
                False,
            ),
            (
                "Saída organizada",
                "Esta seção vai explicar o que aparece na saída, quando ela é gerada e como usar os botões "
                "de copiar e salvar o resultado.",
                False,
            ),
            (
                "JSON",
                "Aqui vai a explicação da prévia JSON, quando ela aparece, quando pode ficar oculta e como usar "
                "os botões Gerar JSON e Copiar JSON.",
                False,
            ),
            (
                "Configurações",
                "Esta parte vai descrever pasta padrão de saída, nome padrão da lista, opções de JSON, tema "
                "e o efeito de cada configuração no uso do programa.",
                False,
            ),
            (
                "Tamanhos",
                "Aqui vamos explicar os grupos Masculino, Feminino e Infantil, além de tamanhos-base, prefixos "
                "e sufixos e como isso afeta a interpretação da entrada.",
                False,
            ),
            (
                "Arquivos, salvamento e backups",
                "Esta seção vai cobrir Abrir Lista, Salvar Entrada, Salvar Entrada Como, Desfazer e pasta de backups.",
                False,
            ),
            (
                "Dúvidas comuns",
                "Aqui entra o FAQ final, com perguntas como: por que meu tamanho não foi reconhecido, "
                "qual separador usar, por que o JSON não apareceu e como corrigir vários textos de uma vez.",
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