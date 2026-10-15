# Varredura e limpeza

Data: 14/10/2026. Unity 6000.6.3f1, URP 17.6.0, DX11.

## Escopo

Revisao de scripts, assets e referencias das duas cenas mantidas, incluindo
ProjectSettings e caminhos de carregamento usados pelas ferramentas de geracao.
Nao houve alteracao de relevo, densidade, cores, seed, prefab da base ou cenas.
Arquivos gerados em Library, pacotes instalados e arquivos arquivados em entregas
anteriores nao foram tratados como codigo descartavel.

## Correcoes

- TerrainWorldController reutiliza as colecoes do streaming finito e infinito,
  em vez de criar HashSet e List em cada Update.
- TerrainSettings reutiliza o conjunto de IDs de POIs na validacao. IDs duplicados
  continuam causando erro, inclusive apos chamadas repetidas e clonagem do preset.
- A estatistica de triangulos usa GetIndexCount, sem copiar mesh.triangles para
  arrays gerenciados apenas para exibir o total.
- WorldSceneryManager seleciona os props mais proximos para o pool limitado de
  colliders, independentemente da ordem de carregamento dos chunks.
- Colliders existentes sao desativados ao zerar distancia ou orcamento, perder a
  referencia ao jogador ou desabilitar o manager. O refresh normal ocorre a cada
  0,5 segundos, como antes.
- O tamanho do pool acompanha mudancas no orcamento; objetos nao modificados nao
  recebem novamente mesh, pose e toggles de colisao a cada refresh. As matrizes
  e os candidatos de colisao sao reutilizados. Prototipos vazios sao ignorados
  na selecao durante a geracao.
- BasePreviewCoordinator ignora spawners sem prefab atribuido, evitando consultar
  um caminho de asset invalido no preview.
- BaseSpawner guarda o GUID do preset original antes do Play e restaura a
  referencia por uma sequencia limitada de updates do Editor apos sair. Isso
  protege contra a reaplicacao tardia dos campos com scene reload desativado.
  O teste agora aguarda o tempo real em Edit mode e busca novamente os componentes
  da cena antes de conferir a referencia; nao usa o tempo de teardown como espera.
- Capturas criam sua pasta de destino e usam finally para restaurar a camera e
  liberar as texturas mesmo quando a gravacao falha.
- Removidos o campo GrassTile.cell, calculos sem uso de media da altura da base
  e o loop de relaxamento que tinha somente uma passagem. A escolha da base
  continua exigindo apoio natural; nao existe fallback para encostas.
- Amostras de proeminencia da base deixam de ser calculadas quando seu peso e zero.

## Retirado de Assets

**46 arquivos, 130.771 bytes, incluindo .meta**, foram movidos para
`Artifacts/ProjectSweep/Removed`, preservando caminhos, conteudo e GUIDs.

| Grupo | Motivo |
| --- | --- |
| Samples/UnityTemplate | Tutorial, layout, Readme, icone e estilos do template; sem uso nas cenas ou ferramentas atuais |
| Scripts/CommandAndPlayerTest | Construtor de sala duplicado, sem referencias; a versao de Prototypes foi mantida |
| Materials/Legacy/MeshMaterial | Material antigo sem referencias nem carregamento por codigo |
| Models/Vegetation | Quatro meshes primitivas substituidas pelo carvalho e pinheiro importados |
| Materials/Base/BaseSandbags e BaseSupplies | Sem uso nas cenas; o gerador historico pode recria-los quando solicitado |
| Scripts/Gameplay/TerrainPlacer | Helper sem componente serializado, chamada ou criacao por codigo; preservado no arquivo para eventual reutilizacao |
| Settings/SampleSceneProfile | Perfil orfao importado com um pacote; nao e o perfil ativo em Settings/Volumes |
| Scenes/Demos.meta | Marcador de pasta sem cena mantida |

O gerador de arvores primitivas foi preservado: seu setup recria a pasta e as
meshes quando executado. Nenhuma referencia ativa depende dos GUIDs arquivados.
O perfil importado arquivado inclui suas alteracoes locais, sem restaurar a
versao do Git. Nao foi apagado nenhum download de forma definitiva.

No renderer PC, foram removidas sete referencias quebradas do container obsoleto
probeVolumeResources. O URP instalado usa os recursos atuais em GraphicsSettings;
modo de renderizacao, SSAO, mascaras, materiais e luzes foram mantidos.
Do perfil global DefaultVolumeProfile foram retirados nove subassets orfaos de
testes ou componentes sem script. Nenhum deles constava na lista de componentes
do perfil; os efeitos validos e suas configuracoes ficaram intactos.
Esses dois assets modificados possuem backups em Before, com os mesmos GUIDs.

`RemovedManifest.csv` registra cada caminho, tamanho e SHA-256. Para restaurar
um item, feche o Play e mova-o, junto com seu .meta, de Removed para o mesmo
caminho relativo no projeto; confira antes se ja existe outro arquivo ali.

## Preservado intencionalmente

- Modelos e materiais fonte em ThirdParty/Environment: usados pelo baker, mesmo
  quando somente suas meshes derivadas aparecem nas cenas.
- Presets historicos de terreno e grama, builders, validadores e benchmark:
  possuem referencias por codigo e permitem reproduzir etapas anteriores.
- Includes HLSL, ScriptableObjects de configuracao, perfis URP, input actions e
  objetos criados por codigo. Ausencia no grafo serializado nao prova desuso.
- Configuracao de Play sem domain/scene reload e seus hooks de cleanup.
- Documentacao historica, arquivos em Artifacts e configuracoes dos pacotes.

## Verificacao

Validacao executada em uma copia isolada, sem fechar ou substituir o Editor do
projeto principal. O validador estrutural conferiu as duas cenas, tipos de scripts,
materiais, modelo e Build Settings apos a limpeza. Os hashes de ambas as cenas
permaneceram iguais aos de antes desta etapa.

20 verificacoes adicionais passaram, incluindo inicializacao em Play, base unica,
ausencia de Canvas, selecao dos oito colliders mais proximos, limites do pool,
desativacao correta, IDs de POIs duplicados, estatisticas de triangulos e captura
com pasta nova e falha real de gravacao.

Tambem passaram 49 verificacoes de base/artilharia, incluindo dois ciclos de
Play, e 61 de curadoria/vegetacao, incluindo apoio natural em dez sementes e
regeneracao deterministica. Total: **130 verificacoes aprovadas**.
A checagem textual de Assets contra os GUIDs dos assets e pacotes instalados
terminou sem referencia ausente, excluindo identificadores internos da Unity.
As fontes do projeto principal foram comparadas com a copia validada.

Medicoes de alocacao gerenciada, na thread principal e apos aquecimento:

| Caminho medido | Chamadas | Bytes alocados |
| --- | ---: | ---: |
| TerrainSettings.Validate | 1.000 | 0 |
| Update do terreno com chunks residentes | 100 | 0 |
| Refresh de colliders sem mudanca de props | 100 | 0 |
| Consulta da quantidade de triangulos | 100 | 0 |

Essas medicoes nao sao uma promessa de zero GC no jogo inteiro. Criacao de chunks,
mudanca de LOD, regeneracao, leitura diagnostica de meshes e crescimento ou troca
do pool ainda podem alocar. Nao foi medido custo de GPU nesta varredura.

## Relatorios e repeticao

- `Artifacts/ProjectSweep/DependenciesBefore.tsv`: grafo anterior a limpeza.
- `Artifacts/ProjectSweep/AssetDependencies.tsv`: grafo apos a limpeza; inclui
  dependencias das cenas e dos assets referenciados em ProjectSettings.
- `Artifacts/ProjectSweep/ReviewCandidates.txt`: candidatos a revisao, nao uma
  lista segura para apagar. Chamadas C#, caminhos montados e includes exigem revisao.
- `Artifacts/ProjectSweep/Validation.txt`: resultados dos testes novos.
- `Artifacts/ProjectSweep/BaseRegression.txt` e `CurationRegression.txt`: testes
  de canhoes, ciclos de Play, sementes e vegetacao.
- `Artifacts/ProjectSweep/GuidValidation.txt`: checagem final de GUIDs.
- `Artifacts/ProjectSweep/Captures`: imagens da cena apos a limpeza.
- `Artifacts/ProjectSweep/Changes.diff`: diff dos scripts, assets e indices existentes.
- `Artifacts/ProjectSweep/Before`: backup dos arquivos alterados nesta etapa.

O menu **Tools > Leviathan > Validation > Write Asset Dependency Audit** atualiza
o inventario sem alterar cenas ou apagar assets. Para repetir os testes em uma
sessao dedicada, execute `ProjectSweepValidation.ValidateBatch`. Esse metodo
entra em Play e encerra o Editor ao terminar; nao use na sessao de trabalho.

Os builders e testes antigos dos documentos 08 a 13 podem depender de demonstracoes
arquivadas. Use a cena Leviathan_BaseGiratoria e os validadores BaseIntegrationValidation,
AssetCurationValidation e ProjectSweepValidation para o estado atual.
Migrar todos os testes historicos para a cena atual, substituir APIs obsoletas do
Unity em todo o projeto e medir GPU do ambiente completo ficam como proximas melhorias.
Os logs da copia isolada ainda registram um erro interno do indexador
UnityEditor.Search no startup; ele e separado dos erros de gameplay pelos
validadores. Tambem permanecem warnings de APIs obsoletas em scripts historicos.
