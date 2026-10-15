# Grama procedural GPU

## Ambiente e escopo

Implementacao para Unity **6000.6.3f1**, **URP 17.6.0**, PC, renderer Forward+ do projeto. Validacao grafica em DirectX 11 na Radeon RX 6600. Nenhum Canvas, HUD, controle da artilharia, modelo ou paleta do terreno foi substituido.

Cena pronta: `Assets/Scenes/Demos/Base_Cartum_Grama.unity`. E uma copia da demonstracao cartum, preservando base, canhao, jogador, instrumentos, agua e semente do terreno. As cenas anteriores permanecem intactas.

## Integracao com o terreno (Passo 0)

- `TerrainWorldController` cria chunks de 1200 m, normalmente com heightmap 121 x 121. O mapa grande carrega uma janela 3 x 3; a geracao sincrona tem seu proprio limite por frame. A geracao opcional em background produz dados CPU que sao aplicados na thread principal.
- `HeightMapGenerator` fornece alturas normalizadas, alturas em metros, normais com amostras extras na borda, inclinacao, bioma, agua, estradas e POIs. O indice Y do mapa cresce na direcao **-Z**.
- `MeshGenerator` reduz a grade pelos strides 1, 2, 4 e 8 e sempre inclui a ultima linha/coluna. Cada celula usa dois triangulos; interpolacao bilinear nao reproduz exatamente essa superficie.
- `CurrentLod` e o LOD solicitado, nao necessariamente a malha visivel. Foi adicionado `AppliedLod`, atualizado somente quando a nova malha e aplicada. No asset atual, os limiares efetivos sao 1800/2600/4000 m, medidos ate o centro do chunk, cujo Y e zero. Mapas pequenos totalmente carregados nao atualizam automaticamente esse LOD.
- Integracao minima: evento `ChunkCreated` no controller; eventos `DataChanged` e `Disposing`, `AppliedLod` e acesso a textura de biomas no chunk. O gerenciador tambem registra chunks ja existentes ao ativar. O diff especifico esta em `Artifacts/Grass/terrain-integration.diff`.
- Estradas e POIs sao aplicados depois da malha. A grama aguarda `WorldFeaturesReady` em mapas finitos antes de enviar a mascara. Um hash das mascaras detecta alteracoes posteriores sem reconstruir o campo em uma troca de malha equivalente.

## Arquitetura

`GrassManager` orquestra tiles, pool, uploads, geracao, culling e draws. Cada chunk possui um `GrassTile`, com heightmap RFloat, normal/densidade RGBAFloat, exclusoes e patches residentes. Nao ha um GameObject por lamina.

Nao se preenche um chunk inteiro de 1,44 km2: somente patches proximos de **30 m** sao residentes. Configuracao inicial: 5 laminas/m2, ate 64 patches, 2 geracoes e 1 upload de tile por frame. O pool e preparado ao ativar e reutilizado durante deslocamentos; buffers sao liberados em `OnDisable`/`OnDestroy`. O patch size efetivo e ajustado para dividir o chunk em partes iguais, evitando uma borda parcial desalinhada.

O compute gera cada candidato em uma grade com jitter, PCG deterministico, variacao por lamina e por pequeno grupo. O hash usa coordenadas globais da grade e a semente do terreno, inclusive para chunks negativos. Cada registro ocupa **32 bytes**: raiz float3, altura/largura half2, normal octaedrica, orientacao/dobra half2, RGB8 e seed.

A densidade vem de altura, bioma e inclinacao. Agua, estradas e POIs impedem crescimento. A menor elegibilidade entre os quatro cantos de uma celula evita invadir margens recortadas. Volumes XZ excluem fundacao/rampa; bounds de props procedurais excluem rochas e estruturas. Isso e conservador: pode deixar uma faixa seca de ate uma celula (10 m no asset atual) junto da agua ou estrada.

As raizes sao reajustadas no compute a cada culling usando **interpolacao triangular do stride aplicado**, sem recriar a distribuicao. Normais sao baseadas na grade de maior resolucao. Meshes 11/7/3 vertices sao criadas por codigo.

## Culling, LOD e sombras

O compute testa seis planos do frustum com margem para dobra/vento, distancia 3D e retencao deterministica. Quatro append buffers guardam indices: perto, medio, longe e sombras. `GraphicsBuffer.CopyCount` atualiza os argumentos sem readback CPU.

Usa-se **Graphics.RenderMeshIndirect** como caminho principal, com argumentos `GraphicsBuffer.IndirectDrawIndexedArgs`. Na Unity 6.6, instanceCount e uma propriedade: o offset para CopyCount e obtido do layout real em memoria, sem assumir nomes de campos internos.

Foi encontrada uma incompatibilidade na validacao standalone **Unity 6000.6.3f1 + DX11 + Graphics Jobs (LegacyJobified)**: RenderMeshIndirect provocou um crash nativo do Player, enquanto o smoke test com os draws de grama desligados passou. A mesma build funcionou com `-force-gfx-direct`. Por isso, nessa familia de versao e nos modos Graphics Jobs do DX11, o manager seleciona automaticamente **DrawMeshInstancedIndirect** como ponte de compatibilidade, com uma variante de shader que usa SV_InstanceID diretamente. Embora obsoleta, essa API permaneceu funcional e foi validada visualmente e por benchmark no modo original. Nenhum PlayerSettings do projeto principal foi alterado. O Inspector informa o caminho ativo. Remova a ponte somente depois de repetir os testes em uma versao em que o caminho moderno esteja corrigido. Nao foi determinada a causa interna do crash do motor.

| Faixa | Geometria | Densidade | Largura |
| --- | --- | --- | --- |
| Perto, ate 18 m | 11 vertices | 100% | 1x |
| Medio, transicao ate 42 m | 7 vertices | 42% | 1,55x |
| Longe, ate 90 m | 3 vertices | 12% | 2,5x |
| Fora de 90 m / terrain LOD 3+ | somente terreno | zero | - |

As transicoes por distancia tem 12 m. O LOD aplicado do terreno limita a qualidade, com interpolacao de 0,4 s. Uma escolha por hash distribui a troca de geometria sem duplicar cobertura; a largura varia continuamente, independentemente da malha sorteada. Dithering suaviza a perda de densidade apenas durante as transicoes, o limite de distancia e o aparecimento de patches. Nao e uma dupla renderizacao de duas malhas com alpha blend.

Sombras projetadas sao configuraveis e limitadas a 22 m, com fade nos ultimos 4 m. A lista de casters proximos nao depende do frustum principal, mantendo sombras de laminas fora da vista. Sao passes separados ShadowsOnly; os draws principais nao projetam sombras novamente. Shader inclui UniversalForward, ShadowCaster, DepthOnly e DepthNormals. Recebe luz principal, SH ambiente, sombras e fog.

Hi-Z nao foi incluido: o projeto nao disponibiliza uma piramide de profundidade apropriada; cria-la exigiria passes, sincronizacao e custo adicional antes de demonstrar ganho neste raio pequeno.

## Matematica resumida

- **Curvatura:** Bezier quadratica com P0=raiz, P1=raiz+up*h/2 e P2=raiz+up*h+dobra. Simplifica para raiz + up*h*t + dobra*t*t. A tangente e up*h + 2*dobra*t; t=0 fixa a base.
- **Rounded normals:** a normal da folha recebe uma componente lateral proporcional a coordenada -1..1 da largura. A interpolacao dos vertices simula uma superficie arredondada; junto a raiz, mistura a normal do solo.
- **View thickening:** quanto menor abs(dot(normal, direcaoDaCamera)), mais a direcao da largura se aproxima de cross(tangente, direcaoDaCamera). A folha deixa de desaparecer quando vista de lado, sem mudar a raiz.
- **Vento:** gradient noise no XZ mundial, deslocado por direcao*velocidade*tempo, mais uma oscilacao pequena por seed. A dobra cresce com t*t. O mesmo campo mundial vale para todos os chunks. `WorldWeatherProvider` pode fornecer a direcao e velocidade; um ponto de interacao opcional afasta as pontas do jogador.
- **Cor:** gradiente raiz/topo, variacao deterministica, mistura da textura existente do bioma e AO falso na base. Iluminacao difusa suave, translucencia simples e especular discreto; nao e um modelo fisico de translucencia.

## Setup e Inspector

1. Abra `Base_Cartum_Grama.unity` e entre em Play. Nao e necessario reconstruir a base.
2. Selecione `Procedural Grass`. `Terrain`, configuracao, camera, jogador e exclusoes ja estao atribuidos. A camera segue a estacao ativa do rig.
3. No componente `GrassShowcase` da base, use os menus de contexto **Show Grass Close Up** e **Show Base And Cannon** para comparar os enquadramentos. Sao comandos do Inspector, sem interface sobre a visao do jogador.
4. Ajuste `Assets/Settings/Grass/ProceduralGrass.asset`: cores, altura/largura, densidade, distancias/retencao LOD, intensidade/direcao/velocidade/escala do vento e sombras proximas.
5. Cores, vento e distancias respondem durante Play. Para densidade, tamanho de patch, seed, dimensoes das laminas, mascaras ou volumes movidos, use **Rebuild masks and distribution** no Inspector do manager. Alteracoes estruturais recriam o pool; nao sao um ajuste sem custo. Regenere o mundo ao mudar resolucao/curva/escala do terreno.
6. Em outra cena, adicione GrassManager, atribua controller/camera/configuracao e volumes de exclusao. Material e compute ja estao no asset. A criacao de malhas e automatica.

O Inspector mostra tiles por LOD, patches, contagens amostradas de laminas, memoria aproximada dos buffers/texturas GPU e bytes de GC do GrassManager.LateUpdate. As contagens GPU sao assincronas e atualizadas por patch; podem demorar alguns segundos para convergir. Gizmos sao opcionais. Nao existem leituras sincronas de buffers no loop de jogo.

## Validacao por etapas

Scripts de teste em `Assets/Editor/Grass/GrassValidation.cs`, executados em copia isolada do projeto para nao interromper o Editor principal. Logs e capturas em `Artifacts/Grass`.

- Etapa 1: C#/HLSL/DX11, patch fixo com 4624 laminas, render nao vazio e hash identico apos reconstruir.
- Etapa 2: frustum e distancia removem os candidatos esperados; casters proximos permanecem quando a camera aponta fora do campo.
- Etapa 3: base/canhao presentes, nenhum Canvas, 9 tiles, regeneracao, desligar/religar, limite de residencia e liberacao do pool. Raizes verificadas contra triangulos do terreno em LODs 0/1/2; maior erro observado antes do polimento final: 0,000137 m.
- Etapa 4: mascaras de agua/estradas/POIs/fundacao e alturas verificadas por readback explicito de teste; capturas separadas no tempo detectam movimento do vento. Os caminhos moderno e de compatibilidade produzem capturas nao vazias. A leitura sincrona existe somente sob UNITY_EDITOR e nunca e chamada pelo runtime normal.
- Etapa 5: zero GC no GrassManager.LateUpdate em 120 samples estaveis do Editor e no benchmark standalone; teleporte de um chunk, descarregamento/registro correto de 9 tiles e reutilizacao do pool, mantendo 49 patches dentro do limite de 64.

### Resultado de performance

Benchmark final na **RX 6600, DX11, LegacyJobified, 1920x1080, renderScale=1**, com 5 laminas/m2, 49 patches residentes e sombras proximas habilitadas. 1200 samples por estado, em oito blocos A/B:

| Medida | Resultado |
| --- | --- |
| GPU do frame completo com grama, mediana | 1,16528 ms |
| GPU do frame completo sem grama, mediana | 0,99980 ms |
| Incremento estimado da grama, incluindo sombras | **0,16548 ms** |
| GPU do frame completo com grama, p95 | 1,18908 ms |
| GPU do frame completo sem grama, p95 | 1,01416 ms |
| Maior GC do update da grama nos samples | **0 bytes** |
| Buffers reservados, incluindo exclusoes | 14.357.760 bytes, aproximadamente 13,69 MiB |

`Artifacts/Grass/Benchmark.json` registra configuracao e numeros; `Benchmark.csv` conserva as amostras. `Standalone_On.png` e `Standalone_Off.png` comprovam visualmente que o estado ligado desenha grama de fato. A execucao oculta nao retornou temporizacao valida; o benchmark foi concluido em janela visivel autorizada pelo usuario.

A estimativa ficou abaixo da meta de 2 ms **nessa camera e configuracao testadas**. Nao e um timestamp isolado por pass nem uma garantia para todos os enquadramentos, mudancas de parametros ou picos de geracao. O teste preliminar do caminho moderno em modo Direct tambem ficou abaixo da meta (incremento aproximado 0,72 ms, antes do ultimo polimento). Para homologacao completa, repita a medicao por pass e em movimento.

## Como medir e otimizar

Use uma Development Build em 1920x1080, renderScale=1, VSync desligado e camera baixa com bastante grama. Aguarde uploads e compilacao de shaders. No Unity Profiler GPU, selecione eventos de compute Generate/Cull, draws do material ProceduralGrass, depth e sombras; some seus custos. Frame Debugger identifica draws/pass counts, mas nao comprova duracao GPU. RenderDoc com GPU duration pode separar dispatches e draws; compare com/sem grama na mesma camera e inclua as sombras.

`GrassBenchmark` so funciona quando a build recebe `-grass-benchmark -grass-output <pasta>`. Faz oito blocos alternados ligado/desligado, 120 frames de aquecimento e 300 samples por bloco, sem liberar buffers entre estados. Grava mediana/p95, hardware, API, modo de threads, resolucao, memoria, capturas e maior GC observado no update da grama. `-grass-baseline-smoke` faz um teste curto sem draws de grama. `GrassValidation.BuildBenchmarkBatch` muda PlayerSettings para preparar essa build: execute numa copia de validacao, nao no checkout de trabalho. A build de teste entregue em `Artifacts/Grass/Standalone/GrassBenchmark.exe` pode ser executada com esses argumentos; sem eles, inicia a demonstracao e desativa a coleta automatica.

Se exceder 2 ms: primeiro reduza shadowDistance ou desative castNearShadows; depois reduza farDistance para 60-70 m, retencao distante e densidade. Reduza generationsPerFrame para 1 se os picos ocorrerem ao mover a camera. Se o limite de patches ficar pequeno para o raio, reduza o raio antes de aumentar memoria. Evite muitos volumes de exclusao pequenos; consolide-os por regiao. Meça tambem movimento/teleporte, nao apenas camera parada.

## Limites e riscos

- Apenas URP Forward/Forward+ e PC com compute/indirect foram implementados. Nao e um shader HDRP/Built-in, nem uma promessa de compatibilidade mobile, XR ou multiple cameras simultaneas. Desenho e culling usam a camera selecionada.
- Apenas Play mostra a grama. A previsualizacao de terreno no Editor continua como antes.
- Terreno deve manter escala uniforme 1 e eixos locais XZ; alturas e limites atuais assumem chunks quadrados. O ajuste de tamanho de patch preserva distribuicao entre os chunks do mesmo controller.
- Normais seguem o campo de alta resolucao; alturas seguem a malha de LOD aplicada. O terreno original nao tem stitching/skirts entre LODs; rachaduras da propria malha nao sao corrigidas por esta melhoria visual.
- Exclusoes usam footprints AABB XZ conservadores, nao colisoes exatas com formas ou cavidades. Ha um limite de 512 footprints por tile. Props/volumes movidos depois da geracao precisam de Rebuild.
- Nao ha Hi-Z, motion vectors/TAA especificos, sombras de luzes adicionais ou fisica persistente das folhas. Dither pode mostrar pontilhado no fade.
- Pool e objetos de patch sao preparados ao ativar. Criacao/destruicao de tiles ainda prepara/libera texturas, arrays CPU e dados de exclusao; esses eventos nao sao livres de custo. Uploads e geracoes sao distribuidos por frames, sem GPU readback bloqueante durante Play normal.
- O update do terreno existente ainda aloca suas proprias colecoes e valida settings por frame. O contador de GC do manager nao significa zero GC no jogo inteiro. O custo de gerar/reaplicar meshes e world features CPU pertence ao terreno e nao foi reescrito.
- O vento e procedural, nao uma simulacao fisica nem uma copia do codigo proprietario da Sucker Punch. Inspiracao tecnica, implementacao propria.

## Referencias primarias

- [GDC: Procedural Grass in Ghost of Tsushima](https://www.gdcvault.com/play/1027033/Advanced-Graphics-Summit-Procedural-Grass)
- [SimonDev: Quick_Grass](https://github.com/simondevyoutube/Quick_Grass)
- [Unity 6.6: RenderMeshIndirect](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Graphics.RenderMeshIndirect.html)
- [Unity 6.6: GraphicsBuffer.CopyCount](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/GraphicsBuffer.CopyCount.html)
- [Unity: Frame Timing Manager](https://docs.unity.com/en-us/engine/6000.6/manual/analysis/graphics-performance-profiling/profile-rendering/frame-timing-manager)
