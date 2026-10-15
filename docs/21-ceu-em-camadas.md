# Ceu procedural em camadas

## Implementado

- Dia, amanhecer, entardecer e noite com transicoes continuas de cor.
- Sol e lua visuais, estrelas deterministicas com variacao de cor e cintilacao.
- Aurora verde/ciano com pontas rosadas, cortinas animadas e direcao ajustavel.
- Duas camadas de nuvens, com alturas, escalas, coberturas e velocidades distintas.
- Iluminacao solar/lunar, ambiente e cor da neblina acompanhando o horario.
- Integracao na cena `Assets/Scenes/Leviathan_BaseGiratoria.unity`.

Unity utiliza um unico `RenderSettings.skybox`. As duas camadas estao no mesmo
shader: cada raio da camera intersecta um plano em coordenadas de mundo. A
translacao da camera altera o ponto amostrado de forma diferente em cada altura,
criando paralaxe real de amostragem sem outra camera ou esfera transparente.
O horizonte desvanece as nuvens para evitar amostragem extrema perto do plano.

## Arquivos

- `Assets/Shaders/Environment/LayeredSky.shader`: um passe de fundo URP, sem
  texturas externas, ray marching, buffers ou geometria de nuvem adicional.
- `Assets/Materials/Environment/LeviathanLayeredSky.mat`: paleta e parametros.
- `Assets/Scripts/World/LayeredSkyController.cs`: relogio, vento, luzes e ambiente.
- `Assets/Editor/Environment/LayeredSkyControllerEditor.cs`: presets no Inspector.
- `Assets/Editor/Environment/LayeredSkySetup.cs`: instalacao pela ferramenta
  `Leviathan > Visual > Instalar ceu em camadas`.

O material antigo `DieselpunkSky.mat` foi mantido. Os demais materiais, shaders,
relevo, semente, local da base e controles nao foram alterados.

## Inspector

Selecione **Ceu Procedural** na Hierarchy:

| Campo | Uso |
| --- | --- |
| Time Of Day | Hora inicial, de 0 a 24. Padrao: 09:30 |
| Cycle Time | Avanca o horario no Play |
| Minutes Per Day | Duracao de um ciclo completo. Padrao: 30 min |
| Animate Atmosphere | Movimento das nuvens, estrelas e aurora |
| Orbit Tilt / Sunrise Bearing | Inclinacao do percurso solar e direcao do nascer do sol |
| Sun / Moon Intensity | Intensidade da iluminacao do cenario |
| Update Environment | Atualiza ocasionalmente o ambiente/reflexos do skybox |
| Environment Update Interval | Intervalo minimo entre atualizacoes. Padrao: 30 s |

Os botoes **00:00 / 06:00 / 12:00 / 18:00** permitem testar rapidamente cada
fase. Para mante-la fixa, desmarque `Cycle Time`. O horario atual aparece abaixo
dos campos. No Play, os presets mudam o relogio de execucao, sem substituir a
hora inicial salva. No Editor, mudam a hora inicial e participam do Undo.

O ciclo usa `Time.deltaTime`: pausar o tempo do jogo tambem pausa o ceu.
O preview do Editor mostra o horario escolhido, mas nao executa o ciclo.

No material **LeviathanLayeredSky**:

- Day/Twilight/Night: cores do zenite, horizonte, amanhecer e entardecer.
- Star Density/Brightness/Field Scale/Sky Seed: quantidade visual, brilho,
  tamanho do padrao e distribuicao estavel das estrelas.
- Aurora Strength/Speed/Bearing e cores: intensidade, movimento e orientacao;
  bearing 0 aponta para o norte do mundo (+Z).
- Cloud Low/High Height: altitudes em Y do mundo, padrao 1.350 e 3.000 m.
- Cloud Low/High Scale/Coverage, Softness e Opacity: tamanho, cobertura e borda.
- Cloud Wind Multiplier: intensidade visual da deriva. A camada alta se move
  a 55% da velocidade da baixa. Ambas seguem o vento do `WorldWeatherProvider`.

## Integracao e limites

A lua e uma segunda luz direcional, mas fica desativada durante o dia. A luz
dominante e indicada em `RenderSettings.sun`, permitindo que os shaders que
usam apenas a luz principal da URP recebam iluminacao lunar. A neblina mantem
as distancias anteriores; apenas sua cor muda.

O controlador nao modifica o material compartilhado no Play: horario e movimento
sao uniforms globais. Ele restaura luzes, ambiente e uniforms ao ser desativado.
Ha uma unica instancia ativa por mundo. As referencias de material, sol e lua
devem permanecer atribuidas; a cena ja esta configurada. A inicializacao suporta
Play com recarga de dominio/cena desativada.

As nuvens sao uma representacao 2D em planos, nao volumes atravessaveis. Nao
projetam sombras no terreno. Mantenha as alturas acima dos pontos onde o jogador
circula; uma camada abaixo da camera nao e renderizada. A cobertura de chuva
nao e alterada automaticamente. O ciclo e artistico, nao astronomia real; a lua
esta oposta ao sol e nao possui fases. A aurora e concentrada numa direcao,
portanto nao aparece igualmente em todas as vistas.

Os reflexos/ambiente sao atualizados periodicamente para evitar custo por frame;
podem ficar atrasados ao saltar manualmente de horario. O efeito nao foi medido
isoladamente em milissegundos de GPU. Use o contador FPS existente para comparar
e o Unity GPU Profiler para medir o passe de skybox.

## Validacao

Unity 6000.6.3f1 / URP 17.6.0 / DX11 / RX 6600. O verificador
`LayeredSkyValidation.ValidateBatch` confirmou por imagens GPU as quatro fases,
estrelas, aurora, paralaxe independente das duas camadas, vento e animacao da
aurora isolada. Tambem verifica virada do relogio, preservacao da hora inicial,
base unica e restauracao do ambiente. Ele encerra o Editor ao terminar, portanto
deve ser executado em uma copia isolada.

Relatorios, capturas, backup da cena anterior e diff:
`Artifacts/LayeredSky`. As capturas de camera nao incluem o overlay IMGUI de FPS.

Resultado: 22 verificacoes do ceu e 57 verificacoes da base passaram, incluindo
duas sessoes de Play sem recarga de cena. O teste isolado identificou 2.692 pixels
afetados pelas estrelas e 24.821 pela aurora, alem de confirmar sua animacao.
