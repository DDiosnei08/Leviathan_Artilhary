using ProjectLeviathan.Grass;
using ProjectLeviathan.ProceduralTerrain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectLeviathan.Base
{
    [DefaultExecutionOrder(200)]
    public sealed class BaseGameRig : MonoBehaviour
    {
        public enum View { Overview, Player, LeftCannon, RightCannon, Map }
        public BaseSpawner spawner;
        public TerrainWorldController terrain;
        public GrassManager grass;
        public LeviathanPlayerController player;
        public Camera overviewCamera, playerCamera, stationCamera;
        public CommandRoomInteractor playerInteractor, stationInteractor;
        public View initialView = View.Overview;
        public float stationDistance = 3f;
        public View CurrentView { get; private set; }
        BaseAssembly assembly;
        CharacterController character;
        Quaternion previousPlatformRotation;
        Vector3 previousPlatformPosition;
        bool cursorReleased;
        void OnEnable() { if(Application.isPlaying) Initialize(); }
        void Start() => Initialize();
        void Initialize()
        {
            character=player.GetComponent<CharacterController>(); spawner.Generated-=Bind;spawner.Generated+=Bind;
            Bind(spawner); SetView(initialView);
        }
        void Bind(BaseSpawner source)
        {
            if (source.Instance==null) return;
            if (stationInteractor!=null) stationInteractor.CancelInteraction();
            assembly=source.Instance.GetComponent<BaseAssembly>();
            character.enabled=false;
            player.transform.SetPositionAndRotation(assembly.playerSpawn.position,assembly.playerSpawn.rotation);
            character.enabled=true;
            spawner.player=player.transform;
            previousPlatformRotation=assembly.platform.transform.rotation; previousPlatformPosition=assembly.platform.transform.position;
            overviewCamera.transform.SetParent(transform,false);
            overviewCamera.transform.position=source.Instance.transform.TransformPoint(new Vector3(-43,31,-42));
            overviewCamera.transform.LookAt(source.Instance.transform.position+Vector3.up*4);
            SetView(CurrentView);
        }
        void Update()
        {
            if (assembly==null) return;
            CarryPlayer();
            var keys=Keyboard.current;
            if (keys!=null)
            {
                if (keys.tabKey.wasPressedThisFrame) SetView(CurrentView==View.Overview ? View.Player : View.Overview);
                if (keys.eKey.wasPressedThisFrame)
                {
                    if (CurrentView!=View.Player && CurrentView!=View.Overview) SetView(View.Player);
                    else if (CurrentView==View.Player)
                    {
                        Transform closest=null; View selected=View.Player; float best=stationDistance;
                        Find(assembly.leftStation,View.LeftCannon,ref closest,ref selected,ref best);
                        Find(assembly.rightStation,View.RightCannon,ref closest,ref selected,ref best);
                        Find(assembly.mapStation,View.Map,ref closest,ref selected,ref best);
                        if (closest!=null) SetView(selected);
                    }
                }
                if (keys.escapeKey.wasPressedThisFrame && CurrentView==View.Player) { cursorReleased=!cursorReleased; ApplyInput(); }
            }
            if (CurrentView==View.Player && cursorReleased && Mouse.current!=null && Mouse.current.leftButton.wasPressedThisFrame)
            { cursorReleased=false; ApplyInput(); }
        }
        void Find(Transform station,View view,ref Transform closest,ref View chosen,ref float best)
        {
            float distance=Vector3.Distance(player.transform.position,station.position);
            if (distance>=best) return; best=distance; closest=station; chosen=view;
        }
        void CarryPlayer()
        {
            Transform pivot=assembly.platform.transform;
            // Only carry a grounded operator actually standing on this moving platform.
            if (character.enabled && character.isGrounded && Physics.Raycast(player.transform.position+Vector3.up*.2f,Vector3.down,out var hit,2.3f)
                && hit.transform.IsChildOf(pivot))
            {
                Vector3 local=Quaternion.Inverse(previousPlatformRotation)*(player.transform.position-previousPlatformPosition);
                Vector3 target=pivot.position+pivot.rotation*local;
                character.Move(target-player.transform.position);
                player.transform.rotation=pivot.rotation*Quaternion.Inverse(previousPlatformRotation)*player.transform.rotation;
            }
            previousPlatformRotation=pivot.rotation; previousPlatformPosition=pivot.position;
        }
        public void SetView(View view)
        {
            playerInteractor.CancelInteraction(); stationInteractor.CancelInteraction(); CurrentView=view; cursorReleased=false;
            if (assembly!=null && view!=View.Overview && view!=View.Player)
            {
                Transform station=view==View.Map ? assembly.mapStation : view==View.LeftCannon ? assembly.leftStation : assembly.rightStation;
                stationCamera.transform.SetParent(transform,false);
                AlignStationCamera(station);
            }
            Camera active=view==View.Overview ? overviewCamera : view==View.Player ? playerCamera : stationCamera;
            Select(overviewCamera,active); Select(playerCamera,active); Select(stationCamera,active);
            terrain.viewer=active.transform;
            if (grass!=null) grass.renderCamera=active;
            ApplyInput();
        }
        void LateUpdate()
        {
            if(assembly==null || CurrentView==View.Overview || CurrentView==View.Player) return;
            Transform station=CurrentView==View.Map ? assembly.mapStation : CurrentView==View.LeftCannon ? assembly.leftStation : assembly.rightStation;
            AlignStationCamera(station);
        }
        void AlignStationCamera(Transform station)
        {
            stationCamera.transform.position=station.position+station.up*1.65f;
            stationCamera.transform.rotation=station.rotation*Quaternion.Euler(CurrentView==View.Map ? 22 : 35,0,0);
            stationCamera.fieldOfView=CurrentView==View.Map ? 80 : 65;
        }
        void ApplyInput()
        {
            bool walking=CurrentView==View.Player && !cursorReleased;
            player.enabled=walking; playerInteractor.enabled=walking;
            stationInteractor.enabled=CurrentView!=View.Player && CurrentView!=View.Overview;
            Cursor.lockState=walking ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible=!walking;
        }
        static void Select(Camera camera,Camera active)
        {
            camera.enabled=camera==active; camera.tag=camera==active ? "MainCamera" : "Untagged";
            if (camera.TryGetComponent(out AudioListener listener)) listener.enabled=camera==active;
        }
        void OnDisable()
        {
            if (spawner!=null) spawner.Generated-=Bind;
            if(playerInteractor!=null) playerInteractor.CancelInteraction();
            if(stationInteractor!=null) stationInteractor.CancelInteraction();
            Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
        }
        void OnDestroy() => OnDisable();
    }
}
