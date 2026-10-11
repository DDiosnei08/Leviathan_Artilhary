# Registro de procedência

O jogo poderá ser comercializado. Por isso cada técnica usada fica registrada aqui: o que foi consultado,
o que foi aproveitado só como ideia, o que foi escrito internamente e a licença aplicável.

| Elemento | Referência consultada | Aproveitado | Código | Licença / observação |
| --- | --- | --- | --- | --- |
| Ruído de gradiente 2D (`GradientNoise`) | Técnica clássica de ruído de Perlin (domínio público como método); curva de suavização quíntica `6t^5 - 15t^4 + 10t^3` | Só o método | Escrito internamente. As **derivadas analíticas foram deduzidas à mão** (regra do produto) e conferidas contra diferenças finitas | Método sem restrição. Nenhum código de terceiros |
| Hash de células | Funções de mistura de inteiros em geral | Só a ideia de embaralhar bits | Escrito internamente (constantes comuns de mistura de 32 bits) | Constantes numéricas genéricas; sem código copiado |
| fBm (soma de oitavas) | Técnica padrão | Só o método | Escrito internamente | Sem restrição |
| Contraste do relevo | Nenhuma | Nada | Função racional própria `x (1 + k) / (1 + k abs(x))` | Interno |
| Zonas planas | Nenhuma externa; evolução do gerador de zonas planas do projeto anterior do próprio desenvolvedor | Ideia própria | Escrito internamente | Interno |
| `smoothstep` | Função padrão | Só a definição | Escrito internamente | Sem restrição |
| Filtro de erosão por gradiente (**Fase 2, ainda não implementado**) | [Fast and Gorgeous Erosion Filter](https://blog.runevision.com/2026/03/), Rune Skovbo Johansen (30/03/2026) | Descrição do método | A escrever internamente | O autor declara MPL-2.0 para o código dele. Não copiamos o código. **Dar crédito** quando for implementado |

## Consultado e NÃO usado

| Referência | Motivo |
| --- | --- |
| Repositório AlexSerodio/procedural-terrain-generation | Licença GPL-3.0 (copiar código obrigaria a abrir o jogo). Foi aberto só para avaliar o formato de exportação e as etapas de uma ferramenta de terreno (alguns arquivos foram lidos para essa avaliação). Nenhum código foi copiado nem adaptado |
| Shader do Shadertoy `7ljcRW` | Licença não verificada (o padrão do Shadertoy é CC BY-NC-SA 3.0, que proíbe uso comercial). Não consultado para cópia |

## Ferramentas de apoio (não fazem parte do jogo)

A calibração dos parâmetros iniciais foi feita com uma implementação de referência em Python (fora do repositório),
escrita para este projeto. Os valores de referência dos testes (`TerrainHeightSamplerTests.ReferenceValues_...`) vêm dela.

## Regra para novas entradas

Antes de adicionar uma técnica ou um arquivo de terceiros (código, shader, textura, modelo, fonte):
1. Registrar aqui a referência e a licença.
2. Se a licença exigir crédito ou restringir uso comercial, decidir antes de integrar.
3. Copiar apenas scripts próprios do projeto anterior, nunca dependências, shaders ou recursos de terceiros sem verificar a licença.
