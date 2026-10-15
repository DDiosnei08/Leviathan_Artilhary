# Terreno procedural e biomas

O mapa usa ruído Perlin 2D determinístico implementado em código gerenciado e amostrado em coordenadas globais em metros. Essa implementação evita depender de `Mathf.PerlinNoise` nativo no worker de geração. A resolução da mesh altera o espaçamento dos vértices, mas não a escala física das características do ruído. O campo principal fornece a escala média; **Terrain Noise Layers** acrescenta variações de grande escala e detalhes com escala, oitavas e intensidade configuráveis. Cada camada usa a mesma posição mundial e uma seed derivada da seed principal, mantendo o resultado igual em qualquer chunk.

## Ajustes em LeviathanTerrain

- **Chunk Size** é o tamanho físico de cada chunk em metros. O asset atual usa 1200 m e **Mesh Resolution** usa 121 vértices por lado: 120 células de 10 m cobrem exatamente 1200 m.
- Chunk `(x,z)` começa em `(x * Chunk Size, z * Chunk Size)`, relativo à origem do `TerrainWorldController`. Os vértices locais vão de 0 a 1200 m; portanto `(0,0)` ocupa X/Z de 0 a 1200 m, `(1,0)` de 1200 a 2400 m em X, e coordenadas negativas terminam na origem.
- **Finite Map Chunks Per Side** define o lado da grade finita (sempre ímpar, máximo 17). O asset atual usa 17, ou seja, 20.400 x 20.400 m. Como a grade usa coordenadas `-8` a `+8` com a origem no canto do chunk `(0,0)`, seus limites são -9600 a 10.800 m nos eixos X e Z; o centro visual fica em (600,600) relativo ao controlador. Veja a seção "Mapa grande e zonas planas" abaixo.
- **Noise Scale** e **Biome Noise Scale** são escalas de características em metros, não o tamanho do chunk nem a resolução da mesh. Escalas maiores produzem características maiores.
- **Terrain Noise Layers** permite somar variações centradas em torno da altura base. `Scale Meters` é a frequência física, `Strength` controla a amplitude adicionada, e oitavas/persistência/lacunarity controlam o detalhe interno. Defina `Strength` como zero para desativar uma camada.
- As escalas foram migradas de 34 e 180 amostras (9,375 m por amostra) para aproximadamente 318,75 m e 1687,5 m, preservando a frequência espacial aproximada ao passar para amostragem em metros. Esses também são os padrões para novos assets: 281,25 m para o noise de terreno (equivalente aos antigos 30 samples) e 1687,5 m para biomas. **Height Multiplier** era 18 m nessa migração; o asset atual usa 1000 m (ver "Mapa grande e zonas planas").
- **Biome Noise Weight** mistura ruído branco na máscara de bioma; mantenha baixo para evitar manchas granuladas.
- **Biome Height Influence** mistura a altura do terreno com a máscara de bioma para escolher a faixa visual.
- **Biome Blend Width** suaviza as transições de cor entre os presets.
- Cada chunk também fornece mapas de umidade, temperatura, inclinação, água e navegação. Umidade e temperatura são ruídos determinísticos globais; a temperatura recebe uma redução configurável com altitude. A inclinação é calculada uma vez em graus a partir da superfície já suavizada.
- A água é uma máscara simples: inclui lagos abaixo de `Lake Threshold` e áreas úmidas rasas em baixadas com umidade alta e inclinação baixa. Não há simulação de fluxo. A navegação é um rating de 0 a 3 derivado da inclinação e da água; 0 significa impraticável, 3 representa terreno suave.
- Os presets visuais existentes continuam controlando as cores por classificação de terreno/bioma. Inclinações acentuadas recebem uma mistura de cor rochosa. O preview isolado sem dados de mundo mantém a antiga máscara procedural de estrada apenas para compatibilidade; o mapa finito renderiza a rede calculada entre POIs.
- **POI Definitions** configuram locais leves por id, nome, quantidade, faixa de altitude/umidade, inclinação máxima, bioma preferido, raio de área plana, suitability mínima e espaçamento. No mapa finito, candidatos válidos são pontuados de forma determinística e a seleção evita água e sobreposição entre locais.
- O mapa finito combina os mapas de seus chunks antes de gerar rotas A* de baixo custo entre POIs. Ratings de navegação baixos aumentam o custo e água/terreno rating 0 são bloqueados. A máscara resultante de estrada e os marcadores de POI são escritos nos mapas dos chunks e visualizados na textura; não é geometria de estrada nem NavMesh. `Connect POIs With Roads` e `Road Width Cells` controlam essa camada visual. Esta primeira versão de conexão cobre o mapa finito; o modo streaming mantém mapas locais e ainda não cria rede global de POIs/estradas.
- `WorldWeatherProvider` expõe `SampleAt(position)` para consultar direção/velocidade do vento, visibilidade e intensidade de chuva. É uma API de dados configurável, sem simulação temporal ou integração com gameplay.

## Dados para sistemas futuros

`TerrainChunk.GeneratedData` contém os mapas reutilizáveis em resolução de vértices; alturas normalizadas e em metros estão separadas (`Values` e `WorldHeights`). Inclinação está em graus; umidade, temperatura, água, suitability, POI e estrada ficam em 0–1; navegação fica em 0–3. Os mapas usam `[x, y]`, onde `y` cresce na direção de Z negativo dentro do chunk, como a malha atual.

No mapa finito, `TerrainWorldController.GeneratedPOIs` disponibiliza locais determinísticos. `TerrainPosition` é relativo ao transform do controller e pode ser convertido para coordenada Unity com `controller.transform.TransformPoint(poi.TerrainPosition)`. Para clima, adicione `WorldWeatherProvider` a um GameObject e consulte `SampleAt(posiçãoMundial)`; o retorno é somente uma amostra de dados e não altera o jogador ou o clima global automaticamente.

O Inspector **Validate Terrain Setup** executa os gates de mapas e features: finitude e determinismo dos valores, continuidade dos dados em +X/+Z, limites e espaçamento dos POIs, determinismo das rotas, escala e cobertura LOD. A validação gera o mapa finito de forma síncrona mesmo quando a opção de background está ligada, para que o resultado possa ser inspecionado imediatamente.
- **Height Multiplier** é a escala vertical em metros e não altera o tamanho horizontal do chunk.
- **Height Smoothing Iterations/Strength** controlam uma suavização ponderada da superfície. Amostras extras ao redor do chunk mantêm o resultado contínuo nas bordas.
- As normais da superfície usam uma amostra extra além da área suavizada. Assim, a iluminação nas bordas considera a altura do chunk vizinho e evita linhas causadas por normais calculadas separadamente.
- As texturas dos chunks usam clamp nas bordas para impedir que a filtragem misture a primeira e a última coluna de cores da mesma textura.
- **Generate Procedural Props** ativa bunkers, trincheiras, árvores mortas e rochas de protótipo.
- **Decorations/Structures Per Chunk** controla a quantidade máxima gerada. Os pontos são determinísticos por seed e chunk; água é evitada e a inclinação é consultada do slope map compartilhado, usando os limites em graus de estrutura/decoração.
- **Prop Minimum/Maximum Height** limita as decorações às faixas de elevação selecionadas.
- **Prop View Distance** oculta a raiz dos props além da distância configurada, sem destruir/recriar as decorações ao mover a câmera.
- Com **Use Background Generation** ativo, o pedido captura uma cópia dos valores de geração e amostra a curva de altura na main thread. O worker recebe somente esse snapshot e coordenadas/LOD; criação de `Mesh`, textura, props e atualização da cena acontecem na main thread. Resultados de LOD obsoletos são descartados e solicitados novamente. Isso evita ler `ScriptableObject` ou avaliar `AnimationCurve` no worker.

Os props atuais são formas primitivas de blockout com cores industriais, não assets finais. Cada chunk possui uma raiz de props própria, removida junto com a malha quando o terreno é regenerado. Bunkers e parapetos têm colliders; adereços visuais não.

Abra **Project Leviathan > Terrain Debug**, selecione `LeviathanTerrain` e clique em **Refresh** para inspecionar height, falloff, biome, cores, slope, moisture, temperature, water e navigation. A validação da cena também confere a colocação de POIs/rotas depois da geração finita. O botão **Validate Terrain Setup** confere escala 1200 m / 121 vértices / células de 10 m, origem e extensão das meshes, amostragem no mesmo ponto mundial com resoluções diferentes, bordas reais +X/+Z (altura, biome, clima, água, navegação, cor e normal), determinismo dos mapas e cobertura total dos LODs.

Use **Frame Terrain in Scene View** após gerar a grade para enquadrar o mapa finito. O enquadramento leva em conta que a grade de três chunks se estende de -1200 a 2400 m.

`Noise.normalize` agora normaliza por amplitude teórica em vez do mínimo/máximo observado em cada chunk. Isso mantém o relevo determinístico e evita degraus de altura causados pela normalização independente de cada chunk.


## Mapa grande e zonas planas (update/part1)

Status: **implementado, ainda não validado na Unity** (nada foi compilado nem executado fora do editor do desenvolvedor; os números abaixo vêm de uma simulação estatística em Python do pipeline de altura, não do jogo).

### Relevo montanhoso
- **Relief Contrast** (`reliefContrast`) expande a altura em torno de 0,5 logo após somar as camadas: `v = clamp01(0.5 + (v - 0.5) * contraste)`. O ruído Perlin normalizado fica concentrado perto de 0,5, então sem contraste o mapa parece achatado. 1 = sem alteração (comportamento antigo); o asset usa 2,1.
- Valores atuais do asset: Height Multiplier 1000 m, Noise Scale 4000 m, persistência 0,45, camadas 9000 m / 0,30 e 300 m / 0,04, Flatness Bias 0,35, Mountain Bias 0,24.

### Zonas planas
- **Flat Zones** (`TerrainFlatZoneSettings`): um ruído de baixa frequência (`scaleMeters`) define regiões em que a altura é puxada para um nível de planície (`minimumLevel`..`maximumLevel`, variando lentamente por `levelScaleMeters`).
- `threshold` decide quanta área é plana (menor = mais área); `blendWidth` controla a suavidade da transição entre montanha e planície (valores muito pequenos criam escarpas íngremes).
- A máscara usa coordenadas de mundo e é aplicada ponto a ponto antes da suavização, então é contínua entre chunks e determinística por seed.
- O asset usa escala 2500 m, threshold 0,50, blend 0,08 e níveis 0,24–0,30 (240–300 m acima do zero). Como `lakeThreshold` é 0,13, as planícies ficam acima da água.
- Estimativa da simulação (3 seeds, mapa de 20,4 km, células de 10 m): altitude mediana ~335–345 m, p95 ~610–630 m, máximo ~820–910 m (abaixo dos 1000 m do multiplicador); inclinação mediana ~10°, p99 ~46–50°; ~19–20% da terra com inclinação < 3° e ~27% < 5°; ~8–10% acima de 25°; água ~1%. Há locais planos de raio ≥ 100 m em todos os 16 quadrantes de ~5 km (43–56 locais por seed) e ≥ 98% deles conectados à malha caminhável principal. A simulação não modela o mapa real (seeds de oitava diferentes), então use o Debug Preview para conferir.

### Janela de chunks (mapas finitos grandes)
- Se `finiteMapChunksPerSide > finiteMapFullLoadMaxChunksPerSide` (padrão 5), o `TerrainWorldController` carrega só os chunks dentro de `finiteMapStreamingRadiusChunks` ao redor do viewer, limitados às bordas do mapa. Os demais são descartados.
- Nesse modo cada chunk gera seus próprios POIs e estradas (mesmo caminho do mundo infinito); **não há planejador global** de POIs/estradas conectando o mapa inteiro. Isso fica para uma etapa futura.
- Fora do Play (preview no editor) a janela usa `finiteMapEditorPreviewRadiusChunks` (padrão 1 = 3×3 chunks), porque o preview é gerado de forma síncrona. Nota: a trava do editor observada em 2026-10-10 ("Hold on… SceneView.Repaint", log com "Ran out of Graphics Ring Buffer space") foi resolvida removendo a opção de linha de comando `-gfx-ring-buffer-size 65536` da Unity, não pelo tamanho do preview; o limite 3×3 foi mantido por precaução e ainda não foi medido com a janela maior.
- `maxChunkGenerationsPerFrame` (padrão 2) limita quantos meshes são gerados por frame durante o Play para evitar travadas ao cruzar a borda da janela.
- Limitação conhecida: impactos de projétil fora da janela carregada não têm terreno para colidir; será necessário um amostrador de altura (da função de ruído) numa etapa seguinte.
- Cores: com 1000 m de relevo a cor precisa seguir a altitude. O asset usa `Biome Height Influence` 0,9 (antes 0,35, que deixava o ruído de bioma dominar e o terreno ficava uniformemente bege): planícies/baixadas verdes (0,24–0,5), campos marrons (0,5–0,68), crista rochosa (0,68–0,84) e picos claros (>0,84); inclinações > ~28° recebem cor rochosa.
- `TestCharacterController` agora sonda o solo a partir de 2000 m de altura (campos `groundProbeStartHeight`/`groundProbeDistance`).
