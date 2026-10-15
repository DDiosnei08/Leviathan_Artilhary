# Instrumentos fisicos - 2026-10-13

## Entrega

Quatro cenas atualizadas, mantendo GUIDs, terreno, posicao da base, canhao e modelos:
as duas cenas de teste e as duas demonstracoes. Canvas e HUD foram removidos, incluindo
textos digitais, imagens de UI e mira desenhada. Os montadores nao recriam esses elementos.

Controles compartilhados por raycast: volantes de artilharia, alavancas, manivela de
transferidor e botoes fisicos da mesa. Os volantes originais receberam marcas visuais
de giro em malhas pequenas; seus modelos nao foram substituidos.

## Validacao

- Unity 6000.6.3f1, URP 17.6.0; compilacao/importacao aprovadas.
- Rotacao circular, arraste linear, ajuste fino e passagem pela zona morta aprovados.
- Captura mantida fora do collider; somente um controle por interactor.
- Batente de elevacao e reversao aprovados, respeitando o limite existente de 90 graus.
- Bloqueios independentes do jogador e cancelamento por distancia/desativacao aprovados.
- Botao pressionado visualmente, comando unico, release e eventos persistentes aprovados.
- Ponteiros fisicos respondendo ao canhao; nenhum Canvas na demonstracao.
- Os dois volantes originais foram selecionados por raycast pela camera do operador.
- Cinco cenas sem componentes ausentes; materiais, modelo e Build Settings validos.
- Regressao de jogador, artilharia, biomas, agua animada e regeneracao aprovada.
- Rotulos da mesa e instrumentos conferidos em capturas, sem sobreposicao.

Logs autoritativos, ambos com exit code 0:
[teste fisico](Logs/physical-play-delivery.log),
[cenas e regressao](Logs/scene-shader-regression.log).
O [log da montagem](Logs/scene-setup-final.log) registra as quatro cenas atualizadas.
Os outros logs de Play registram ajustes do verificador: viewport de batch, precisao
de coordenadas e uso do limite configurado, sem alteracao desse limite no projeto.

## Capturas

- [Operador sem HUD](Captures/Operator_NoHUD.png)
- [Manivela, transferidor e botoes da mesa](Captures/Map_PhysicalControls.png)
- [Base preservada](Captures/CartoonBase.png)
- [Agua - quadro A](Captures/CartoonWater_A.png)
- [Agua - quadro B](Captures/CartoonWater_B.png)

Os quatro arquivos anteriores das cenas ficam em `BeforeScenes`; os hashes usados
para evitar sobrescrever salvamentos feitos durante a tarefa ficam em `scene-source-hashes.json`.
As verificacoes foram feitas em copia isolada; o editor aberto do usuario nao foi encerrado.

Configuracao e limites: [docs/12](../../docs/12-interacao-instrumentos-fisicos.md).
O transferidor e os botoes demonstram o sistema de interacao; mapa operacional,
triangulacao, calculadoras e disparo continuam pendentes.
