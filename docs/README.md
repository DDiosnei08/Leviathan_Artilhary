# Leviathan - documentacao do jogo

Esta pasta registra o conceito do jogo e as decisoes alinhadas com o autor durante a conversa de desenvolvimento. O projeto de trabalho atual e o `Leviathan2`.

Leviathan e um jogo de operacao individual de artilharia indireta. O jogador interpreta informacoes de observadores, trabalha sobre um mapa, consulta instrumentos e calculadoras, prepara o canhao e corrige os disparos a partir dos relatos recebidos.

## Como ler

| Documento | Conteudo |
| --- | --- |
| [01 - Conceito e escopo](01-conceito-e-escopo.md) | Identidade, papel do jogador, nivel de simulacao e limites da primeira etapa |
| [02 - Loop de operacao](02-loop-de-operacao.md) | Sequencia de uma operacao e exemplo de sessao |
| [03 - Mapa e inteligencia](03-mapa-e-inteligencia.md) | Quadriculas, medicoes, referencias, observadores e triangulacao |
| [04 - Artilharia e calculadoras](04-artilharia-e-calculadoras.md) | Cargas, alcance, calculo de elevacao, instrumentos e dois canhoes |
| [05 - Disparo, clima e impactos](05-disparo-clima-e-impactos.md) | Tempo de viagem, resolucao do tiro, vento, dispersao e dano |
| [06 - Reconhecimento e relatos](06-reconhecimento-e-relatos.md) | Radio, impressos, fotografias aereas e atualizacao do mapa |
| [07 - Mundo, base e maquina colossal](07-mundo-base-e-maquina-colossal.md) | Geracao procedural, escala do mundo e evolucao da base |
| [08 - Estado e implementacao](08-estado-e-implementacao.md) | O que existe, integracao futura, etapas e criterios de aceite |
| [09 - Decisoes e pendencias](09-decisoes-e-pendencias.md) | Registro das decisoes e detalhes ainda abertos |
| [10 - Shaders do terreno e agua](10-shaders-terreno-e-agua.md) | Materiais cartum, parametros, demonstracao e limites da melhoria visual |
| [11 - Estrutura do projeto](11-estrutura-do-projeto.md) | Pastas, cenas, convencoes e verificacao da reorganizacao |
| [12 - Instrumentos fisicos](12-interacao-instrumentos-fisicos.md) | Volantes, manivelas, botoes, captura e operacao sem HUD |
| [13 - Grama procedural GPU](13-grama-procedural-gpu.md) | Compute, indirect rendering, culling, LOD, vento, setup e profiling |
| [14 - Base giratoria](14-base-giratoria.md) | FBX, base unica por semente, fosso, dois canhoes, cena e validacao |
| [15 - Ambiente, vegetacao e erosao](15-ambiente-vegetacao-e-erosao.md) | Grama mais densa, arvores, sulcos e base sobre terreno natural |
| [16 - Assets e cenario procedural](16-assets-e-cenario-procedural.md) | Curadoria dos downloads, bosques, pedras, postes, barreiras e sincronizacao da base |
| [17 - Varredura e limpeza](17-varredura-e-limpeza.md) | Dependencias, arquivo recuperavel, menos GC no terreno e colisao de props proximos |
| [18 - Polimento dieselpunk](18-polimento-visual-dieselpunk.md) | Paleta mineral, pintura e metal, props, vegetacao, agua e atmosfera |
| [19 - Grama na borda da base](19-grama-na-borda-da-base.md) | Exclusao precisa, transicao irregular e seguranca do concreto |
| [20 - Distancia e contador FPS](20-distancia-e-contador-fps.md) | Alcances maiores, memoria da grama e diagnostico de FPS sem Canvas |
| [21 - Ceu em camadas](21-ceu-em-camadas.md) | Dia/noite, estrelas, auroras, vento e nuvens com paralaxe |
| [22 - Composicao do mapa](22-composicao-do-mapa-e-splines.md) | Etapas de solo, vegetacao, relevo, estradas, infraestrutura e atmosfera |
| [23 - Spline projetada](23-spline-projetada.md) | Faixas sobre terreno, esfera, cone e outras superficies estaticas |
| [Referencias visuais](referencias/README.md) | Imagens fornecidas pelo autor e como devem orientar o projeto |

## Status das definicoes

- **Acordado:** decisao expressa pelo autor ou confirmada por ele.
- **Existente:** comportamento encontrado nos scripts e na cena do prototipo.
- **Proposta:** sugestao de implementacao que ainda pode ser ajustada.
- **Pendente:** detalhe que ainda nao recebeu uma definicao suficiente.

Uma especificacao de comportamento futuro nao significa que ele ja esta implementado. Consulte o documento 08 antes de iniciar uma tarefa.

## Regras centrais acordadas

- O jogador e um operador individual.
- O trabalho envolve triangulacao, distancia, direcao e ajustes manuais.
- O mapa tem quadriculas de 1 km, subdivididas em 100 metros.
- Linhas desenhadas pelo jogador mostram distancia e angulo.
- Ha cinco cargas de propelente, chegando inicialmente a 25 km.
- A calculadora de elevacao recebe os dados informados pelo operador e fornece o angulo recomendado.
- O jogador transfere a recomendacao aos controles do canhao manualmente.
- O tiro e resolvido por local de queda, tempo de viagem e dano; um projetil viajando fisicamente nao e necessario.
- O operador recebe informacoes de vento e pode tentar compensar seus efeitos.
- Existe uma pequena dispersao adicional.
- O jogador conhece a queda por relatos; o mapa so recebe essa informacao quando o relato chega.
- Fotografias aereas em preto e branco podem ser adicionadas ao mapa.
- A maquina colossal futura tera dois canhoes com direcao compartilhada e elevacao independente.
- A maquina permanece parada durante a operacao de tiro.
- Movimento e reacao dos inimigos ficam para uma etapa posterior.

## Prototipo atual

A cena de referencia atual e [Leviathan_BaseGiratoria.unity](../Assets/Scenes/Leviathan_BaseGiratoria.unity).
Ela possui terreno procedural, agua e grama, o modelo BaseCompleta, dois canhoes e jogador.
As cenas antigas citadas nos documentos 08 a 13 foram substituidas; esses documentos registram etapas anteriores.
O teste de artilharia foi preservado. Consulte os documentos 14 e 15 para o estado atual.

O guia de teste e de referencias do Inspector ja existente esta em [BASE-PROVISORIA-INTEGRADA.md](../Assets/Documentation/BASE-PROVISORIA-INTEGRADA.md).

Os sistemas descritos como futuros nao devem ser considerados implementados apenas por aparecerem nesta pasta. Melhorias visuais implementadas sao descritas no documento 10; o estado dos sistemas de operacao continua registrado no documento 08.
