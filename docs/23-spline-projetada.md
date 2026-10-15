# Spline projetada sobre superficies

Unity 6000.6.3f1, URP 17.6.0. Implementacao em C# e ShaderLab/HLSL,
sem dependencias adicionais. A curva usa interpolacao cubica Catmull-Rom
em forma Hermite. O projeto nao possuia o pacote Unity Splines instalado.

## Uso

1. Abra `Assets/Scenes/Tests/SurfaceSplineDemo.unity` para a demonstracao em
   paisagem, esfera e cone. Ela tambem funciona em Play, sem iniciar o terreno do jogo.
2. Para criar outra faixa: `GameObject > Leviathan > Spline projetada`.
3. Arraste os colliders das superficies para **Surfaces**. Use MeshCollider
   nao convexo para representar fielmente uma malha estatica irregular.
4. Edite os pontos em **Knots** ou pelas alcas na Scene View. Shift + clique
   acrescenta um ponto sobre uma superficie. Undo funciona na edicao dos pontos.
5. Escolha o modo de projecao e ajuste largura e resolucao.
6. **Reconstruir malha** atualiza explicitamente a geometria. A exportacao `.asset`
   cria uma copia independente; nao substitui a spline original.

## Modos

| Modo | Uso | Referencia |
| --- | --- | --- |
| Directional | Paisagens, pisos, paredes, varias superficies | Direction em coordenadas de mundo; para terreno: (0,-1,0) |
| Radial | Esferas e objetos ao redor de um centro | Radial Center; se vazio, a origem da spline |
| KnotNormals | Cone, caminho envolvendo um objeto, direcoes variaveis | Normal local de cada ponto, interpolada ao longo da curva |

Todas as colunas da faixa sao projetadas, incluindo as bordas. A normal do
colisor orienta a largura e o afastamento. UV.x vai de 0 a 1 na largura;
UV.y acompanha o comprimento projetado dividido por **Texture Metres**.

## Parametros

- **Width:** largura em metros de mundo. **Knots/Width:** multiplicador por ponto.
- **Sample Spacing:** distancia aproximada entre amostras longitudinais. Menor
  acompanha melhor curvas e relevo, com mais vertices/raycasts na reconstrucao.
- **Width Segments:** subdivisoes transversais. Importante em esferas e faixas largas.
- **Smoothing:** intensidade das tangentes. 0 conecta pontos por segmentos retos;
  1 usa a curva interpolada completa. Curvas muito fechadas exigem menor largura.
- **Surface Offset:** afastamento em metros, aplicado ao longo da normal para
  reduzir z-fighting. Comece em 0,02 a 0,04 m.
- **Maximum Surface Error:** tolerancia entre o centro do triangulo e a superficie.
  Triangulos sem apoio ou com desvio excessivo sao descartados.
- **Probe Above/Below:** alcance antes/depois da amostra ao longo da projecao.
  A origem do raio precisa ficar fora do colisor; aumente Above para objetos grandes.
- **Surface Layers / Surfaces:** filtro por layer e lista opcional de colliders.
  Triggers, colliders desativados e o colisor gerado pela propria faixa sao ignorados.
- **Generate Collider:** colisor opcional da malha gerada. Desnecessario para uma
  faixa decorativa sobre um chao que ja tem colisao.
- **Auto Rebuild:** atualiza alteracoes do Inspector, transform da spline e bounds
  dos alvos explicitos. Mudancas via script nos pontos devem chamar `RequestRebuild()`.

O shader `Leviathan/Spline Track` oferece cascalho, marcas de rodas e borda
irregular. Funciona com luz principal, sombras recebidas, fog, DepthOnly,
DepthNormals e instanciamento DOTS da URP. Pode ser substituido por outro material.

## Precisao e limites

- O resultado acompanha **a superficie dos colliders**. Um BoxCollider sobre um
  modelo complexo nao representa os detalhes desse modelo. Um SphereCollider e
  uma esfera analitica; o MeshCollider acompanha as faces da malha visivel.
- Uma malha finita aproxima superficies curvas. Nao existe garantia de precisao
  matematica em qualquer resolucao; reduza o espacamento e aumente as subdivisoes.
- O raio escolhe a primeira superficie atingida. Para cavernas, sobreposicoes e
  dobras, use alvos explicitos e normais por ponto, ou divida a faixa em partes.
- Sem apoio, os triangulos sao omitidos. Uma descontinuidade pode deixar uma
  abertura intencional; o sistema nao inventa uma ponte sobre o vazio.
- Amostras finitas podem nao detectar buracos menores que o espacamento.
- Curvas com raio menor que metade da largura podem se sobrepor. A edicao da
  curva continua sendo responsabilidade do autor; nao ha corte booleano de cruzamentos.
- Limite de 250.000 vertices por reconstrucao. A geracao aloca listas e realiza
  consultas fisicas somente ao reconstruir; nao foi projetada para recalcular
  uma estrada inteira a cada frame.
- Mover alvos encontrados apenas por layer nao dispara reconstrucao automatica.
  Malhas deformadas com bounds inalterados tambem exigem `RequestRebuild()`.
- A precisao de raycast depende do colisor: [Collider.Raycast](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Collider.Raycast.html)
  e [RaycastHit.normal](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/raycasthit/normal).

## Integracao procedural

`TerrainRoadMeshGenerator` extrai caminhos do grafo da mascara de estradas e
reutiliza `SurfaceSplineBuilder`. A projecao consulta diretamente os triangulos
do heightmap no LOD aplicado, sem raycasts e sem esperar pelo collider de um
vizinho. O caminho continua determinado pela navegabilidade e pela semente.
As faixas usam largura limitada a 85% da celula para manter uma margem dentro
da exclusao de vegetacao. Agua e abertura da base nao recebem estrada.

## Validacao

`SurfaceSplineValidation.ValidateBatch` e um teste para uma instancia **isolada**
do Editor: ele cria/salva a demonstracao e encerra o Unity ao concluir.
Confere contato, fechamento do anel, finitude, falta de superficie, ciclo de vida,
colisor proprio, direcao invalida e pixels do material na GPU. Relatorio e imagem
ficam em `Artifacts/MapComposition`.
