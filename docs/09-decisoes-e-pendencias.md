# 09 - Registro de decisoes e pendencias

## Decisoes confirmadas

| ID | Decisao |
| --- | --- |
| D01 | Leviathan e o nome atual do jogo |
| D02 | A maquina colossal sera criada futuramente, com dois canhoes de grande capacidade |
| D03 | O jogador e um operador individual |
| D04 | Localizacao e preparacao exigem trabalho manual, incluindo triangulacao, distancia e angulos |
| D05 | Informacoes chegam por radio ou instrumentos como maquinas de escrever |
| D06 | Dois observadores podem informar posicoes e direcoes para triangulacao |
| D07 | Pontos de referencia precisam poder ser marcados |
| D08 | A grade principal mede 1 km por lado e a subdivisao mede 100 m por lado |
| D09 | O mapa usa identificacao como A1 e C4; linhas arrastadas mostram angulo e distancia |
| D10 | Ha transferidor, bussola fixa e tabelas de propelente entre as ferramentas previstas |
| D11 | Sao cinco cargas: 2-5, 5-10, 10-15, 15-20 e 20-25 km |
| D12 | Alcance maximo inicial de 25 km confirmado; alcances maiores podem ser considerados depois |
| D13 | Tipos de projetil inicialmente nao mudam o alcance para configuracoes equivalentes |
| D14 | Uma maquina permite consultar distancia a partir da configuracao informada, com leitura em passos de 100 m conforme interpretacao do alinhamento |
| D15 | Haver uma calculadora de elevacao com dados inseridos pelo operador |
| D16 | A proposta de ler automaticamente a elevacao atual da peca foi corrigida; a recomendacao da calculadora e transferida manualmente aos controles |
| D17 | Resolver o tiro por ponto de queda, tempo de viagem e dano; um projetil fisico viajando nao e necessario |
| D18 | O operador nao observa diretamente a explosao distante |
| D19 | O mapa recebe a queda somente quando chega o relato |
| D20 | Fotografias aereas de impacto em preto e branco podem ser adicionadas ao mapa |
| D21 | Vento informado ao operador, com influencia calculavel e pequena dispersao |
| D22 | Chuva, tempo e vento podem participar da variacao do acerto; coeficientes ainda abertos |
| D23 | Inimigos em movimento ou reagindo ficam para depois |
| D24 | As duas pecas compartilham direcao e possuem elevacoes independentes |
| D25 | O operador alterna entre estacoes no mesmo local; municoes e preparacoes podem ser individuais |
| D26 | Disparos simultaneos sao possiveis |
| D27 | A maquina permanece parada durante a operacao de tiro |

## Correcao importante do alinhamento

A maquina de angulo recebe a distancia e a configuracao informadas pelo operador e fornece uma elevacao recomendada. Ela nao precisa consultar a elevacao atual do canhao para produzir essa recomendacao.

A referencia visual contem seis cargas e outros valores ilustrativos. A regra aprovada para o jogo permanece em cinco cargas ate 25 km.

## Pendencias que afetam a primeira implementacao

| Tema | O que falta definir | Implicacao |
| --- | --- | --- |
| Calculadora de distancia | Faixa ou valor de referencia; dados manuais necessarios | Evita presumir um alcance unico a partir de uma carga que possui varios alcances |
| Modelo de alcance | Tabela ou curva do jogo e relacao com elevacao | Base comum para calculadora e resultado do tiro |
| Elevacao | Limites, precisao e eventual escolha entre arcos | Operacao e validade das recomendacoes |
| Altitude | Se a diferenca entre peca e alvo participa do calculo | Referencia da imagem inclui correcao de altitude, ainda nao confirmada |
| Grade | Origem, sentido da numeracao e notacao da subquadricula | Consistencia de mapa e relatos |
| Leituras | Casas decimais de angulo e arredondamento da distancia | Legibilidade e dificuldade |
| Relatos | Precisao, formato e referencial de curto/longo/esquerda/direita | Permite corrigir sem ambiguidade |
| Disparo | Comando, requisitos de preparo, recarga e intervalos | Fecha a operacao da peca |
| Tempo | Viagem, comunicacao e entrega de fotografia | Ritmo da sessao |
| Clima | Unidades, direcao do vento, coeficientes e variacao temporal | Correcao calculavel |
| Dispersao | Amplitude e dependencia de distancia/carga/clima | Incerteza sem apagar o efeito das correcoes |
| Dano | Tipos, intensidade, area e estado dos alvos | Resultado dos impactos |
| Reconhecimento | Foto automatica ou solicitada, horario e disponibilidade | Acesso do jogador a informacoes |
| Missao | Objetivo, condicao de sucesso e falha | Forma de uma sessao completa |

## Propostas registradas, ainda ajustaveis

- Fechar o primeiro ciclo com um canhao na base provisoria.
- Usar 1 unidade de mundo como 1 metro nos sistemas de operacao.
- Adotar norte para cima e azimute horario a partir do norte.
- Permitir zoom, deslocamento, remocao de linhas e anotacoes.
- Preservar historico de tiros, relatos e fotografias.
- Gerar fotografias a partir do estado real do mundo procedural.
- Separar o instante do impacto do instante de recebimento da informacao.
- Usar um modelo comum de alcance para tabela, calculadora e disparo.
- Dimensionar o mundo para o alcance pretendido e carregar apenas a janela necessaria.

## Como atualizar esta pasta

Quando uma pendencia for resolvida, atualizar o documento do sistema correspondente e este registro. Quando uma etapa for implementada, atualizar o estado no documento 08.

Preservar a diferenca entre decisoes do autor, propostas de implementacao e comportamento verificado no projeto.
