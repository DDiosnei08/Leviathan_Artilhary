# Grama na borda da base

## Alteracao

A grama deixou de usar o raio de exclusao de arvores e props para a base. Antes,
o raio de 30 m era ampliado por meia celula e pela rejeicao dos quatro cantos da
celula na GPU. Isso produzia uma clareira muito maior que o concreto e com borda
recortada pela grade do terreno.

Agora o compute avalia cada lamina usando a posicao real em coordenadas de mundo.
O concreto continua protegido. Fora dele, a densidade cresce suavemente com uma
pequena variacao espacial deterministica. A variacao apenas afasta a borda:
nunca avanca para dentro da fundacao. Nao depende da resolucao do heightmap nem
do LOD da malha; o mesmo ruido atravessa chunks vizinhos sem reiniciar.

O raio fisico e recuperado de `BaseSite.carveRadius + 0.25 m`, conforme o contrato
de `BaseSpawner.ConfigureFootprint`: o recorte do fosso fica 25 cm dentro do
limite do `Fosso_Piso`. Outro modelo com um contrato diferente exige ajustar
essa correspondencia. O sistema atual assume fundacao circular sem escala no
mundo, como o prefab BaseGiratoria.

## Inspector

Asset: `Assets/Settings/Grass/BaseGiratoriaGrass.asset`, grupo
`Base grass edge (does not affect trees or scenery)`:

| Campo | Valor | Efeito |
| --- | --- | --- |
| Base Clearance | 0.8 m | Faixa sem raizes de grama fora do concreto |
| Base Edge Fade | 2 m | Distancia de transicao ate a densidade normal |
| Base Edge Variation | 1.5 m | Variacao adicional para fora da borda |
| Base Edge Noise Scale | 0.18 | Frequencia espacial da irregularidade |

Para alterar durante Play, ajuste o asset e clique `Rebuild masks and distribution`
no Inspector de `Grama GPU` (GrassManager). Fora de Play, basta iniciar a cena
`Assets/Scenes/Leviathan_BaseGiratoria.unity` novamente. Os parametros de vento,
densidade e cor nao foram alterados.

## Escopo e seguranca

- Arvores, pedras, postes e demais props mantem sua exclusao original.
- Agua, estradas, POIs, volumes manuais e encostas continuam bloqueando grama.
- Nenhuma alteracao de relevo, posicionamento da base, semente ou cena.
- A mudanca de posicao/raio/validade da base invalida a distribuicao mesmo quando
  as alturas do chunk permanecem identicas.
- O ruido e calculado somente na geracao das laminas e na faixa de transicao;
  nao adiciona buffers, texturas nem alocacoes ao Update.
- A margem protege as raizes. Vento, dobra e alargamento das laminas podem
  projetar as pontas sobre essa faixa; aumente Base Clearance para vento extremo.

## Testes

`Assets/Editor/Grass/BaseGrassEdgeValidation.cs` possui dois entry points de
batch, para uma copia isolada do projeto. Ambos encerram o Editor ao concluir:

- `BaseGrassEdgeValidation.FixedBatch`: dois chunks adjacentes, celulas de 10 m,
  comparacao de todos os candidatos GPU com a formula CPU, margem fisica,
  continuidade na divisa, determinismo, realocacao da base sobre alturas iguais,
  remocao da exclusao quando nao ha base e liberacao de buffers.
- `BaseGrassEdgeValidation.IntegratedBatch`: cena real em Play, raizes proximas
  ao concreto sem invasao da margem, preservacao do raio de props, regeneracao
  sem mudar semente/base, verificacao de raizes e mascaras, GC e capturas.

Relatorios, capturas, copia anterior dos arquivos e diff ficam em
`Artifacts/GrassBaseEdge`. Esta validacao nao e uma medicao do custo GPU em ms.

Validado em Unity 6000.6.3f1, URP 17.6.0, DX11 / RX 6600: 17 verificacoes no
teste de chunks e 11 na cena integrada passaram. A cena gerou 2.568 laminas na
faixa proxima a base, sem nenhuma raiz na margem protegida. O resultado se
manteve ao regenerar o terreno, com zero GC no LateUpdate em 30 amostras
estabilizadas. A primeira tentativa do teste integrado encontrou tiles distantes
sem upload; o verificador foi corrigido para ignora-los e executado novamente.
