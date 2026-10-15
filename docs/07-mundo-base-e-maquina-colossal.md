# 07 - Mundo, base e maquina colossal

## Mundo procedural

**Existente:** o modulo de geracao procedural do projeto `Leviathan` foi incorporado ao `Leviathan2`. O prototipo usa um mundo baseado em semente, chunks, relevo, cores de superficie, elementos procedurais e pontos de interesse.

**Diretriz proposta:** o mapa, as posicoes de observadores, os impactos e as fotografias devem corresponder ao mesmo mundo e a mesma semente. Um ponto marcado no mapa precisa manter a mesma coordenada ao ser consultado por qualquer sistema.

O desenho visual do mapa pode ser estilizado, mas as medidas precisam continuar coerentes com o mundo.

## Escala do jogo

As medicoes devem trabalhar em metros e podem ser apresentadas em quilometros. **Proposta tecnica:** adotar 1 unidade de mundo como 1 metro nos sistemas de operacao.

| Valor | Papel |
| --- | --- |
| 100 m | Subquadricula e resolucao acordada para a leitura de distancia do instrumento |
| 1 km | Quadricula principal |
| 2 a 25 km | Faixas iniciais de alcance das cinco cargas |
| 25 km | Alcance maximo inicial confirmado |

Um alcance de 25 km em qualquer direcao a partir de uma base central requer uma area de pelo menos 50 km de lado, antes de considerar margens para desvios. Isso e uma referencia geometrica para dimensionar o mundo, nao uma alteracao ja aplicada aos assets.

## Dimensoes atuais

| Configuracao existente | Valores consultados | Consequencia |
| --- | --- | --- |
| BaseProvisoriaTerrain | Chunks de 1.200 m; mapa de 3 x 3 chunks | Area nominal de demonstracao de 3,6 km de lado |
| LeviathanTerrain | Chunks de 1.200 m; mapa de 17 x 17 chunks | Area nominal de 20,4 km de lado |

Essas configuracoes ainda precisam ser revistas para suportar os disparos de 25 km e a area operacional pretendida. O tamanho da janela carregada perto do jogador pode ser menor que a extensao total do mundo.

## Base provisoria

**Existente:** a cena integrada possui fundacao, rampa, delimitacao com sacos de areia, suprimentos visuais, sinalizacao, canhao, instrumentos e sala de comando.

Ela permite validar a operacao em primeira pessoa e serve de base para os proximos sistemas. A mesa de mapa atual e um elemento visual, ainda sem o comportamento operacional especificado.

**Proposta:** usar a sala e a mesa existentes para testar o mapa, os relatos e as calculadoras antes de construir os interiores definitivos da maquina colossal.

## Maquina colossal

**Acordado:** construir futuramente uma maquina de grandes dimensoes com dois canhoes de artilharia de grande capacidade.

Ela deve acomodar a operacao individual, com estacoes no mesmo local. A direcao e compartilhada entre as pecas; as elevacoes, cargas e municoes podem diferir. Disparos simultaneos sao possiveis.

A maquina permanece parada durante a operacao de tiro. Locomocao fora desse periodo, forma visual, dimensoes, energia, sistemas auxiliares e manutencao ainda nao estao definidos.

## Cuidados de integracao propostos

- Separar extensao do mundo e quantidade de terreno carregado.
- Garantir que impactos distantes possam ser resolvidos sem depender de um objeto de projetil ativo.
- Garantir que dano e reconhecimento fora da janela visivel usem o estado correto da regiao.
- Registrar a origem da peca no disparo para manter as medidas coerentes.
- Alinhar norte do mapa, coordenadas de mundo e azimute dos canhoes.
- Evitar que uma regeneracao do terreno torne marcacoes, alvos e fotografias incompativeis com a semente atual.
