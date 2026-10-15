# 08 - Estado e plano de implementacao

## Estado consultado no projeto

| Sistema | Estado atual |
| --- | --- |
| Terreno procedural | Integrado ao Leviathan2 |
| Materiais cartum e agua visual | Implementados; detalhes, demonstracao e limites no documento 10 |
| Cena da base provisoria | Criada, com canhao e sala de comando |
| Jogador em primeira pessoa | Existente |
| Troca entre vista geral, jogador e operador | Existente |
| Azimute e elevacao do canhao | Controlaveis |
| Volantes e instrumentos | Ligados ao canhao |
| Alavancas da sala | Ligadas ao mesmo controlador de artilharia |
| Provedor de clima | Existente para consultas de condicoes e aparencia do terreno |
| Mesa de mapa | Visual; sem o sistema operacional definido nesta pasta |
| Selecao de municao | Existe um comportamento de teste em uma classe de alavanca; efeitos nao implementados |
| Cargas e modelo de alcance | Nao implementados |
| Calculadoras de distancia e elevacao | Nao implementadas |
| Disparo, tempo de viagem e dano | Nao implementados |
| Observadores e triangulacao no mapa | Nao implementados |
| Relatos de queda e atualizacao atrasada | Nao implementados |
| Fotografias de reconhecimento | Nao implementadas |
| Maquina colossal e segundo canhao | Nao implementados |

O status descreve o codigo consultado. Nao substitui uma nova verificacao depois de futuras alteracoes.

## Referencias existentes

| Arquivo | Papel |
| --- | --- |
| [Base_Provisoria_Integrada.unity](../Assets/Scenes/Demos/Base_Provisoria_Integrada.unity) | Cena de integracao atual |
| [ArtilleryTestController.cs](../Assets/Scripts/Gameplay/Artillery/ArtilleryTestController.cs) | Pivots, limites e leitura dos instrumentos |
| [HandwheelInteraction.cs](../Assets/Scripts/Gameplay/Artillery/HandwheelInteraction.cs) | Interacao com os volantes |
| [ArtilleryGaugeDriver.cs](../Assets/Scripts/Gameplay/Artillery/ArtilleryGaugeDriver.cs) | Ponteiros do modelo de instrumentos |
| [IntegratedBattlefieldRig.cs](../Assets/Scripts/Prototypes/IntegratedBattlefieldRig.cs) | Cameras, jogador e modo de operacao |
| [CommandRoomInteractor.cs](../Assets/Scripts/Gameplay/CommandRoom/CommandRoomInteractor.cs) | Interacao em primeira pessoa |
| [TerrainWorldController.cs](../Assets/Scripts/World/ProceduralTerrain/TerrainWorldController.cs) | Geracao e carregamento do terreno |
| [WorldWeatherProvider.cs](../Assets/Scripts/World/WorldWeatherProvider.cs) | Consulta de vento, visibilidade e chuva |
| [IntegratedBattlefieldSceneBuilder.cs](../Assets/Editor/SceneBuilders/IntegratedBattlefieldSceneBuilder.cs) | Montagem da cena de demonstracao |
| [Guia da base](../Assets/Documentation/BASE-PROVISORIA-INTEGRADA.md) | Teste da cena e referencias do Inspector |

## Responsabilidades futuras propostas

Os nomes abaixo descrevem responsabilidades, nao arquivos que ja existem:

| Responsabilidade | Limite |
| --- | --- |
| Coordenadas e grade | Conversao entre mundo, mapa e quadriculas |
| Ferramentas do mapa | Pontos, linhas, medidas, selecao e anotacoes |
| Inteligencia recebida | Relatos e informacoes conhecidas pelo jogador |
| Modelo de alcance | Dados comuns usados por tabela, calculadora e tiro |
| Configuracao de peca | Carga, municao, elevacao e identificacao do canhao |
| Direcao compartilhada | Azimute comum das duas pecas futuras |
| Resolucao do disparo | Configuracao registrada, tempo de viagem e ponto de impacto |
| Impactos e dano | Consequencias no mundo |
| Comunicacao da queda | Momento em que o jogador recebe o resultado |
| Reconhecimento | Captura, entrega e armazenamento de fotografias |

Evitar adicionar toda a operacao ao controlador de teste atual. Ele pode continuar responsavel pelos pivots enquanto os sistemas de calculo, informacao e disparo mantem responsabilidades proprias.

## Etapas propostas

### 1. Mapa e coordenadas

Construir a grade de 1 km e 100 m, conversao de coordenadas, norte comum, linhas com distancia e angulo, marcacoes, zoom e deslocamento.

**Aceite:** uma linha de 1.000 metros no mundo mede 1 km no mapa; ampliar ou deslocar a visualizacao nao altera essa medida; uma linha de 100 m corresponde a uma subquadricula.

### 2. Relatos e localizacao

Entregar dois relatos de observadores e permitir que o jogador registre suas informacoes e trabalhe a triangulacao de um alvo fixo.

**Aceite:** o alvo real nao aparece automaticamente; as medidas e marcacoes usadas pelo operador persistem durante a operacao.

### 3. Cargas, tabelas e calculadora

Definir o modelo de alcance do jogo, as faixas das cinco cargas e a calculadora de elevacao com entradas manuais.

**Aceite:** entradas validas fornecem uma recomendacao coerente; uma distancia fora da faixa produz um estado incompativel; o calculo nao gira o canhao automaticamente.

### 4. Disparo e retorno

Implementar uma peca com preparacao de carga e municao, animacao, tempo de viagem, impacto, dano e relato atrasado.

**Aceite:** mover o canhao depois do disparo nao altera o tiro anterior; o dano ocorre na chegada; a queda aparece no mapa apenas quando o relato e entregue.

### 5. Clima e correcao

Informar vento ao operador e aplicar o componente calculavel e a dispersao residual.

**Aceite:** testes controlados permitem observar o efeito do vento e melhorar a correcao; condicoes iguais com a mesma semente permitem reproducao durante a verificacao.

### 6. Reconhecimento fotografico

Produzir fotos aereas em preto e branco do mundo, entregar a informacao e permitir associa-la ao mapa.

**Aceite:** a imagem corresponde ao local e ao estado no horario de captura; fotos antigas permanecem identificaveis e nao mudam retroativamente.

### 7. Segunda peca e maquina colossal

Adicionar a operacao de duas pecas com azimute compartilhado e configuracoes individuais, depois adaptar os sistemas a maquina colossal.

**Aceite:** os dois canhoes possuem a mesma direcao, podem usar elevacoes e municoes diferentes, podem disparar simultaneamente e geram resultados identificados separadamente.

## Ordem de trabalho

Esta ordem e uma proposta para fechar um ciclo jogavel antes de ampliar o ambiente e a quantidade de sistemas. A implementacao de novas etapas depende de resolver as pendencias relevantes do documento 09.

As imagens de referencia orientam a apresentacao. Valores e mecanismos que contradigam as decisoes do autor nao devem ser copiados como requisitos.
