# 04 - Artilharia e calculadoras

## Cargas e alcance

**Acordado:** o jogador escolhe uma quantidade discreta de propelente. A progressao inicial possui cinco cargas:

| Quantidade | Faixa de alcance |
| --- | --- |
| 1 carga | 2 a 5 km |
| 2 cargas | 5 a 10 km |
| 3 cargas | 10 a 15 km |
| 4 cargas | 15 a 20 km |
| 5 cargas | 20 a 25 km |

O autor confirmou 25 km como alcance maximo inicial e demonstrou interesse em alcances maiores futuramente. Ampliacoes ainda nao possuem novas cargas ou faixas definidas.

Os limites compartilhados, como 5 km entre as cargas 1 e 2, precisam receber uma regra explicita. **Proposta:** ambas podem atender ao limite, com a recomendacao de elevacao correspondente a cada configuracao.

## Municao

**Acordado:** tipos de projetil podem ser diferentes, mas inicialmente nao alteram o alcance para a mesma configuracao de carga e elevacao. Seus efeitos no impacto podem variar.

O catalogo e os efeitos definitivos ainda nao estao definidos. O script de alavanca existente possui nomes de teste `HE`, `AP` e `SMOKE`; isso nao estabelece automaticamente o catalogo final nem significa que seus efeitos ja funcionam.

## Funcao de consulta de distancia

O autor descreveu uma maquina em que o jogador informa tipo de projetil e quantidade de propelente, obtendo uma estimativa de distancia com leitura em passos de 100 metros.

Essa funcao ajuda a consultar a capacidade de alcance da configuracao escolhida.

**Pendente:** como representar um valor unico quando a carga possui uma faixa e a elevacao muda o alcance. E necessario escolher entre mostrar a faixa, usar uma condicao de referencia definida ou receber mais um dado manual. Nao assumir leitura automatica da elevacao atual do canhao: essa proposta foi corrigida pelo autor.

A resolucao de 100 metros foi interpretada durante o alinhamento como passos no mostrador, por exemplo 12.300, 12.400 e 12.500 m. Ela nao estabelece uma garantia de erro maximo de 100 metros para a queda real.

## Calculadora de elevacao

**Acordado:** haver uma maquina que calcula o angulo necessario a partir dos dados fornecidos pelo operador, seguindo a referencia visual da calculadora balistica.

### Entradas e saida

| Dado | Origem |
| --- | --- |
| Distancia do alvo | Medida e informada pelo jogador |
| Quantidade de cargas | Escolhida e informada pelo jogador |
| Tipo de projetil | Selecionado pelo operador; alcance inicialmente equivalente entre tipos |
| Elevacao recomendada | Resultado apresentado pela maquina |

O jogador informa a distancia, seleciona a configuracao e aciona o mecanismo. O mostrador apresenta a recomendacao e o operador regula a peca manualmente.

### Apresentacao proposta

- Roda ou seletor para definir a distancia.
- Seletor de cargas.
- Selecao do tipo de projetil.
- Alavanca ou comando de processamento.
- Mostrador mecanico do angulo calculado.

Esses elementos seguem a linguagem da referencia. Quantidade de dispositivos, dimensoes e posicoes na base ainda nao foram definidos.

### Consistencia do calculo

**Diretriz proposta:** tabela, calculadora e resolucao do tiro devem consultar o mesmo modelo de alcance. Uma distancia calculada para determinada carga e elevacao deve corresponder ao alcance ideal usado pelo disparo, antes das influencias ambientais.

O modelo pode ser uma tabela ajustavel para o jogo. A formula, interpolacao, arcos de tiro, limites de elevacao e correcao de altitude ainda precisam ser definidos. Nao tratar os valores mostrados nas imagens como regras numericas aprovadas.

Uma entrada fora da faixa deve produzir um estado claro de configuracao incompativel. Nao apresentar um angulo aparentemente valido por arredondamento ou limitacao silenciosa.

## Operacao manual

A calculadora nao regula automaticamente o canhao. Seus dados sao uma recomendacao consultada pelo jogador.

O disparo utiliza a configuracao que efetivamente esta na artilharia. O operador precisa conferir os instrumentos e transferir os valores aos controles.

## Dois canhoes na maquina colossal

| Parametro | Comportamento acordado |
| --- | --- |
| Direcao / azimute | Compartilhado entre os dois canhoes |
| Elevacao | Independente por canhao |
| Quantidade de propelente | Configuracao individual de cada peca |
| Municao | Pode ser diferente entre as pecas |
| Preparacao | Operador alterna entre estacoes no mesmo local |
| Disparo | Possibilidade de disparos simultaneos |
| Movimento da maquina | Parada durante a operacao de tiro |

O objetivo e atingir dois alvos na mesma direcao, em distancias diferentes. As previsoes e os relatos precisam identificar qual peca produziu cada tiro.

**Existente:** o prototipo atual possui um canhao controlavel. Os dois canhoes e o mecanismo compartilhado ainda nao foram implementados.
