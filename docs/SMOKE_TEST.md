# Smoke Test de Release do ListForge

Use este roteiro antes de publicar uma versao do ListForge. Ele combina validacao automatica dos artefatos com uma conferida manual curta no aplicativo.

## 1. Validacao automatica

Na raiz do projeto:

```powershell
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
.\test-release.ps1 -Version X.Y.Z
```

Quando a versao tiver `update.json`, o script tambem valida o manifest. Para uma build local sem manifest publico:

```powershell
.\test-release.ps1 -Version X.Y.Z -SkipUpdateManifest
```

## 2. Artefatos esperados

O script deve confirmar estes arquivos:

- `bin\Release\dist\X.Y.Z\ListForge-Installable\ListForge.exe`
- `bin\Release\dist\X.Y.Z\ListForge-Portable-OneFile\ListForge-vX.Y.Z.exe`
- `bin\Release\dist\X.Y.Z\ListForge-Trial-OneFile\ListForge-Trial-vX.Y.Z.exe`
- `bin\Release\dist\X.Y.Z\Installer\ListForge-Setup-X.Y.Z.exe`
- `bin\Release\dist\X.Y.Z\SHA256SUMS.txt`
- `bin\Release\dist\X.Y.Z\Release\ListForge-Setup-X.Y.Z.exe`
- `bin\Release\dist\X.Y.Z\Release\ListForge-Trial-vX.Y.Z.exe`
- `bin\Release\dist\X.Y.Z\Release\ListForge-vX.Y.Z.exe`
- `bin\Release\dist\X.Y.Z\Release\SHA256SUMS.txt`
- `bin\Release\dist\X.Y.Z\Release\RELEASE_NOTES_X.Y.Z.txt`
- `bin\Release\dist\X.Y.Z\Release\update.json`, quando houver manifest publico.

## 3. Amostras

Arquivos de entrada para teste manual:

- `TestAssets\Samples\lista-valida-simples.txt`
- `TestAssets\Samples\lista-com-erro.txt`
- `TestAssets\Samples\lista-avancada.txt`
- `TestAssets\Samples\lista-completa.txt`
- `TestAssets\Samples\lista-grande-base.txt`

Para teste de volume manual, copie o conteudo de `lista-grande-base.txt` varias vezes ate formar uma lista maior.

## 4. Roteiro manual minimo

1. Abra o onefile completo `ListForge-vX.Y.Z.exe`.
2. Abra `lista-valida-simples.txt`.
3. Clique em Processar ou Forjar.
4. Confira se a Lista organizada foi gerada sem erro.
5. Gere a Previa JSON e copie o JSON.
6. Ative e desative o Modo Forja, se ele existir na versao.
7. Digite na entrada e confirme que os efeitos visuais nao acumulam nem travam.
8. Abra `lista-com-erro.txt`.
9. Processe e confirme que a pre-validacao mostra erro amigavel e destaca a linha.
10. Abra `lista-completa.txt`.
11. Processe e confira quantidade, apelido, tipo sanguineo e meia na saida.
12. Gere JSON e confira que meia nao aparece no JSON.
13. Ative Lista avancada.
14. Abra `lista-avancada.txt`.
15. Configure tipos de peca diferentes e confirme que o JSON respeita a ordem escolhida.
16. Salve a saida textual.
17. Gere um JSON em arquivo.
18. Gere o pacote de suporte pelo menu lateral.
19. Abra a tela Sobre e confira versao, edicao e atualizacoes.
20. Abra a tela Configuracoes, altere uma preferencia simples, salve e reabra o aplicativo.

## 5. Trial

1. Abra o onefile Trial `ListForge-Trial-vX.Y.Z.exe` em ambiente de teste.
2. Processe uma lista valida e confirme consumo de 1 credito.
3. Tente processar `lista-com-erro.txt` e confirme que nao consome credito.
4. Repita processamentos validos ate o limite de teste e confirme o bloqueio.
5. Confirme que a versao completa nao depende do estado Trial.

## 6. Atualizacao

Quando `update.json` for publicado:

1. Abra a versao instalada anterior.
2. Va em Sobre.
3. Use Verificar agora.
4. Confirme que a nova versao aparece.
5. Use Baixar agora.
6. Confirme que o instalador e validado antes de abrir.

## 7. Resultado

So publique a release quando:

- testes automatizados passarem;
- `test-release.ps1` passar;
- artefatos e hashes estiverem corretos;
- smoke test manual nao encontrar regressao critica;
- release notes estiverem revisadas.
