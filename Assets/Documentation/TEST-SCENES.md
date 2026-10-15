# Cenas de teste do Leviathan2

## Cenas existentes

| Caminho | Finalidade |
| --- | --- |
| `Assets/Scenes/Tests/Test_ArtilleryControls/Test_ArtilleryControls.unity` | Canhao, volantes e instrumentos isolados |
| `Assets/Scenes/Tests/Test_ControllerPlayer/Test_controlsPlayer.unity` | Jogador e interacoes da sala de comando |
| `Assets/Scenes/Demos/Base_Provisoria_Integrada.unity` | Integracao preservada da base no terreno |
| `Assets/Scenes/Demos/Base_Cartum_Terreno_Agua.unity` | Demonstracao visual cartum, base e agua |
| `Assets/Scenes/Templates/SampleScene.unity` | Cena original do template Unity |

As cenas antigas chamadas Test_01_Terrain, Test_02_Walk e Test_03_Gun nao existem
neste projeto. Disparo, mapa, observadores e missoes sao etapas futuras.

## Criar novos testes

1. Duplique uma cena adequada e salve em `Assets/Scenes/Tests/<Sistema>/`.
2. Use um preset proprio em `Assets/Settings/Terrain` quando precisar de outra seed.
3. Adicione somente os componentes relacionados ao teste.
4. Verifique o Console, os controles e as referencias antes de integrar na demo.

Os montadores MonoBehaviour das cenas isoladas ficam em `Assets/Scripts/Prototypes`;
eles permanecem componentes runtime para conservar as referencias serializadas.
Os montadores das demonstracoes sao ferramentas do editor em `Assets/Editor/SceneBuilders`.

Abrir uma cena nao exige altera-la em Build Settings. SampleScene continua sendo
a unica cena habilitada no build; a reorganizacao manteve essa escolha.

## Cuidados

Os comandos de montagem podem reconstruir as demonstracoes. Para testar os assets
sem remontar cenas, use `Tools > Leviathan > Validation > Check Organized Project`.
Os guias principais ficam em `../../docs` e os resultados de testes em `../../Artifacts`.
