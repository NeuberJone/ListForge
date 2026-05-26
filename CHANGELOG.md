# Changelog

Todas as mudanças relevantes do ListForge serão documentadas neste arquivo.

O formato segue uma estrutura simples inspirada em Keep a Changelog, com entradas agrupadas por versão.

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
