# Reorganizacao - 2026-10-13

## Resultado

- 106 GUIDs anteriores preservados, sem duplicacao.
- Cinco cenas mantidas byte a byte, sem remontagem ou alteracao de objetos.
- FBX, materiais e presets do terreno mantidos byte a byte.
- Caminhos literais dos scripts, guias e Build Settings atualizados.
- Compilacao e importacao verificadas com Unity 6000.6.3f1 / URP 17.6.0.
- Cinco cenas abertas e conferidas, sem componentes ausentes.
- Jogador, controles do canhao, shaders, agua animada e regeneracao aprovados em Play.
- Testes executados numa copia isolada, removida depois; o editor do usuario nao foi encerrado.

## Evidencias

| Arquivo | Conteudo |
| --- | --- |
| [asset-moves.json](asset-moves.json) | Caminhos antigos/novos, GUIDs e hashes anteriores |
| [unity-validation-final.log](unity-validation-final.log) | Verificacao final, Unity exit code 0 |
| [Captures/CartoonBase.png](Captures/CartoonBase.png) | Base e canhao preservados no terreno |
| [Captures/CartoonWater_A.png](Captures/CartoonWater_A.png) | Agua apos aquecimento do cache |
| [Captures/CartoonWater_B.png](Captures/CartoonWater_B.png) | Segundo quadro, confirmando movimento |

Marcadores finais: `ORGANIZATION_ASSETS_OK`, cinco `ORGANIZATION_SCENE_OK`,
`CARTOON_TERRAIN_RENDER_OK`, `CARTOON_WATER_ANIMATION_OK` e `CARTOON_SHADERS_PLAY_OK`.
O teste identificou 1.023.298 pixels com cores de biomas e 73.198 pixels variando na agua.

## Tentativas anteriores

`unity-sandbox-attempt.log`: a sandbox impediu conexao com o servico local de licenca;
a execucao foi repetida com permissao fora da sandbox.

`unity-first-play-attempt.log`: cenas e referencias passaram, mas a primeira captura
de agua ainda estava sem superficie no cache grafico. O verificador passou a aguardar
uma imagem valida antes de comparar a animacao. O teste final passou sem modificar
os shaders ou as cenas. A indexacao interna Unity Search tambem registrou uma excecao
no primeiro carregamento, excluida do monitor de erros do jogo pelo verificador existente.

A estrutura e as convencoes estao em [docs/11](../../docs/11-estrutura-do-projeto.md).
