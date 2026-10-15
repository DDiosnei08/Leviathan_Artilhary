using System.Collections;
using UnityEngine;

namespace ProjectLeviathan.Gameplay
{
    /// <summary>
    /// Assenta este objeto no terreno procedural ao iniciar (o terreno só tem colisor depois de gerado, então
    /// tenta por alguns segundos) e, opcionalmente, posiciona uma câmera de observação perto dele.
    /// </summary>
    public class TerrainPlacer : MonoBehaviour
    {
        [Header("Sondagem do solo")]
        [SerializeField] float probeStartHeight = 2000f;
        [SerializeField] float probeDistance = 3000f;
        [SerializeField] float retryIntervalSeconds = 0.25f;
        [SerializeField] float maxWaitSeconds = 15f;
        [SerializeField] float groundOffset = 0f;

        [Header("Câmera de observação (opcional)")]
        [SerializeField] Transform cameraToFrame;
        [Tooltip("Deslocamento da câmera em relação ao objeto, no espaço local dele (x direita, y cima, z frente).")]
        [SerializeField] Vector3 cameraLocalOffset = new Vector3(-9f, 6f, -12f);

        public bool IsPlaced { get; private set; }

        public void ConfigureCamera(Transform cameraTransform, Vector3 localOffset)
        {
            cameraToFrame = cameraTransform;
            cameraLocalOffset = localOffset;
        }

        IEnumerator Start()
        {
            float waited = 0f;
            while (waited <= maxWaitSeconds)
            {
                if (TryPlace())
                {
                    yield break;
                }

                yield return new WaitForSeconds(retryIntervalSeconds);
                waited += retryIntervalSeconds;
            }

            Debug.LogWarning(
                $"'{name}' não achou o terreno em {maxWaitSeconds:F0} s (sem colisor sob x={transform.position.x:F0}, " +
                $"z={transform.position.z:F0}). O ponto pode estar fora do mapa ou fora da janela de chunks carregada.",
                this);
        }

        bool TryPlace()
        {
            Vector3 position = transform.position;
            Vector3 origin = new Vector3(position.x, probeStartHeight, position.z);
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, probeDistance))
            {
                return false;
            }

            transform.position = hit.point + Vector3.up * groundOffset;
            IsPlaced = true;
            FrameCamera();
            return true;
        }

        void FrameCamera()
        {
            if (cameraToFrame == null)
            {
                return;
            }

            cameraToFrame.position = transform.TransformPoint(cameraLocalOffset);
            Vector3 lookTarget = transform.position + Vector3.up * 2.5f;
            cameraToFrame.rotation = Quaternion.LookRotation(lookTarget - cameraToFrame.position, Vector3.up);
        }
    }
}
