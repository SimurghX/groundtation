using UnityEngine;
using UnityEngine.UI;
using GroundStation.Drone;

namespace GroundStation.DigitalTwin
{
    /// <summary>
    /// 3D Digital Twin: sahneyi (harita + drone) ikinci bir kamerayla RenderTexture'e cizer,
    /// paneldeki RawImage'da gosterir. Kamera drone'u arkadan-yukaridan takip eder.
    /// Bu script panel uzerinde olabilir; kamera sahne kokunde olusturulur.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DigitalTwin3DView : MonoBehaviour
    {
        [Header("3D Kamera")]
        [Tooltip("Atanmazsa runtime'da olusturulur. Sahnedeki harita ve drone'u gormeli.")]
        [SerializeField] private Camera twinCamera;
        [Tooltip("Kamera cozunurlugu. Atanmazsa 1024x768 olusturulur.")]
        [SerializeField] private RenderTexture renderTexture;
        [SerializeField] private int textureWidth = 1024;
        [SerializeField] private int textureHeight = 768;
        [Tooltip("Ana kamera ortografik olsa bile Twin kamerayi perspektifte zorlar.")]
        [SerializeField] private bool forcePerspectiveCamera = true;

        [Header("Goruntu hedefi")]
        [Tooltip("3D goruntunun gosterilecegi RawImage (genellikle TwinViewPort icinde).")]
        [SerializeField] private RawImage targetRawImage;

        [Header("Kamera konumu (drone takip)")]
        [Tooltip("Drone'un arkasinda ne kadar geride (XZ).")]
        [SerializeField] private float followDistance = 16f;
        [Tooltip("True ise kamera yuksekligi drone yuksekligiyle ayni olur.")]
        [SerializeField] private bool lockCameraHeightToDrone = true;
        [Tooltip("Yukseklik kilitliyken uygulanacak ek ofset (0 = tam ayni yukseklik).")]
        [SerializeField] private float followHeight = 0f;
        [SerializeField] private float smoothSpeed = 7f;

        [Header("Referanslar")]
        [SerializeField] private DroneWaypointFollower drone;
        [Tooltip("Digital Twin sadece 3D olsun diye 2D rota/ikon her zaman gizlenir.")]
        [SerializeField] private bool hide2DOverlayWhenActive = true;

        private Transform _droneTransform;
        private DigitalTwinRouteView _routeView;
        private DigitalTwinDroneView _droneView;
        private Vector3 _currentCameraPosition;
        private bool _initialized;

        private void Awake()
        {
            if (drone == null) drone = FindObjectOfType<DroneWaypointFollower>();
            if (drone != null) _droneTransform = drone.transform;
        }

        private void OnEnable()
        {
            EnsureCameraAndTexture();
            if (twinCamera != null)
            {
                if (forcePerspectiveCamera)
                {
                    twinCamera.orthographic = false;
                    twinCamera.fieldOfView = 46f;
                }
                twinCamera.enabled = true;
                if (targetRawImage != null && renderTexture != null)
                {
                    targetRawImage.texture = renderTexture;
                    targetRawImage.color = Color.white;
                    targetRawImage.raycastTarget = false;
                }
            }
            if (hide2DOverlayWhenActive)
                Set2DOverlayVisible(false);
        }

        private void OnDisable()
        {
            if (twinCamera != null)
                twinCamera.enabled = false;
            if (targetRawImage != null)
                targetRawImage.texture = null;
            if (hide2DOverlayWhenActive)
                Set2DOverlayVisible(true);
        }

        private void Set2DOverlayVisible(bool visible)
        {
            if (_routeView == null) _routeView = GetComponentInChildren<DigitalTwinRouteView>(true);
            if (_droneView == null) _droneView = GetComponentInChildren<DigitalTwinDroneView>(true);
            if (_routeView != null) _routeView.gameObject.SetActive(visible);
            if (_droneView != null) _droneView.gameObject.SetActive(visible);
        }

        private void LateUpdate()
        {
            if (twinCamera == null) return;

            if (_droneTransform == null)
            {
                SetFallbackCameraPosition();
                return;
            }

            Vector3 dronePos = _droneTransform.position;
            Vector3 forward = _droneTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 desiredPos = dronePos - forward * followDistance;
            desiredPos.y = lockCameraHeightToDrone ? (dronePos.y + followHeight) : desiredPos.y + followHeight;
            _currentCameraPosition = Vector3.Lerp(_currentCameraPosition, desiredPos, smoothSpeed * Time.deltaTime);
            twinCamera.transform.position = _currentCameraPosition;
            twinCamera.transform.LookAt(dronePos + Vector3.up * 5f);
        }

        private void SetFallbackCameraPosition()
        {
            if (twinCamera == null) return;
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                _currentCameraPosition = mainCam.transform.position;
                twinCamera.transform.position = _currentCameraPosition;
                twinCamera.transform.rotation = mainCam.transform.rotation;
            }
            else
            {
                _currentCameraPosition = new Vector3(0f, 150f, -150f);
                twinCamera.transform.position = _currentCameraPosition;
                twinCamera.transform.LookAt(Vector3.zero + Vector3.up * 20f);
            }
        }

        private void EnsureCameraAndTexture()
        {
            if (_initialized) return;

            if (drone == null) drone = FindObjectOfType<DroneWaypointFollower>();
            if (drone != null) _droneTransform = drone.transform;

            if (twinCamera == null)
                CreateTwinCamera();

            if (twinCamera != null && renderTexture == null)
            {
                renderTexture = new RenderTexture(textureWidth, textureHeight, 24);
                renderTexture.name = "DigitalTwin3D_RT";
                renderTexture.Create();
                twinCamera.targetTexture = renderTexture;
            }
            else if (twinCamera != null && renderTexture != null)
            {
                if (!renderTexture.IsCreated()) renderTexture.Create();
                twinCamera.targetTexture = renderTexture;
            }

            if (targetRawImage == null)
                ResolveRawImage();

            if (twinCamera != null)
            {
                if (_droneTransform != null)
                {
                    _currentCameraPosition = _droneTransform.position - _droneTransform.forward * followDistance;
                    _currentCameraPosition.y = _droneTransform.position.y + followHeight;
                }
                else
                    SetFallbackCameraPosition();
                twinCamera.transform.position = _currentCameraPosition;
                if (_droneTransform != null)
                    twinCamera.transform.LookAt(_droneTransform.position + Vector3.up * 5f);
                else if (Camera.main != null)
                    twinCamera.transform.rotation = Camera.main.transform.rotation;
            }

            _initialized = true;
        }

        private void CreateTwinCamera()
        {
            Camera mainCam = Camera.main;
            Transform parent = mainCam != null ? mainCam.transform.parent : null;
            if (parent == null)
                parent = transform.root;

            GameObject camGo = new GameObject("DigitalTwin3DCamera");
            camGo.transform.SetParent(parent);
            twinCamera = camGo.AddComponent<Camera>();
            Camera refCam = mainCam != null ? mainCam : Camera.main;
            if (refCam != null)
            {
                twinCamera.CopyFrom(refCam);
                twinCamera.depth = refCam.depth - 1;
                if (forcePerspectiveCamera)
                {
                    twinCamera.orthographic = false;
                    twinCamera.fieldOfView = 60f;
                }
                twinCamera.transform.position = refCam.transform.position;
                twinCamera.transform.rotation = refCam.transform.rotation;
                _currentCameraPosition = refCam.transform.position;
            }
            else
            {
                twinCamera.clearFlags = CameraClearFlags.Skybox;
                twinCamera.cullingMask = -1;
                twinCamera.depth = -2;
            }
            twinCamera.name = "DigitalTwin3DCamera";
            twinCamera.enabled = false;
        }

        private void ResolveRawImage()
        {
            Transform searchRoot = transform;
            for (int i = 0; i < 4 && searchRoot != null; i++)
            {
                var t = searchRoot.Find("TwinViewPort");
                if (t == null) t = searchRoot.Find("TwinViewport");
                if (t != null)
                {
                    var r = t.GetComponent<RawImage>();
                    if (r != null) { targetRawImage = r; return; }
                    r = t.GetComponentInChildren<RawImage>(true);
                    if (r != null) { targetRawImage = r; return; }
                    var go = new GameObject("Twin3DRawImage");
                    go.transform.SetParent(t, false);
                    var rectT = t as RectTransform;
                    if (rectT != null)
                    {
                        var rt = go.AddComponent<RectTransform>();
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                    }
                    targetRawImage = go.AddComponent<RawImage>();
                    targetRawImage.color = Color.white;
                    targetRawImage.raycastTarget = false;
                    go.transform.SetAsFirstSibling();
                    return;
                }
                searchRoot = searchRoot.parent;
            }
            var anyRaw = GetComponentInChildren<RawImage>(true);
            if (anyRaw != null) targetRawImage = anyRaw;
        }
    }
}
