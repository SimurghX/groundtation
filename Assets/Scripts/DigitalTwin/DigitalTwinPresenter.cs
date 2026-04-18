using UnityEngine;
using UnityEngine.UI;
using GroundStation.Drone;
using GroundStation.Routes;

namespace GroundStation.DigitalTwin
{
    /// <summary>
    /// Tek kaynaktan (DroneWaypointFollower + RouteManager) veri okur; Digital Twin view ve telemetry'yi gunceller.
    /// Panel acikken her frame calisir.
    /// </summary>
    public class DigitalTwinPresenter : MonoBehaviour
    {
        [Header("Data source")]
        [SerializeField] private DroneWaypointFollower drone;
        [SerializeField] private RouteManager routeManager;
        [SerializeField] private DroneTelemetryUI mainTelemetryUI;
        [SerializeField] private bool mirrorMainTelemetryPanelFormat = true;
        [Tooltip("JSON koprusu telemetri yazdiginda twin panelde bunu onceliklendir.")]
        [SerializeField] private DigitalTwinRemoteState jsonTelemetryOverride;

        [Header("View")]
        [SerializeField] private RectTransform twinViewport;
        [SerializeField] private DigitalTwinDroneView droneView;
        [SerializeField] private DigitalTwinRouteView routeView;

        [Header("Twin panel telemetry (opsiyonel)")]
        [SerializeField] private Text altitudeText;
        [SerializeField] private Text speedText;
        [SerializeField] private Text modeText;
        [SerializeField] private Text waypointIndexText;
        [SerializeField] private Text flightDurationText;
        [SerializeField] private Text fpsText;
        [SerializeField] private Text missionText;
        [SerializeField] private Text meshStatusText;
        [SerializeField] private Text warningText;
        [SerializeField] private RectTransform twinTelemetryRoot;

        [Header("World bounds (XZ)")]
        [Tooltip("Rota bosken kullanilacak alan yari capi (metre).")]
        [SerializeField] private float defaultBoundsRadius = 100f;
        [SerializeField] private float boundsPadding = 20f;

        private float _minX, _maxX, _minZ, _maxZ;
        private bool _boundsDirty = true;

        private bool _telemetryResolved;
        private bool _viewRefsResolved;
        private bool _viewportBackgroundEnsured;
        private float _nextSourceResolveTime;
        private bool _mirroredLayoutOnce;

        private void Awake()
        {
            if (drone == null) drone = FindObjectOfType<DroneWaypointFollower>();
            if (routeManager == null) routeManager = FindObjectOfType<RouteManager>();
            if (mainTelemetryUI == null) mainTelemetryUI = FindObjectOfType<DroneTelemetryUI>();
            ResolveViewRefsIfNeeded();
        }

        private void Start()
        {
            ResolveViewRefsIfNeeded();
            ResolveTelemetryRefsIfNeeded();
        }

        /// <summary>
        /// Twin Viewport, Drone View, Route View atanmamissa panel icinde isimle bulur.
        /// </summary>
        private void ResolveViewRefsIfNeeded()
        {
            if (_viewRefsResolved && twinViewport != null && droneView != null && routeView != null)
                return;

            if (twinViewport == null)
            {
                Transform searchRoot = transform;
                for (int attempt = 0; attempt < 3 && searchRoot != null; attempt++)
                {
                    var t = searchRoot.Find("TwinViewPort");
                    if (t == null) t = searchRoot.Find("TwinViewport");
                    if (t == null)
                    {
                        foreach (Transform c in searchRoot)
                        {
                            if (c.name.ToLowerInvariant().Contains("twinview"))
                            {
                                t = c;
                                break;
                            }
                        }
                    }
                    if (t != null)
                    {
                        twinViewport = t as RectTransform ?? t.GetComponent<RectTransform>();
                        break;
                    }
                    searchRoot = searchRoot.parent;
                }
            }

            if (twinViewport != null)
            {
                if (droneView == null)
                    droneView = twinViewport.GetComponentInChildren<DigitalTwinDroneView>(true);
                if (routeView == null)
                    routeView = twinViewport.GetComponent<DigitalTwinRouteView>();
                if (routeView == null)
                    routeView = twinViewport.GetComponentInChildren<DigitalTwinRouteView>(true);
            }

            _viewRefsResolved = true;
        }

        /// <summary>
        /// Inspector'da atanmamissa panel icindeki tum Text'leri tarayip isimle eslestirir (kapali objeler dahil).
        /// </summary>
        private void ResolveTelemetryRefsIfNeeded()
        {
            if (_telemetryResolved) return;
            if (altitudeText != null && speedText != null && modeText != null && waypointIndexText != null)
            {
                _telemetryResolved = true;
                return;
            }

            Transform root = (twinViewport != null && twinViewport.parent != null) ? twinViewport.parent : transform;
            var texts = root.GetComponentsInChildren<Text>(true);
            foreach (var text in texts)
            {
                string n = text.gameObject.name.ToLowerInvariant();
                if (altitudeText == null && (n.Contains("altitude") || n.Contains("yukseklik")))
                    altitudeText = text;
                else if (speedText == null && (n.Contains("speed") || n.Contains("hiz")))
                    speedText = text;
                else if (modeText == null && n.Contains("mode"))
                    modeText = text;
                else if (waypointIndexText == null && (n.Contains("wpindex") || n.Contains("waypoint") || n.Contains("wp index")))
                    waypointIndexText = text;
                else if (flightDurationText == null && (n.Contains("flight") || n.Contains("ucus") || n.Contains("sure")))
                    flightDurationText = text;
                else if (fpsText == null && n.Contains("fps"))
                    fpsText = text;
                else if (missionText == null && (n.Contains("mission") || n.Contains("faz") || n.Contains("phase")))
                    missionText = text;
                else if (meshStatusText == null && (n.Contains("mesh") || n.Contains("link") || n.Contains("signal")))
                    meshStatusText = text;
                else if (warningText == null && (n.Contains("warn") || n.Contains("uyari") || n.Contains("alert")))
                    warningText = text;
            }
            _telemetryResolved = (altitudeText != null || speedText != null || modeText != null || waypointIndexText != null || flightDurationText != null || fpsText != null);
        }

        /// <summary>
        /// Viewport tamamen siyah kalmasin diye bir kez arka plan rengini koyu gri yapar (veya Image ekler).
        /// </summary>
        private void EnsureViewportVisibleBackground()
        {
            if (_viewportBackgroundEnsured || twinViewport == null) return;

            var img = twinViewport.GetComponent<Image>();
            if (img == null)
                img = twinViewport.gameObject.AddComponent<Image>();
            if (img != null)
            {
                // Siyah degil, koyu gri-mavi; rota ve drone ikonu ustte gorunur
                if (img.color.r < 0.1f && img.color.g < 0.1f && img.color.b < 0.1f)
                    img.color = new Color(0.12f, 0.12f, 0.18f, 1f);
            }
            _viewportBackgroundEnsured = true;
        }

        private void OnEnable()
        {
            if (routeManager != null)
                routeManager.OnRouteChanged += OnRouteChanged;
            _mirroredLayoutOnce = false;
        }

        private void OnDisable()
        {
            if (routeManager != null)
                routeManager.OnRouteChanged -= OnRouteChanged;
        }

        private void OnRouteChanged(RouteData _)
        {
            _boundsDirty = true;
        }

        private void LateUpdate()
        {
            EnsureDataSources();
            var twin3D = GetComponentInChildren<DigitalTwin3DView>(true);
            bool use3DOnly = twin3D != null && twin3D.enabled;
            if (use3DOnly)
            {
                ResolveTelemetryRefsIfNeeded();
                UpdateTelemetry();
                return;
            }
            ResolveViewRefsIfNeeded();
            if (twinViewport == null)
                return;
            if (!twinViewport.gameObject.activeInHierarchy)
                return;

            EnsureViewportVisibleBackground();
            RefreshBounds();
            UpdateDroneView();
            UpdateRouteView();
            UpdateTelemetry();
        }

        private void EnsureDataSources()
        {
            if (Time.unscaledTime < _nextSourceResolveTime) return;
            _nextSourceResolveTime = Time.unscaledTime + 1f;
            if (drone == null) drone = FindObjectOfType<DroneWaypointFollower>();
            if (routeManager == null) routeManager = FindObjectOfType<RouteManager>();
            if (mainTelemetryUI == null) mainTelemetryUI = FindObjectOfType<DroneTelemetryUI>();
            if (jsonTelemetryOverride == null) jsonTelemetryOverride = FindObjectOfType<DigitalTwinRemoteState>();
            if (twinTelemetryRoot == null && altitudeText != null)
                twinTelemetryRoot = altitudeText.transform.parent as RectTransform;
        }

        private void RefreshBounds()
        {
            if (!_boundsDirty && routeManager == null)
                return;

            float cx = 0f, cz = 0f;
            bool hasAny = false;

            if (drone != null)
            {
                var p = drone.transform.position;
                cx = p.x; cz = p.z;
                _minX = p.x - defaultBoundsRadius;
                _maxX = p.x + defaultBoundsRadius;
                _minZ = p.z - defaultBoundsRadius;
                _maxZ = p.z + defaultBoundsRadius;
                hasAny = true;
            }

            var data = routeManager != null ? routeManager.GetRouteData() : null;
            if (data != null && data.waypoints != null && data.waypoints.Count > 0)
            {
                for (int i = 0; i < data.waypoints.Count; i++)
                {
                    var wp = data.waypoints[i];
                    float x = wp.worldPosition.x, z = wp.worldPosition.z;
                    if (!hasAny)
                    {
                        _minX = x; _maxX = x; _minZ = z; _maxZ = z;
                        hasAny = true;
                    }
                    else
                    {
                        if (x < _minX) _minX = x;
                        if (x > _maxX) _maxX = x;
                        if (z < _minZ) _minZ = z;
                        if (z > _maxZ) _maxZ = z;
                    }
                }
            }

            if (!hasAny)
            {
                _minX = cx - defaultBoundsRadius;
                _maxX = cx + defaultBoundsRadius;
                _minZ = cz - defaultBoundsRadius;
                _maxZ = cz + defaultBoundsRadius;
            }

            float pad = boundsPadding;
            _minX -= pad; _maxX += pad; _minZ -= pad; _maxZ += pad;
            float rangeX = _maxX - _minX;
            float rangeZ = _maxZ - _minZ;
            if (rangeX < 1f) { _minX -= 0.5f; _maxX += 0.5f; }
            if (rangeZ < 1f) { _minZ -= 0.5f; _maxZ += 0.5f; }
            _boundsDirty = false;
        }

        private Vector2 WorldXZToViewport(Vector3 worldPos)
        {
            float rangeX = _maxX - _minX;
            float rangeZ = _maxZ - _minZ;
            if (rangeX < 0.0001f) rangeX = 1f;
            if (rangeZ < 0.0001f) rangeZ = 1f;
            float nx = (worldPos.x - _minX) / rangeX;
            float nz = (worldPos.z - _minZ) / rangeZ;
            var rect = twinViewport.rect;
            float w = rect.width;
            float h = rect.height;
            if (w < 50f) w = 800f;
            if (h < 50f) h = 600f;
            float localX = (nx - 0.5f) * w;
            float localZ = (nz - 0.5f) * h;
            return new Vector2(localX, localZ);
        }

        private void UpdateDroneView()
        {
            if (droneView == null || drone == null) return;

            Vector2 viewPos = WorldXZToViewport(drone.transform.position);
            float headingDeg = drone.transform.eulerAngles.y;
            droneView.SetPosition(viewPos);
            droneView.SetRotation(headingDeg);
        }

        private void UpdateRouteView()
        {
            if (routeView == null) return;

            var data = routeManager != null ? routeManager.GetRouteData() : null;
            if (data == null || data.waypoints == null || data.waypoints.Count == 0)
            {
                routeView.SetWaypoints(null);
                return;
            }

            var viewPoints = new Vector2[data.waypoints.Count];
            for (int i = 0; i < data.waypoints.Count; i++)
                viewPoints[i] = WorldXZToViewport(data.waypoints[i].worldPosition);
            routeView.SetWaypoints(viewPoints);
        }

        private void UpdateTelemetry()
        {
            ResolveTelemetryRefsIfNeeded();
            if (!_telemetryResolved) ResolveTelemetryRefsIfNeeded();
            if (drone == null) return;

            if (jsonTelemetryOverride != null && jsonTelemetryOverride.UseJsonTelemetry)
            {
                if (altitudeText != null && !string.IsNullOrEmpty(jsonTelemetryOverride.LastAltitudeText))
                    altitudeText.text = jsonTelemetryOverride.LastAltitudeText;
                if (speedText != null && !string.IsNullOrEmpty(jsonTelemetryOverride.LastSpeedText))
                    speedText.text = jsonTelemetryOverride.LastSpeedText;
                if (modeText != null && !string.IsNullOrEmpty(jsonTelemetryOverride.LastModeText))
                {
                    string mode = jsonTelemetryOverride.LastModeText;
                    if (!string.IsNullOrEmpty(jsonTelemetryOverride.LastSourceId))
                        mode += " [" + jsonTelemetryOverride.LastSourceId + "]";
                    modeText.text = mode;
                }
                if (waypointIndexText != null && !string.IsNullOrEmpty(jsonTelemetryOverride.LastWaypointText))
                    waypointIndexText.text = jsonTelemetryOverride.LastWaypointText;
                if (missionText != null && !string.IsNullOrEmpty(jsonTelemetryOverride.LastMissionText))
                    missionText.text = jsonTelemetryOverride.LastMissionText;
                if (meshStatusText != null && !string.IsNullOrEmpty(jsonTelemetryOverride.LastMeshText))
                    meshStatusText.text = jsonTelemetryOverride.LastMeshText;
                if (warningText != null)
                {
                    string counters = string.Format("Objeler: Engeller {0} | Hedefler {1}",
                        jsonTelemetryOverride.OperationalState.obstacleCount,
                        jsonTelemetryOverride.OperationalState.targetCount);
                    string voxels = string.Format("Voxel: {0}", jsonTelemetryOverride.OperationalState.voxelCount);
                    string link = jsonTelemetryOverride.LastVehicleStatusText;
                    if (string.IsNullOrEmpty(jsonTelemetryOverride.LastWarningText))
                        warningText.text = string.IsNullOrEmpty(link) ? counters + " | " + voxels : link + " | " + counters + " | " + voxels;
                    else
                        warningText.text = string.IsNullOrEmpty(link)
                            ? jsonTelemetryOverride.LastWarningText
                            : link + " | " + jsonTelemetryOverride.LastWarningText;
                }
                if (flightDurationText != null && mainTelemetryUI != null && !string.IsNullOrEmpty(mainTelemetryUI.LastFlightDurationText))
                    flightDurationText.text = mainTelemetryUI.LastFlightDurationText;
                else if (flightDurationText != null)
                {
                    float sec = drone.FlightDurationSeconds;
                    int m = Mathf.FloorToInt(sec / 60f);
                    int s = Mathf.FloorToInt(sec % 60f);
                    flightDurationText.text = string.Format("U\u00E7u\u015F: {0:D2}:{1:D2}", m, s);
                }
                if (fpsText != null)
                {
                    if (mainTelemetryUI != null && !string.IsNullOrEmpty(mainTelemetryUI.LastFpsText))
                        fpsText.text = mainTelemetryUI.LastFpsText;
                    else
                        fpsText.text = string.Format("FPS: {0:F0}", 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime));
                }
                return;
            }

            if (mirrorMainTelemetryPanelFormat && mainTelemetryUI != null)
            {
                MirrorTelemetryLayoutFromMainIfNeeded();
                if (altitudeText != null && !string.IsNullOrEmpty(mainTelemetryUI.LastAltitudeText))
                    altitudeText.text = mainTelemetryUI.LastAltitudeText;
                if (speedText != null && !string.IsNullOrEmpty(mainTelemetryUI.LastSpeedText))
                    speedText.text = mainTelemetryUI.LastSpeedText;
                if (modeText != null && !string.IsNullOrEmpty(mainTelemetryUI.LastModeText))
                    modeText.text = mainTelemetryUI.LastModeText;
                if (waypointIndexText != null && !string.IsNullOrEmpty(mainTelemetryUI.LastWaypointText))
                    waypointIndexText.text = mainTelemetryUI.LastWaypointText;
                if (missionText != null)
                    missionText.text = "";
                if (meshStatusText != null)
                    meshStatusText.text = "";
                if (warningText != null)
                    warningText.text = "";
                if (flightDurationText != null)
                {
                    if (!string.IsNullOrEmpty(mainTelemetryUI.LastFlightDurationText))
                        flightDurationText.text = mainTelemetryUI.LastFlightDurationText;
                    else
                    {
                        float sec = drone.FlightDurationSeconds;
                        int m = Mathf.FloorToInt(sec / 60f);
                        int s = Mathf.FloorToInt(sec % 60f);
                        flightDurationText.text = string.Format("Uçuş: {0:D2}:{1:D2}", m, s);
                    }
                }
                if (fpsText != null)
                {
                    if (!string.IsNullOrEmpty(mainTelemetryUI.LastFpsText))
                        fpsText.text = mainTelemetryUI.LastFpsText;
                    else
                        fpsText.text = string.Format("FPS: {0:F0}", 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime));
                }
                return;
            }

            float alt = drone.transform.position.y;
            float speed = drone.CurrentSpeed;
            int wpIndex = drone.CurrentWaypointIndex;
            bool running = drone.IsRunning;

            if (altitudeText != null)
                altitudeText.text = string.Format("Y\u00FCkseklik: {0:F1} m", alt);
            if (speedText != null)
                speedText.text = string.Format("H\u0131z: {0:F1} m/s", speed);
            if (modeText != null)
                modeText.text = running ? "Mod: GUIDED (Route)" : "Mod: IDLE";
            if (waypointIndexText != null)
                waypointIndexText.text = string.Format("WP Index: {0}", wpIndex);
            if (missionText != null)
                missionText.text = "";
            if (meshStatusText != null)
                meshStatusText.text = "";
            if (warningText != null)
                warningText.text = "";
            if (flightDurationText != null)
            {
                float sec = drone.FlightDurationSeconds;
                int m = Mathf.FloorToInt(sec / 60f);
                int s = Mathf.FloorToInt(sec % 60f);
                flightDurationText.text = string.Format("Uçuş: {0:D2}:{1:D2}", m, s);
            }
            if (fpsText != null)
                fpsText.text = string.Format("FPS: {0:F0}", 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime));
        }

        private void MirrorTelemetryLayoutFromMainIfNeeded()
        {
            if (_mirroredLayoutOnce || mainTelemetryUI == null) return;
            ResolveTwinTelemetryRootIfNeeded();

            CopyTextRectAndStyle(altitudeText, mainTelemetryUI.AltitudeTextUI);
            CopyTextRectAndStyle(speedText, mainTelemetryUI.SpeedTextUI);
            CopyTextRectAndStyle(modeText, mainTelemetryUI.ModeTextUI);
            CopyTextRectAndStyle(waypointIndexText, mainTelemetryUI.WaypointIndexTextUI);
            CopyTextRectAndStyle(flightDurationText, mainTelemetryUI.FlightDurationTextUI);
            CopyTextRectAndStyle(fpsText, mainTelemetryUI.FpsTextUI);

            if (twinTelemetryRoot != null && mainTelemetryUI.AltitudeTextUI != null)
            {
                var mainRoot = mainTelemetryUI.AltitudeTextUI.transform.parent as RectTransform;
                if (mainRoot != null)
                {
                    twinTelemetryRoot.anchorMin = mainRoot.anchorMin;
                    twinTelemetryRoot.anchorMax = mainRoot.anchorMax;
                    twinTelemetryRoot.pivot = mainRoot.pivot;
                    twinTelemetryRoot.anchoredPosition = mainRoot.anchoredPosition;
                    twinTelemetryRoot.sizeDelta = mainRoot.sizeDelta;
                }
            }

            _mirroredLayoutOnce = true;
        }

        private void ResolveTwinTelemetryRootIfNeeded()
        {
            if (twinTelemetryRoot != null) return;
            if (altitudeText != null)
                twinTelemetryRoot = altitudeText.transform.parent as RectTransform;
            if (twinTelemetryRoot != null) return;

            Transform root = (twinViewport != null && twinViewport.parent != null) ? twinViewport.parent : transform;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("telemetry") || n.Contains("hud"))
                {
                    twinTelemetryRoot = t as RectTransform;
                    if (twinTelemetryRoot != null) break;
                }
            }
        }

        private static void CopyTextRectAndStyle(Text target, Text source)
        {
            if (target == null || source == null) return;
            target.fontSize = source.fontSize;
            target.fontStyle = source.fontStyle;
            target.alignment = source.alignment;
            target.color = source.color;
            target.horizontalOverflow = source.horizontalOverflow;
            target.verticalOverflow = source.verticalOverflow;

            var tr = target.rectTransform;
            var sr = source.rectTransform;
            if (tr != null && sr != null)
            {
                tr.anchorMin = sr.anchorMin;
                tr.anchorMax = sr.anchorMax;
                tr.pivot = sr.pivot;
                tr.anchoredPosition = sr.anchoredPosition;
                tr.sizeDelta = sr.sizeDelta;
            }
        }
    }
}
