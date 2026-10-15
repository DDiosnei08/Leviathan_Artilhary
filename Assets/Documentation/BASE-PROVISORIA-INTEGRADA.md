# Base provisoria integrada

## Abrir e testar

1. Abra o projeto Leviathan2 e espere o Unity terminar a importacao.
2. Abra `Assets/Scenes/Demos/Base_Provisoria_Integrada.unity`.
3. Clique em Play. A primeira camera mostra a base inteira no mapa.
4. Pressione Tab para entrar no jogador. Tab novamente retorna a vista geral.
5. Caminhe com WASD, olhe com o mouse, corra com Shift e pule com Espaco.
6. Aproxime-se da estacao atras do canhao e pressione E para usar os volantes.
7. Nesse modo, o mouse fica livre. Segure o clique em um volante e faca o movimento circular para ajustar a mira. Shift permite ajuste fino; E retorna ao jogador. Os volantes tambem funcionam diretamente em primeira pessoa.
8. Na sala de comando, mire em uma alavanca e segure o clique: movimento horizontal altera o azimute; vertical altera a elevacao. A camera fica parada enquanto voce ajusta.
9. Esc libera o cursor enquanto voce esta andando. Clique na Game View para retomar.

## O que ja esta ligado

A cena preserva o canhao, os volantes, os dials e os instrumentos da cena de artilharia. A sala de comando, a mesa de mapa, os suportes de municao e o jogador vieram da cena de controle do jogador. As duas alavancas da sala comandam o mesmo canhao externo.

A base tem fundacao com colisao, acesso por rampa, sacos de areia, caixas de suprimentos, sinalizacao e iluminacao. O local de montagem foi escolhido procurando um trecho seco e com pouca variacao de altura.

O terreno usa uma copia das configuracoes procedurais, com a mesma seed e relevo do mundo original. Esta cena usa 3 x 3 chunks (3,6 km de lado nas configuracoes iniciais) para carregar uma area de demonstracao menor. O asset original `LeviathanTerrain.asset` continua disponivel.

## Componentes e referencias

As referencias abaixo ja estao preenchidas. Nao e necessario arrastar os componentes novamente para o primeiro teste.

| Objeto | Componente | Referencias principais |
| --- | --- | --- |
| Base Provisoria | IntegratedBattlefieldRig | Terrain, Artillery, Player, Interactor, Operator Interactor, as tres cameras e Handwheels |
| Procedural Battlefield | TerrainWorldController | Terrain Settings = BaseProvisoriaTerrain; Terrain Material = TerrainCartoon; Weather Provider no mesmo objeto |
| Procedural Battlefield | WorldWeatherProvider | Condicoes de clima usadas pelo terreno |
| Base Provisoria/ArtilleryBattery | ArtilleryTestController | Azimuth Pivot e Elevation Pivot do canhao real; sem leituras de UI |
| Volantes da ArtilleryBattery | HandwheelInteraction | Controller = ArtilleryBattery; Wheel Visual, Wheel Center e Rotation Axis |
| Instrumentos da ArtilleryBattery | ArtilleryGaugeDriver | Artillery Controller = ArtilleryBattery; centros e ponteiros do modelo |
| Base Provisoria/Player | CharacterController | Capsula de colisao e movimento do jogador |
| Base Provisoria/Player | LeviathanPlayerController | Camera Pivot = PlayerCamera |
| Base Provisoria/Player | CommandRoomInteractor | View Camera = PlayerCamera; Use Screen Center ligado; Lock Player While Interacting ligado |
| Alavancas da CommandPost | ArtilleryCommandLever | Artillery Controller = ArtilleryBattery; Mode = Azimuth ou Elevation; Lever Pivot original |
| OperatorCamera | CommandRoomInteractor | View Camera = OperatorCamera; Use Screen Center desligado |
| MapPhysicalInstruments | RotaryInstrumentInteraction, PhysicalPushButton, MechanicalDialIndicator | Manivela, transferidor e botoes ligados por eventos persistentes |
| OverviewCamera, PlayerCamera, OperatorCamera | Camera e AudioListener | O rig mantem apenas uma camera e um listener ativos por vez |

O campo Viewer do terreno acompanha a camera selecionada. A geracao automatica antiga detecta o TerrainWorldController desta cena e nao cria um segundo mapa.

## Ajustes opcionais no Inspector

- Para comecar andando, selecione Base Provisoria e altere Initial View para Player. Para a primeira visualizacao, mantenha Overview.
- Para modificar relevo, vegetacao e tamanho do mapa, edite `Assets/Settings/Terrain/BaseProvisoriaTerrain.asset`.
- Para expandir o mapa mantendo a janela de carregamento, aumente Finite Map Chunks Per Side para um numero impar e deixe Finite Map Full Load Max Chunks Per Side em 3. O campo Streaming Radius controla quantos chunks vizinhos sao carregados.
- Para ajustar a vista geral, mova OverviewCamera e mantenha a camera apontada para a base.
- Para mudar a sensibilidade dos volantes, altere Degrees Per Turn e Max Angle Step em cada HandwheelInteraction.
- Para mudar os limites do canhao, ajuste Min Elevation e Max Elevation no ArtilleryTestController.
- Se um ponteiro de modelo girar no eixo incorreto, ajuste Axis, Invert e Zero Offset no ArtilleryGaugeDriver.
- As tres cameras usam a mesma tag MainCamera somente quando estao selecionadas. Evite habilitar cameras manualmente durante Play.

## Reconstruir depois de alterar o terreno

Depois de mudar a seed, a altura ou o formato do relevo, saia do Play e use `Leviathan > Create Integrated Battlefield Scene`. Isso refaz a cena a partir das duas cenas de origem e procura um novo local para a base.

Esse comando substitui a cena integrada. Se voce ja personalizou os objetos dela, primeiro duplique a cena pelo Project para preservar sua versao. Para apenas atualizar o preview do mapa, use Regenerate Terrain no TerrainWorldController; isso nao reposiciona a base.

Para incluir a cena em um executavel, abra File > Build Profiles > Scene List e adicione a cena integrada. O terreno possui referencias serializadas ao asset e ao material, entao a cena nao depende de carregar esses arquivos pelo editor durante o jogo.

## Limites desta etapa

Os materiais cartum e a geracao de agua foram integrados posteriormente. Consulte `docs/10-shaders-terreno-e-agua.md` para parametros, limites e a cena de demonstracao.

A mesa de mapa possui uma manivela que gira um transferidor fisico e botoes de zerar o instrumento e a mira da peca. Nao ha Canvas, HUD, mira desenhada ou leituras digitais sobre a camera. Consulte `docs/12-interacao-instrumentos-fisicos.md` para adicionar controles. A mesa ainda nao realiza triangulacao ou medicoes do terreno; os estoques de municao continuam cenograficos. Ainda nao ha disparo, balistica, dano ou inventario conectado.
