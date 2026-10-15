# Base giratoria integrada

Atualizacao: a deformacao radial/aterro descrita nesta etapa foi substituida
por posicionamento sobre terreno naturalmente plano e recorte geometrico do fosso.
Consulte [ambiente, vegetacao e erosao](15-ambiente-vegetacao-e-erosao.md) para o comportamento atual.

## Abrir e testar

- Projeto: `C:\ProjetoENTEC\Leviathan2`. Unity **6000.6.3f1**, URP **17.6.0**.
- Cena: `Assets/Scenes/Leviathan_BaseGiratoria.unity`.
- A cena abre em visao geral. **Tab** alterna entre visao geral e jogador.
- **WASD** anda, mouse olha, **Shift** corre e **Espaco** salta.
- Perto do posto de um canhao ou da mesa, **E** entra no posto; **E** novamente sai.
- No posto, o mouse fica livre. Segure e arraste o aro dos volantes; **Shift** permite ajuste fino.
- Os botoes fisicos selecionam municao e uma das cinco cargas, carregam e disparam.
- Na mesa, a manivela gira o ponteiro do transferidor; o botao zera o instrumento.
- A mesa tambem permite carregar ambos os canhoes e disparar ambos. Nao ha Canvas ou HUD.
- O disparo e bloqueado enquanto a plataforma esta girando. Aguarde sua parada mecanica.

## Arquivos e Inspector

| Recurso | Responsabilidade |
| --- | --- |
| `Assets/Models/World/BaseGiratoria/BaseCompleta.fbx` | Copia importada do FBX fornecido; original em Downloads nao alterado |
| `Assets/Prefabs/BaseGiratoria.prefab` | Modelo, colisoes, pivos, dois canhoes, instrumentos e mesa |
| `Assets/Scripts/Gameplay/Base/BaseSpawner.cs` | Uma instancia ativa, escolha de local, ciclo de regeneracao e clone das configuracoes em Play |
| `BaseSite.cs` | IHeightSource, configuracoes, dados imutaveis, solver e registro com dono unico |
| `ProceduralHeightSource.cs` | Altura natural por amostragem das mesmas triangulacoes do terreno |
| `PlatformRotator.cs` | Velocidade maxima, aceleracao, frenagem e rotacao compartilhada |
| `CannonController.cs` | Elevacao, carga, ferrolho, estoque reservado, disparo e recuo por canhao |
| `BaseHandwheelInteraction.cs` | Volantes fisicos; ajuste `gearRatio` para a reducao mecanica |
| `BaseGaugeDriver.cs` | Ponteiros analogicos e placas fisicas de estado, municao e propelente |
| `BaseGameRig.cs` | Jogador, cameras, postos e acompanhamento do conves em movimento |
| `BasePhysicalMap.cs` | Carta fisica monocromatica vinculada a semente, quadrados de 1 km / subdivisoes de 100 m |
| `Assets/Settings/Terrain/BaseGiratoriaTerrain.asset` | Configuracao especifica da nova cena, preservando os assets anteriores |
| `Assets/Editor/BaseIntegration` | Importacao, construcao idempotente da cena e testes em Play |

No objeto **Base Unica por Semente**, configure `heightSource`, `basePrefab`, area de busca,
raios de platô/transicao, escavacao e exclusao. A semente efetiva vem de
`terrainSettings.noiseSettings.seed`; nao e uma segunda semente independente.
Em Play a configuracao do terreno e clonada: editar a semente runtime nao altera o asset salvo.
O spawner identifica a mudanca e substitui a base, mantendo somente uma instancia ativa.
`Gerar base unica (Play)` no menu de contexto reavalia o local. Fora do Play, use
**Leviathan > Base Giratoria > Criar ou reconstruir cena** para atualizar a preview.
Os mesmos parametros e a mesma
semente repetem a mesma posicao, altitude e direcao; mudar relevo/area de busca tambem muda o resultado.

No prefab, `Rotacao_Y_Unity` e o pivo vertical. `maxSpeed`, `acceleration` e `braking`
controlam a inercia. Cada `Controle_Canhao_E/D` permite ajustar limites e velocidade
da elevacao, angulo/tempo do ferrolho, tempo de carga, recuo em metros e recuperacao.
As referencias dos pivos ja estao preenchidas. `ammo` possui HE/AP e estoques independentes.
Os eventos `onLoadStart`, `onLoadFinished`, `onFire` e `onStateChanged` aceitam som e efeitos.

## Adaptacao do FBX

O FBX recebido tinha elevacao de 40 graus, recuo aplicado e `REF_Jogador_2m`.
No prefab a pose de repouso foi restaurada e o boneco desativado. O importador usa
**Bake Axis Conversion**, preserva a hierarquia e nao importa a animacao de teste.
Os materiais originais foram convertidos para materiais URP em `Assets/Materials/BaseGiratoria`.
Meshes continuam referenciando o FBX; nao foram duplicados.

Os empties importados possuem escala 100 e eixo convertido. Por isso foi criado
um pivo Unity vertical acima de `PIVO_Plataforma_Giratoria`, sem girar em torno do eixo
incorreto. Os marcadores `Canhao_E/D_BOCA`, `Canhao_E/D_POSTO_OPERADOR` e
`BASE_CAMERA_CONVES` foram adicionados no prefab porque nao estavam no arquivo recebido.
O tiro usa o eixo local Y da boca; o recuo e convertido de metros para o espaco do pai.
O ferrolho tem sinal configurado por lado. O prefab e uma hierarquia desvinculada do
Model Prefab para permitir esses ajustes; ao substituir o FBX, reconstrua e valide.

## Terreno e semente

O spawner roda antes do controlador do terreno. O solver usa a altura **natural**,
sem reamostrar o platô da propria base. O resultado e copiado para cada snapshot
de geracao; threads nao consultam um registro estatico mutavel durante a geracao.

`HeightMapGenerator` preserva alturas normalizadas/biomas e produz alturas em metros
ja conformadas, incluindo a borda usada nas normais. `BaseTerrainMesh` refina somente
as celulas que tocam a influencia do fosso (aproximadamente 0,5 m de espacamento).
O resto do mundo continua na resolucao/LOD existente. Chunks que tocam a base ficam
em LOD 0 para conservar o fosso. Malhas grandes usam indices UInt32.
As bordas externas refinadas ficam sobre os triangulos originais, evitando frestas.

Grama, agua, props e POIs/estradas sao excluidos da area de influencia mais uma margem.
Essa margem tambem evita raizes de grama desalinhadas com o refinamento local.
O projeto atual deve manter o objeto do terreno na origem, sem rotacao ou escala:
os sistemas existentes e este adaptador usam coordenadas mundiais em metros.

## Cenas e ferramentas antigas

Somente a nova cena e `Assets/Scenes/Tests/Test_ArtilleryControls/Test_ArtilleryControls.unity`
ficam em Assets. As outras cenas e respectivos `.meta` foram arquivados em
`Artifacts/BaseIntegration/ArchivedScenes`, fora da importacao da Unity.
A nova cena e a cena habilitada no Build Settings; o teste fica listado, desabilitado.

Os scripts anteriores nao foram apagados. Os construtores antigos sao ferramentas
historicas e dependem das cenas arquivadas: use agora **Leviathan > Base Giratoria**.
Um `CommandRoomTestSceneBuilder` duplicado em `CommandAndPlayerTest` foi isolado no
namespace `ProjectLeviathan.Legacy`; duas ligacoes a campos de HUD que nao existem mais
foram removidas para permitir compilacao. O script e o seu `.meta` foram preservados.

## Validacao e limites

**Leviathan > Base Giratoria > Validar integracao em Play** testa os pivos, os dois
ciclos de carga, reserva/reembolso de municao, recuo, rotacao e regeneracao.
Logs, testes e capturas estao em `Artifacts/BaseIntegration`.
O resultado final foi **61 verificacoes aprovadas**, incluindo sair/entrar novamente
em Play com recarregamento de dominio e cena desativados.
As sementes utilizadas no teste sao **-265324695**, **12345** e **9173**.
Raycasts em 12 pontos verificam que o terreno fica abaixo do piso do fosso.
Tambem sao testados cliques reais por raycast nos botoes fisicos.

O teste usa uma copia isolada para nao interromper o editor do projeto.
Avisos de rotas sem conexao em encostas sao limitacoes ja existentes do gerador.
Um erro de indexacao de `UnityEditor.Search` em batchmode e registrado no log,
mas separado dos erros de gameplay nos testes.

Uma demo antiga foi salva novamente durante o trabalho. Essa versao mais recente foi
preservada separadamente em `Artifacts/BaseIntegration/ArchivedScenes_Resaved` antes
da remocao. Abra a nova cena no editor para nao recriar a antiga ao salvar uma aba antiga.

- A carta e um mapa de relevo **aproximado**, amostrado de forma mais grosseira que os chunks. Nao e fotografia de reconhecimento.
- Linhas de medicao, triangulacao, calculadoras, vento balistico, relatos, impactos e dano ainda nao foram implementados nesta integracao.
- A selecao de cargas e municao esta ligada aos controles/estado; ainda nao ha tabela balistica que transforme isso em alcance.
- `projectilePrefab` fica vazio por padrao: o disparo executa estado, evento e recuo, sem simular viagem de projetil.
- O solver exige terreno seco. Configuracoes patologicas de mapa totalmente submerso geram erro explicito; nao cria uma base flutuante ou fora do mapa.
- O limite de 2 ms da grama nao foi remedido para esta cena. O FBX adiciona muitos renderers/colisores; meca a cena completa no hardware alvo.
- Gerar ou trocar semente e uma operacao de preparacao do mundo, nao uma acao sem custo: solver, carta e meshes podem produzir um pico de CPU.
- Os materiais e a geometria de gameplay foram validados no pipeline URP/DX11 instalado; HDRP nao foi adicionado.
