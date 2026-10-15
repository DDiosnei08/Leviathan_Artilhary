# 06 - Reconhecimento e relatos

## Canais de informacao

**Acordado:** as informacoes podem chegar por radio ou instrumentos como maquinas de escrever.

A apresentacao fisica da mensagem faz parte da operacao. Conteudo, velocidade de impressao, som, voz e formato definitivo ainda nao foram escolhidos.

## Informacoes antes do disparo

Os relatos podem trazer posicoes de observadores, direcoes de avistamento e pontos de referencia que precisam ser marcados pelo jogador.

O exemplo principal e combinar duas observacoes para triangular uma posicao inimiga. O relato fornece dados para o trabalho do operador; ele nao precisa trazer uma coordenada final ja resolvida.

## Retorno de queda

**Acordado:** o jogador nao observa diretamente a explosao distante. Recebe um retorno como "caiu curto e a esquerda", e o mapa passa a mostrar o local da queda quando a informacao chega.

**Pendente:** em relacao a qual direcao o relato usa "esquerda", "direita", "curto" e "longo". E preciso escolher um referencial comum entre o que o observador diz e o que o jogador mede.

**Proposta de conteudo:** identificacao do tiro e da peca, horario da observacao, descricao do desvio, posicao observada e efeito percebido. Precisao e nivel de detalhe podem ser ajustados.

## Fotografias de reconhecimento

**Acordado:** permitir adicionar ao mapa imagens do impacto, em preto e branco, como fotografias feitas por avioes de reconhecimento.

As fotografias funcionam como informacao para avaliar o resultado do disparo. O autor nao definiu ainda uma simulacao de voo dos avioes; a origem ficcional da fotografia nao exige implementar essa aeronave nesta etapa.

### Comportamentos propostos

- Gerar uma captura aerea da regiao observada no proprio mundo procedural.
- Converter a apresentacao para preto e branco.
- Mostrar impactos e danos que realmente ocorreram naquela regiao.
- Associar a foto a uma area do mapa e, quando possivel, a um tiro.
- Registrar horario de captura e de entrega.
- Permitir fixar, selecionar, ampliar, reposicionar e retirar a imagem.
- Preservar fotos anteriores para comparar resultados.

Esses comportamentos sao propostas de implementacao que concretizam a funcionalidade acordada. Grao, bordas, carimbos e outras caracteristicas visuais ainda podem ser escolhidos.

### Momento da fotografia

A foto deve representar o estado da regiao no instante da captura. Se chegar mais tarde, nao deve mudar para representar automaticamente o estado mais recente.

**Proposta:** tratar chegada do relato e chegada da fotografia como eventos separados. O relato pode chegar primeiro e a fotografia depois. Tempos, disponibilidade e custo desse reconhecimento estao pendentes.

### Fotografias e marcacoes

O mapa recebe a queda quando chega a comunicacao correspondente. Fotografias devem respeitar o momento em que sao entregues ao jogador.

Uma foto antiga pode continuar sendo util como comparacao. Ela precisa ter identificacao suficiente para o jogador reconhecer que se trata de uma observacao passada.

## Historico proposto

Guardar os relatos e fotos entregues permite consultar o que foi recebido, em vez de depender apenas de mensagens temporarias. O historico pode relacionar cada item ao tiro, observador, horario e regiao correspondentes.
