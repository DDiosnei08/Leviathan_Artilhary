# 05 - Disparo, clima e impactos

## Resolucao do disparo

**Acordado:** precisamos do local de queda, do tempo de viagem e do dano na regiao atingida. O projetil nao precisa percorrer fisicamente toda a distancia.

A apresentacao pode usar a animacao de tiro no canhao. Efeitos locais como clarao, fumaca, recuo e som sao propostas de apresentacao; detalhes e assets definitivos ainda estao abertos.

## Fluxo proposto para a simulacao

1. Registrar a configuracao da peca no instante do disparo.
2. Determinar o alcance ideal dessa configuracao.
3. Aplicar as influencias ambientais e a dispersao previstas pelo modelo.
4. Determinar o ponto de queda e o tempo de viagem.
5. Reproduzir o efeito de disparo na peca.
6. Aguardar o tempo de viagem.
7. Resolver o impacto e aplicar dano no mundo.
8. Produzir a informacao que os observadores poderao comunicar.
9. Entregar o relato segundo as regras de comunicacao.
10. Atualizar o mapa do jogador quando esse relato chegar.

O ponto de queda pode ser conhecido internamente desde a preparacao do evento. Isso nao autoriza sua revelacao antecipada ao operador.

## Dados propostos de um tiro

| Grupo | Dados |
| --- | --- |
| Identificacao | ID do tiro, ID do canhao e horario do disparo |
| Origem | Posicao da peca |
| Configuracao | Azimute, elevacao, municao e quantidade de cargas |
| Ambiente | Condicoes consideradas na resolucao |
| Resolucao | Ponto ideal, ponto final de queda e tempo de viagem |
| Consequencias | Regiao afetada e resultado do dano |
| Comunicacao | Relato produzido, horario de entrega e fotografia associada quando houver |

**Proposta:** manter esses dados registrados, para que ajustes posteriores da peca nao alterem retroativamente um tiro em andamento.

## Vento e clima

**Acordado:** o operador recebe direcao e intensidade do vento para tentar compensar. O resultado combina uma influencia calculavel com uma pequena dispersao.

Chuva, tempo e vento foram citados como fatores que podem variar o local de acerto. A influencia numerica de cada um ainda nao foi definida.

**Propostas para fechar o modelo:**

- Identificar claramente se a direcao informada e a origem ou o destino do vento.
- Escolher a unidade da intensidade exibida.
- Definir como a influencia varia com distancia, carga e tempo de viagem.
- Definir se o clima e considerado apenas no disparo ou se pode mudar durante a viagem.
- Separar o desvio ambiental da dispersao aleatoria.
- Permitir repetir um teste com os mesmos parametros e a mesma semente aleatoria durante o desenvolvimento.

O jogador deve conseguir melhorar suas correcoes a partir do que recebeu de informacao e observou nos retornos.

## Tempo de viagem

O tempo de viagem e necessario mesmo com a resolucao sem projetil fisico. Sua relacao com distancia, elevacao e carga esta pendente.

O atraso de comunicacao e um tempo separado do voo. Nao misturar dano no instante do impacto com a revelacao posterior desse dano ao jogador.

## Dano

**Acordado:** aplicar dano no local de queda, com possibilidade de diferenca entre tipos de municao.

**Pendentes:** raio de efeito, intensidade, queda do dano com a distancia ao centro, estruturas destrutiveis, efeitos de cada tipo e persistencia de crateras.

**Proposta inicial:** um alvo fixo com estado de dano e um efeito de impacto suficiente para comparar os resultados no reconhecimento. O catalogo completo pode ser desenvolvido depois de validar o ciclo.

## Limites de informacao

O HUD e o mapa nao devem revelar ponto de queda, dano ou alvo real antes de existir a comunicacao correspondente. Eventuais visualizacoes de depuracao precisam ficar fora da apresentacao normal ao jogador.
