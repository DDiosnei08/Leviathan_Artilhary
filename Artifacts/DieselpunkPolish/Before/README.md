# Leviathan2

Projeto Unity do prototipo Leviathan. Unity 6000.6.3f1, Universal Render Pipeline 17.6.0.

## Comecar

Abra o projeto no Unity Hub e abra
[Leviathan_BaseGiratoria](Assets/Scenes/Leviathan_BaseGiratoria.unity).
A cena integra o FBX BaseCompleta, dois canhoes, uma base por semente,
terreno procedural, agua, grama e instrumentos fisicos.
A cena tambem possui [arvores, grama mais densa e erosao simples](docs/15-ambiente-vegetacao-e-erosao.md).
A base escolhe terreno naturalmente plano, sem gerar um pedestal no heightmap.
Os [downloads selecionados e o cenario procedural](docs/16-assets-e-cenario-procedural.md)
incluem bosques, pedras, postes, barreiras e suprimentos. Recursos fora de uso
ficam arquivados em `Artifacts/AssetCuration/Unused`, fora de `Assets`.
O [teste de artilharia](Assets/Scenes/Tests/Test_ArtilleryControls/Test_ArtilleryControls.unity)
e a unica outra cena mantida. As cenas anteriores foram arquivadas fora de Assets.

O prototipo possui carga, ferrolho, recuo, rotacao compartilhada e elevacao independente.
Resolucao balistica, calculadoras, triangulacao e relatos continuam futuros, descritos em [docs](docs/README.md).
Veja [setup da nova base](docs/14-base-giratoria.md) para os controles e o Inspector.
Os controles agora usam [instrumentos fisicos, sem HUD](docs/12-interacao-instrumentos-fisicos.md):
volantes, alavancas, manivela e botoes sobre a mesa, com ponteiros analogicos.

## Organizacao

- `Assets/Scripts/Gameplay`: artilharia, jogador e sala de comando.
- `Assets/Scripts/World`: terreno procedural e dados de clima.
- `Assets/Scripts/Prototypes`: montagem e coordenacao das demonstracoes.
- `Assets/Editor`: ferramentas exclusivas do editor e validacao.
- `Assets/Scenes`: cena integrada e teste isolado de artilharia.
- `Assets/Models`: modelos importados, com seus arquivos `.meta`.
- `Assets/Materials`, `Assets/Shaders`, `Assets/Settings`: recursos visuais e configuracoes.
- `Assets/Documentation`: guias consultaveis dentro do projeto.
- `docs`: especificacao do jogo e documentacao tecnica.
- `Artifacts`: capturas, logs e registro da reorganizacao.

Veja [estrutura e convencoes](docs/11-estrutura-do-projeto.md) para os caminhos completos.
Veja [varredura e limpeza](docs/17-varredura-e-limpeza.md) para o inventario de dependencias,
as correcoes de desempenho e os arquivos retirados de `Assets` de forma recuperavel.
Nao mover assets sem seus `.meta`; preferir a janela Project da Unity.
`Library`, `Temp`, `Logs`, `.csproj` e `.slnx` sao arquivos gerados, nao codigo do jogo.
