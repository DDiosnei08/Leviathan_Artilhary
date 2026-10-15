# 12 - Interacao com instrumentos fisicos

## Direcao

As informacoes da operacao pertencem aos objetos do mundo: ponteiros, graduacoes,
placas e instrumentos sobre a mesa. Os Canvas, imagens e textos sobrepostos das
quatro cenas de jogo/teste foram retirados, incluindo a mira desenhada. Nao foram
substituidos por Canvas em World Space. As inscricoes novas sao malhas TextMesh.

## Interacao

- Em primeira pessoa, aponte a camera para o controle e segure o botao esquerdo.
- A camera e o movimento horizontal ficam travados durante o ajuste; a gravidade continua.
- Arraste em circulo para volantes/manivelas. Horizontal e Vertical sao modos alternativos.
- Shift reduz o passo para 20%, permitindo ajustes finos.
- O controle continua capturado se o mouse sair do collider; soltar encerra o ajuste.
- Esc, perda de foco, troca de camera, desativacao, obstaculo ou afastamento cancelam.
- Botoes afundam ao apertar, executam uma vez por clique e retornam ao soltar.
- O cursor normal aparece no modo de estacao; nao ha cursor/mira desenhado pelo jogo.

Tab e E continuam disponiveis para as cameras do prototipo. O reset de mira por R
foi removido; use o botao fisico ZERAR PECA sobre a mesa. Na mesa, ZERAR MAPA apenas
zera o transferidor mecanico, nao apaga dados de um mapa operacional futuro.

## Componentes

### CommandRoomInteractor

Um por camera de interacao. `View Camera` aponta para a camera ativa; `Player Controller`
aponta para o jogador se houver movimento a bloquear. `Interact Distance` e `Interact Mask`
definem o alcance e os colliders aceitos. Deixe `Use Screen Center` ligado em primeira
pessoa e desligado na camera fixa com cursor livre. O rig ativa somente o interactor
da vista selecionada. Nao adicione um interactor adicional em paralelo para a mesma camera.

O clique e pesquisado uma unica vez. O interactor encontra o componente no collider
ou em seu pai e mantem a captura ate liberar/cancelar. Bloqueios do jogador possuem
donos: soltar um controle nao remove o bloqueio de outra estacao. Zonas de foco tambem
respeitam a captura e nao movem a camera durante o ajuste.

### RotaryInstrumentInteraction

Para manivelas, seletores e outros mecanismos, adicione um collider ao objeto raiz
ou aos filhos e este componente ao pai. Configure:

| Campo | Uso |
| --- | --- |
| Wheel Visual | Transform que deve girar, separado da carcaca fixa |
| Wheel Center | Centro real do eixo; sem referencia, usa o visual |
| Rotation Axis | Eixo local do visual, por exemplo Z para volante frontal ou Y para manivela horizontal |
| Drag Mode | Circular, Horizontal ou Vertical |
| Invert Direction | Inverte o sentido do ajuste |
| Degrees Per Pixel | Sensibilidade dos modos lineares |
| Dead Zone Pixels | Evita instabilidade junto ao centro |
| Max Angle Step | Limita saltos por amostra de mouse |
| Limit Rotation / Minimum / Maximum | Batentes mecanicos opcionais |
| On Rotation | Evento com a variacao aceita, para mecanismos relativos |
| On Value Changed | Evento com a posicao angular, para ponteiros/seletores |

O arraste circular e calculado no plano fisico do eixo, compensando a perspectiva.
Se o controle estiver quase de perfil para a camera, prefira arraste linear: o plano
de rotacao de perfil nao oferece um movimento circular observavel.

### HandwheelInteraction

Especializacao do componente anterior. Preserve `Controller` e `Control Type`
(Azimuth/Elevation). `Degrees Per Turn` e o quanto a peca varia por volta completa.
Os batentes de elevacao do canhao limitam tambem o giro visual do volante, sem deriva.
A antiga referencia de camera foi preservada oculta apenas por compatibilidade;
a camera agora pertence ao interactor. O volante nao consulta o mouse por conta propria.

### PhysicalPushButton

Adicione ao pai com collider. `Button Visual` e a tampa movel; `Press Axis` e o eixo
local de deslocamento. `Press Travel` controla o afundamento; `Return Speed` controla
a mola de retorno. Conecte `On Pressed` e `On Released` no Inspector aos metodos desejados.
Um botao segurado nao repete o comando. A visualizacao nao exige Canvas ou imagem de UI.

### Ponteiros

`ArtilleryGaugeDriver` continua usando centros e ponteiros do modelo importado.
`MechanicalDialIndicator.SetReading(float)` gira um ponteiro mecanico generico,
conservando sua orientacao original. A manivela da mesa envia sua posicao por evento
ao transferidor; os botoes enviam reset ao instrumento ou ao canhao real.

## Cenas e montagem

As referencias das quatro cenas existentes foram atualizadas sem remontar o terreno,
reposicionar a base ou substituir o canhao. A cena SampleScene do template nao mudou.
Os montadores tambem deixaram de criar HUD. Para novos prototipos:

- `PhysicalInstrumentBuilder` cria instrumentos simples de blockout e conexoes.
- `Tools > Leviathan > Interaction > Configure Physical Controls In Open Scene`
  retira Canvas e prepara cameras/controles na cena aberta. Esse comando altera a cena;
  duplique-a antes de aplicar em uma cena personalizada. Nao e necessario repeti-lo nas cenas entregues.
- Os botoes e a manivela da mesa sao demonstracoes do sistema de interacao, nao uma
  calculadora balistica ou um mapa com triangulacao implementados.

Os scripts antigos DialMarks/DialLabels baseados em UI nao sao usados nas cenas atuais.
Nao os adicione aos novos instrumentos; use geometria de graduacoes e ponteiros.

## Verificacao

`PhysicalInteractionSceneSetup.ApplyBatch` atualiza as quatro cenas em uma copia isolada.
`PhysicalInteractionValidation.VerifyPlayBatch` testa captura por raycast, rotacao circular,
arraste linear, ajuste fino, zona morta, batentes, reversao, botoes, bloqueios independentes,
cancelamentos e ausencia de Canvas, e salva capturas em `Artifacts/Interaction/Captures`.

Os arquivos anteriores das quatro cenas ficam em `Artifacts/Interaction/BeforeScenes`
como registro desta migracao; nao fazem parte dos assets carregados no jogo.

Validado em 2026-10-13 com Unity 6000.6.3f1 / URP 17.6.0, em copia isolada. Os testes
funcionais e o clique nos dois volantes originais pela camera do operador passaram.
A verificacao das cinco cenas confirmou componentes/referencias validos; o teste de
regressao confirmou jogador, canhao, cores, agua animada e regeneracao. O limite de
elevacao de 90 graus configurado na demonstracao foi preservado.

As faces dos volantes originais receberam pequenas marcas de giro em geometria,
sem troca dos modelos. Os rotulos da mesa foram conferidos por captura e ajustados
para nao se sobrepor. Os resultados ficam em [registro de validacao](../Artifacts/Interaction/README.md),
com [operador sem HUD](../Artifacts/Interaction/Captures/Operator_NoHUD.png) e
[mesa com controles](../Artifacts/Interaction/Captures/Map_PhysicalControls.png).

Depois da importacao dos scripts, reabra a cena para carregar os objetos atualizados.
