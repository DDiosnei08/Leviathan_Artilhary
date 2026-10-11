# Fase 0 e Fase 1: altura base e pré-visualização

**Estado: Implementado; validação em andamento.** Compilou na Unity, a pré-visualização funciona e 30/31 testes passaram. O teste que falhou revelou uma simetria no hash, corrigida (GeneratorVersion continua 1, pois nenhum terreno foi exportado ainda). Falta rodar os 31 testes de novo.

## Objetivo

1. Projeto limpo que abre na Unity 6000.6.3f1 sem erros (Fase 0).
2. Uma função `TerrainHeightSampler.Sample(x, z)` que devolve a altura e o gradiente de qualquer ponto do mapa de 20.400 m, de forma determinística (Fase 1).
3. Uma janela de pré-visualização para ver o relevo e medir a área plana, antes de qualquer malha ou chunk.

## Arquivos

| Arquivo | Função |
| --- | --- |
| `Terrain/Core/TerrainConstants.cs` | Escala fixa: chunk de 1200 m, 17 x 17 chunks, domínio de 20.400 m |
| `Terrain/Core/SlopeMath.cs` | Única conversão de gradiente para inclinação em graus |
| `Terrain/Core/GradientNoise.cs` | Ruído de gradiente 2D com derivadas analíticas, e fBm |
| `Terrain/Core/HeightParams.cs` | Parâmetros imutáveis, validados uma vez |
| `Terrain/Core/TerrainHeightSampler.cs` | A função de altura (única fonte da verdade) |
| `Terrain/Core/HeightGrid.cs` | Grade de amostras e estatísticas (altura, % plana, % íngreme) |
| `Terrain/Runtime/TerrainSettings.cs` | ScriptableObject com os parâmetros editáveis |
| `Editor/Terrain/TerrainPreviewWindow.cs` | Janela `Tools > Leviathan > Terrain Preview` |
| `Tests/EditMode/Terrain/*.cs` | 31 testes de edição |

## Decisões desta fase

- **Domínio:** 17 x 1200 m = 20.400 m. A área de missão (20.000 m) é um limite operacional separado.
- **Gradiente:** adimensional (metros por metro). Inclinação = `atan(|gradiente|)` em graus, sempre por `SlopeMath`.
- **Contraste do relevo:** função racional suave `y = x (1 + k) / (1 + k |x|)`, sem corte duro (um corte duro deixaria o gradiente com saltos e criaria topos achatados).
- **Zonas planas:** onde a máscara vale 1, o relevo de detalhe some e a altura segue só o relevo de grande escala (a primeira camada), num nível entre 25% e 55% da altura máxima. Assim as planícies acompanham o relevo geral. Uma primeira versão usava um nível independente e criava paredões de até 76° nas bordas das planícies; foi descartada.
- **Posições de amostra:** `origem + índice * espaçamento` (uma multiplicação), para grades vizinhas obterem exatamente as mesmas coordenadas.
- **Versão do gerador:** `HeightParams.GeneratorVersion = 1`. Qualquer mudança no ruído ou no filtro deve aumentar esse número.

## Como validar (passo a passo)

1. **Abrir o projeto.** Unity Hub > Add > escolha a pasta do projeto > abra com 6000.6.3f1. Espere terminar de compilar.
   *Esperado:* a aba **Console** sem mensagens vermelhas. Se houver erro, copie o texto completo e me envie.
2. **Rodar os testes.** Menu `Window > General > Test Runner` > aba **EditMode** > **Run All**.
   *Esperado:* **31 testes passando**. Se algum falhar, clique nele e copie a mensagem; ela diz o valor esperado e o obtido.
3. **Criar a configuração.** Na janela Project, clique com o botão direito na pasta `Assets/Settings` > `Create > Leviathan > Terrain Settings`. Mantenha o nome `TerrainSettings`.
4. **Abrir a pré-visualização.** Menu `Tools > Leviathan > Terrain Preview`. Arraste o asset `TerrainSettings` para o campo **Settings**, deixe 512 amostras por lado e clique em **Gerar pré-visualização**.
   *Esperado:* uma imagem do mapa e um texto como *"Domínio 20400 m, 512 x 512 amostras (39,9 m cada) em ... ms. Altura: mínima ~230 m, máxima ~800 m ... Área plana (< 3°): ~20 a 30%. Área íngreme (> 30°): ~1 a 3%."* Os números exatos dependem da semente.
5. **Trocar a visualização.** Em **Visualização**: `Relevo` (sombreado), `Altura`, `Inclinacao` (verde = plano, vermelho = íngreme) e `ZonasPlanas` (branco = zona plana).
6. **Ajustar.** No asset, mude `Flat Zones > Threshold` (menor = mais área plana) e `Blend Width` (maior = encostas mais suaves), gere de novo e veja a porcentagem plana mudar.

## Valores de referência (para comparar)

Calculados por uma implementação independente em Python, com a **semente 12345** e os parâmetros padrão, grade de 256 x 256 amostras:

| Medida | Valor |
| --- | --- |
| Altura mínima / máxima / média | 188,7 m / 856,6 m / 476,2 m |
| Área plana (< 3°) | 21,2% |
| Área íngreme (> 30°) | 1,1% |
| Inclinação máxima dentro das zonas planas | 2,5° |

Com a semente padrão do asset (703110054) os números mudam um pouco, mas ficam na mesma faixa.
Os testes `TerrainHeightSamplerTests` conferem esses valores com tolerância.

## O que esta fase NÃO faz

Não há malha 3D, chunks, LOD, colisão, filtro de erosão nem exportação de arquivo. Isso vem nas próximas fases, uma por vez.
A aparência ainda é a de um relevo de ruído; o detalhe de ravinas virá com o filtro da Fase 2, depois que você aprovar a base.

## Limites conhecidos

- A geração da pré-visualização roda na thread principal; 1024 x 1024 pode demorar alguns segundos. O Terrain Baker completo (Fase 3) fará isso em segundo plano.
- Se os testes do Test Runner não aparecerem, confira `Window > General > Test Runner` > **EditMode** e se o pacote Test Framework 1.8.0 está instalado.
- O código foi revisado, mas **nunca compilado** pelo autor. Erros de compilação são possíveis e a correção é a primeira tarefa se aparecerem.
