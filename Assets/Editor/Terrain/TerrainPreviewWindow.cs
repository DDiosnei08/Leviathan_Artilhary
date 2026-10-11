using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Leviathan.Terrain.Editor
{
    /// <summary>
    /// Pré-visualização mínima do mapa inteiro (Fase 1). Gera uma imagem do domínio de 20.400 m
    /// a partir de um TerrainSettings, com estatísticas de altura e de área plana.
    /// O Terrain Baker completo (exportação etc.) vem em fase posterior.
    /// A geração roda na thread principal; por isso a resolução máxima é 1024.
    /// </summary>
    public sealed class TerrainPreviewWindow : EditorWindow
    {
        enum PreviewMode
        {
            Relevo,
            Altura,
            Inclinacao,
            ZonasPlanas
        }

        TerrainSettings settings;
        int samplesPerSide = 512;
        PreviewMode mode = PreviewMode.Relevo;
        float verticalExaggeration = 2f;

        HeightGrid grid;
        Texture2D texture;
        Image image;
        Label statusLabel;

        [MenuItem("Tools/Leviathan/Terrain Preview")]
        static void Open()
        {
            GetWindow<TerrainPreviewWindow>("Terrain Preview");
        }

        void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;

            UnityEditor.UIElements.ObjectField settingsField = new UnityEditor.UIElements.ObjectField("Settings")
            {
                objectType = typeof(TerrainSettings),
                allowSceneObjects = false,
                value = settings
            };
            settingsField.RegisterValueChangedCallback(e => settings = e.newValue as TerrainSettings);
            root.Add(settingsField);

            // DropdownField com texto (e não PopupField<int>): com T = int, o terceiro argumento seria
            // interpretado como índice e não como valor.
            DropdownField resolutionField = new DropdownField("Amostras por lado", new List<string> { "256", "512", "1024" }, 1);
            resolutionField.RegisterValueChangedCallback(e => samplesPerSide = int.Parse(e.newValue));
            root.Add(resolutionField);

            EnumField modeField = new EnumField("Visualização", mode);
            modeField.RegisterValueChangedCallback(e =>
            {
                mode = (PreviewMode)e.newValue;
                RebuildTexture();
            });
            root.Add(modeField);

            Slider exaggerationSlider = new Slider("Exagero vertical (relevo)", 1f, 6f) { value = verticalExaggeration };
            exaggerationSlider.RegisterValueChangedCallback(e =>
            {
                verticalExaggeration = e.newValue;
                RebuildTexture();
            });
            root.Add(exaggerationSlider);

            Button generateButton = new Button(Generate) { text = "Gerar pré-visualização" };
            generateButton.style.marginTop = 4;
            root.Add(generateButton);

            statusLabel = new Label("Escolha um TerrainSettings (Assets > Create > Leviathan > Terrain Settings) e clique em Gerar.");
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            statusLabel.style.marginTop = 6;
            root.Add(statusLabel);

            image = new Image { scaleMode = ScaleMode.ScaleToFit };
            image.style.flexGrow = 1;
            image.style.marginTop = 6;
            image.style.minHeight = 256;
            root.Add(image);

            RebuildTexture();
        }

        void OnDestroy()
        {
            DestroyTexture();
        }

        void Generate()
        {
            if (settings == null)
            {
                statusLabel.text = "Nenhum TerrainSettings selecionado.";
                return;
            }

            try
            {
                Stopwatch watch = Stopwatch.StartNew();
                TerrainHeightSampler sampler = new TerrainHeightSampler(settings.ToParams());
                grid = HeightGrid.BakeDomain(sampler, samplesPerSide);
                HeightGridStats stats = grid.ComputeStats();
                watch.Stop();

                statusLabel.text =
                    "Domínio " + TerrainConstants.DomainSizeMeters.ToString("0") + " m, " + samplesPerSide + " x " + samplesPerSide +
                    " amostras (" + grid.MetersPerSample.ToString("0.0") + " m cada) em " + watch.ElapsedMilliseconds + " ms.\n" +
                    "Altura: mínima " + stats.MinHeight.ToString("0") + " m, máxima " + stats.MaxHeight.ToString("0") +
                    " m, média " + stats.MeanHeight.ToString("0") + " m.\n" +
                    "Área plana (< " + TerrainConstants.FlatMaxDegrees.ToString("0") + "°): " + stats.PercentFlat.ToString("0.0") +
                    "%. Área íngreme (> " + TerrainConstants.SteepMinDegrees.ToString("0") + "°): " + stats.PercentSteep.ToString("0.0") + "%.";
            }
            catch (System.Exception exception)
            {
                grid = null;
                statusLabel.text = "Erro ao gerar: " + exception.Message;
            }

            RebuildTexture();
        }

        void RebuildTexture()
        {
            if (image == null)
            {
                return;
            }

            if (grid == null)
            {
                DestroyTexture();
                image.image = null;
                return;
            }

            int width = grid.Width;
            int height = grid.Height;
            if (texture == null || texture.width != width || texture.height != height)
            {
                DestroyTexture();
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            Color32[] pixels = new Color32[width * height];
            float maxHeight = Mathf.Max(1f, grid.Heights.Length > 0 ? MaxOf(grid.Heights) : 1f);
            Vector3 light = new Vector3(-1f, 1.2f, 1f).normalized;

            for (int index = 0; index < pixels.Length; index++)
            {
                pixels[index] = ColorFor(index, maxHeight, light);
            }

            // A linha 0 da textura fica embaixo, igual ao eixo z crescendo para cima (norte para cima).
            texture.SetPixels32(pixels);
            texture.Apply(false);
            image.image = texture;
        }

        Color32 ColorFor(int index, float maxHeight, Vector3 light)
        {
            switch (mode)
            {
                case PreviewMode.Altura:
                {
                    float t = Mathf.Clamp01(grid.Heights[index] / maxHeight);
                    return Gray(t);
                }
                case PreviewMode.ZonasPlanas:
                    return Gray(grid.FlatMask[index]);
                case PreviewMode.Inclinacao:
                    return SlopeColor(grid.SlopeDegrees[index]);
                default:
                {
                    Vector3 normal = new Vector3(-grid.GradientX[index] * verticalExaggeration, 1f, -grid.GradientZ[index] * verticalExaggeration).normalized;
                    return Gray(Mathf.Clamp01(Vector3.Dot(normal, light)));
                }
            }
        }

        static Color32 Gray(float t)
        {
            byte v = (byte)Mathf.RoundToInt(Mathf.Clamp01(t) * 255f);
            return new Color32(v, v, v, 255);
        }

        // Verde (< 3°), amarelo (até 15°), laranja (até 30°), vermelho (acima).
        static Color32 SlopeColor(float degrees)
        {
            Color green = new Color(0.15f, 0.65f, 0.25f);
            Color yellow = new Color(0.95f, 0.9f, 0.3f);
            Color orange = new Color(0.95f, 0.55f, 0.15f);
            Color red = new Color(0.8f, 0.1f, 0.1f);

            Color c;
            if (degrees < TerrainConstants.FlatMaxDegrees)
            {
                c = green;
            }
            else if (degrees < 15f)
            {
                c = Color.Lerp(yellow, orange, (degrees - TerrainConstants.FlatMaxDegrees) / (15f - TerrainConstants.FlatMaxDegrees) * 0.5f);
            }
            else if (degrees < TerrainConstants.SteepMinDegrees)
            {
                c = Color.Lerp(orange, red, (degrees - 15f) / (TerrainConstants.SteepMinDegrees - 15f));
            }
            else
            {
                c = red;
            }

            return c;
        }

        static float MaxOf(float[] values)
        {
            float max = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] > max)
                {
                    max = values[i];
                }
            }

            return max;
        }

        void DestroyTexture()
        {
            if (texture != null)
            {
                DestroyImmediate(texture);
                texture = null;
            }
        }
    }
}
