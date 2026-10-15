# Polimento visual dieselpunk

## Direcao visual

A cena Leviathan_BaseGiratoria recebe uma paleta de petroleo, aco, minerais frios,
vegetacao contida, terra seca, latao e vermelho-oxido. A proposta continua
estilizada, com iluminacao suave e detalhes discretos, sem texturas realistas
pesadas nem filtro sepia sobre o jogo inteiro.

O trabalho nao altera semente, relevo, erosao, distribuicao ou densidade de props,
arvores e grama, posicionamento da base, colisoes ou regras dos canhoes.
A cena isolada de artilharia e seu hash permanecem preservados.

## Implementacao

- CartoonTerrain preserva a textura procedural de biomas em _BaseMap e as UVs.
  Ruido suave em duas escalas e tinta mineral usam coordenadas mundiais para
  continuidade entre chunks. A inclinacao mistura uma cor de rocha exposta.
- DieselpunkSurface e um shader URP de superficie opaca para pintura, aco, latao,
  concreto e props. Usa iluminacao PBR da URP, sombras, SSAO, fog, instancing,
  GPU Resident Drawer/DOTS, DepthOnly, DepthNormals e ShadowCaster.
- Desgaste e granulado usam coordenadas locais escaladas em metros. Assim a
  pintura acompanha as pecas que giram ou recuam, em vez de deslizar sobre elas.
  E desgaste procedural, nao uma deteccao fisica de bordas ou ferrugem simulada.
- SceneryPrototype permite um material opcional por arquetipo. O manager
  compartilha as copias dos materiais entre prototipos iguais e libera todas
  ao desabilitar. O material geral continua como fallback para presets antigos.
- Pedras, postes, barreiras de concreto, sacos e caixas recebem acabamentos
  diferentes. As cores de vertices dos meshes importados continuam presentes.
  No poste, a saturacao das cores de vertices aproxima uma mascara metal/madeira.
- CartoonTrees tem saturacao, tinta de folhagem, variacao por arvore, sombras
  suaves e DepthNormals para o renderer Deferred e SSAO.
- A grama recebe cores menos vivas e reflexo/translucencia menores; nao mudou
  de densidade nem de algoritmo de geracao. Sua iluminacao ambiente minima foi reduzida.
- A agua recebe tons de petroleo, espuma mais contida, Fresnel e brilho solar.
  O movimento e as mascaras de margem existentes continuam sendo utilizados.
- Sol, ceu, ambiente trilight e fog foram ajustados na cena integrada. Um Volume
  global usa tonemapping Neutral, contraste leve e pouca dessaturacao. Nao usa
  bloom, blur, vignette ou novas imagens sobre a visao do operador.
- Papel, mapa, tinta, agulhas e cores semanticas dos botoes ficam fora do desgaste.

## Inspector

| Asset ou componente | Principais ajustes |
| --- | --- |
| Materials/Terrain/TerrainCartoon | Mineral Patch Scale/Strength, Mineral Tint, Exposed Rock Tint/Strength, Detail Strength, Shadow Contrast |
| Settings/Terrain/BaseGiratoriaTerrain | Cores dos biomas e Surface Palette; mudancas aqui tambem afetam a textura procedural |
| Shader Dieselpunk Surface | Paint Color, Metallic, Smoothness, Paint Chipping, Grime Strength, Weathering Scale e Surface Grain |
| Materials/Vegetation/TreeCartoon | Color Saturation, Foliage Tint/Strength, Per Tree Variation e Shadow Tint |
| Settings/Grass/BaseGiratoriaGrass | Root Color, Tip Color, Translucency e Specular |
| Materials/Water/WaterCartoon | Shallow/Deep Color, Foam, Pattern Strength, Sky Reflection Strength e Sun Glint Strength |
| Settings/Vegetation/WorldScenery | Material por prototipo; vazio usa o material geral |
| Settings/Volumes/DieselpunkGrade | Tonemapping e Color Adjustments |
| Sol e RenderSettings da cena | Direcao e intensidade da luz, ambiente, ceu e distancia de fog |

Novos materiais ficam em Assets/Materials/Environment. Materiais da base foram
atualizados no lugar, preservando GUIDs e referencias do prefab.
Mudancas em materiais fonte de arvores/props requerem desativar/reativar o
manager para atualizar suas copias de runtime. Ajustes na configuracao de grama
seguem o fluxo do manager existente; regenerar o terreno reconstrui as cores.

## Validacao e registros

O teste visual usa uma copia isolada em Unity 6000.6.3f1, URP 17.6.0 e DX11.
Confere shaders, textura de biomas por chunk, materiais instanciados por
arquetipo, controles fisicos, capturas sem magenta, contraste, regeneracao e
liberacao dos materiais. A altura natural amostrada antes/depois, a semente e
o centro do preview sao comparados ao aplicar o setup.

Resultado: **173 verificacoes aprovadas**: uma de preservacao do setup, 42
visuais, 49 de base/artilharia, 61 de curadoria/geracao e 20 de runtime/GC/colliders.
As verificacoes incluem dez sementes, dois ciclos de Play, shaders sem erros,
materiais por arquetipo e liberacao das copias. A checagem final de GUIDs nao
encontrou referencias ausentes. Nas rotinas de terreno e refresh de colisao
medidas apos aquecimento, as alocacoes gerenciadas continuaram em zero.

A animacao da agua e medida por diferenca de imagens num mesh isolado renderizado
na GPU, sem grama ou arvores confundindo o resultado. A janela inicial da base
pode nao conter agua; nao foi criado um lago artificial em area seca para a foto.

Artefatos ficam em Artifacts/DieselpunkPolish: Captures, Validation.txt,
SetupValidation.txt, logs, Changes.diff e Before. O backup permite recuperar
os shaders, materiais, configuracoes e cena anteriores sem reverter outras tarefas.

O menu Leviathan > Visual > Aplicar acabamento dieselpunk reaplica esta direcao
visual. Ele redefine os valores estilisticos; nao deve ser usado para preservar
ajustes artistico-manuais posteriores. O metodo DieselpunkVisualValidation.ApplyAndValidateBatch
e exclusivo de uma sessao dedicada: entra em Play e encerra o Editor ao terminar.

## Limites

Fresnel da agua e uma reflexao de cor estilizada, nao reflexao planar da cena.
O shader de superficie nao tem normal map, parallax, transparencia, Meta para
lightmapping ou motion vectors personalizados. A cena atual usa luz dinamica e
nao depende desses recursos. Nao foram adicionados novos modelos, terrenos,
inimigos, destruicao ou gameplay. O custo de GPU total nao foi medido; nao e uma
garantia de manter a grama abaixo de 2 ms no ambiente completo.
