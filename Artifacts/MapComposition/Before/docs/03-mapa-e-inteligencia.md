# 03 - Mapa e inteligencia

## Papel do mapa

O mapa e a principal superficie de planejamento do operador. As referencias fornecidas mostram uma grade sobre terreno representado por fotografia aerea ou desenho cartografico, com linhas, distancias, direcoes e marcacoes.

As duas apresentacoes sao referencias visuais. A escolha definitiva entre mapa cartografico, fotografia de base ou alternancia de camadas permanece aberta.

## Escala acordada

| Elemento | Dimensao |
| --- | --- |
| Quadricula principal | 1 x 1 km, ou 1.000 x 1.000 m |
| Subquadricula | 0,10 x 0,10 km, ou 100 x 100 m |
| Subdivisoes por lado | 10 |
| Subquadriculas por quadricula principal | 100, organizadas em 10 x 10 |

As quadriculas principais usam letras e numeros, como A1 e C4. O formato de identificacao das subquadriculas ainda precisa ser escolhido.

**Proposta de convencao:** norte para cima, leste para a direita, azimute de 0 graus no norte e crescimento no sentido horario. A origem da numeracao e o tratamento de letras apos Z devem ser fixados antes da implementacao.

## Interacoes acordadas

- Selecionar um ponto e arrastar para criar uma linha.
- Mostrar distancia e angulo da linha.
- Marcar pontos de referencia recebidos ou reconhecidos.
- Marcar posicoes dos observadores.
- Trabalhar a triangulacao a partir de posicoes e direcoes informadas.
- Consultar uma bussola fixa ao lado do mapa.
- Usar um transferidor.
- Receber a marcacao da queda quando o relato chegar.
- Adicionar fotografias de reconhecimento ao mapa.

## Interacoes propostas para tornar o mapa utilizavel

- Ampliar e deslocar a visualizacao mantendo a escala correta das medicoes.
- Preservar varias linhas para comparacao e triangulacao.
- Selecionar e remover uma linha ou marcacao especifica.
- Diferenciar observador, referencia, alvo estimado e queda confirmada.
- Dar nome ou anotacao aos pontos marcados.
- Exibir a grade de 100 metros com legibilidade em aproximacoes maiores.
- Evitar que fotografias fixadas escondam permanentemente as medicoes; permitir reposicionar e retirar.

Essas interacoes sao propostas de implementacao, nao funcionalidades existentes.

## Origem das informacoes

**Acordado:** os dados podem chegar por radio ou instrumentos como maquinas de escrever. Um exemplo central e receber dois relatos com as posicoes de dois observadores e a direcao em que cada um avistou o inimigo.

Pontos de referencia tambem podem fazer parte das informacoes. O jogador precisa marca-los para usa-los no trabalho sobre o mapa.

**Proposta de estrutura de relato:** identificador do observador, horario, posicao ou referencia, direcao observada e observacao textual. Precisao e formato dos dados ainda estao abertos.

## Triangulacao manual

O jogador representa no mapa as informacoes dos observadores e usa as linhas para estimar o ponto de encontro das direcoes.

A ferramenta de linha fornece medidas entre os pontos escolhidos. Nao foi acordado um sistema que resolva e marque o inimigo automaticamente.

Relatos com direcoes quase paralelas, observacoes insuficientes ou referencias mal posicionadas precisam ter um comportamento definido durante a implementacao. O mapa nao deve inventar uma solucao quando a informacao nao permite uma localizacao confiavel.

## Separacao entre mundo e conhecimento

**Diretriz de implementacao proposta:** manter separadas a posicao real do inimigo, conhecida pela simulacao, e a posicao estimada pelo jogador.

Da mesma forma, o sistema pode conhecer um impacto antes de o operador receber a informacao. A marcacao no mapa deve respeitar o recebimento do relato.

Marcacoes manuais devem ser identificaveis como estimativas, enquanto relatos e fotografias devem preservar a origem da informacao.

## Exemplo de coordenada

`C4` identifica uma area de 1 km de lado. Um ponto dentro dela pode ser descrito usando deslocamentos de 100 metros ou indices das subquadriculas. O exemplo nao define ainda qual dessas notacoes sera adotada.

Uma selecao sobre o mapa precisa corresponder ao mesmo ponto do mundo procedural, inclusive depois de ampliar, deslocar ou redimensionar a visualizacao.
