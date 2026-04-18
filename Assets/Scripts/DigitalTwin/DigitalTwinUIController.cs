using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Mapbox.Unity.Map;
using GroundStation.Routes;

namespace GroundStation.DigitalTwin
{
    public class DigitalTwinUIController : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject digitalTwinPanel;
        [SerializeField] private DigitalTwinMap3DMode map3DMode;
        [Tooltip("Twin 3D goruntusu haritayi cekmek icin harita sahnede kalmali; gizlenirse ikiz ekrani bos gorunur. Istersen burada Map objesini atayip 'Twin acikken gizle'yi acarsin.")]
        [SerializeField] private GameObject mapViewRoot;
        [Tooltip("Acilirsa twin acikken mapViewRoot gizlenir. Kapali birakirsan harita kalir ve Digital Twin 3D goruntuyu alir.")]
        [SerializeField] private bool hideMapWhenTwinOpen = false;

        [Header("Buton (opsiyonel - gorsel secili durumu icin)")]
        [SerializeField] private Button digitalTwinButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject buttonHighlightWhenOpen;

        private bool _isOpen;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (map3DMode == null)
                map3DMode = FindObjectOfType<DigitalTwinMap3DMode>();
            if (map3DMode == null)
            {
                var map = FindObjectOfType<AbstractMap>();
                if (map != null)
                    map3DMode = map.gameObject.AddComponent<DigitalTwinMap3DMode>();
            }
            if (digitalTwinPanel == null)
            {
                var go = GameObject.Find("DigitalTwinPanel");
                if (go != null) digitalTwinPanel = go;
            }
            ResolveCloseButtonIfNeeded();
            if (digitalTwinPanel != null)
                digitalTwinPanel.SetActive(false);
            _isOpen = false;
            if (buttonHighlightWhenOpen != null)
                buttonHighlightWhenOpen.SetActive(false);
        }

        private void OnEnable()
        {
            if (digitalTwinButton != null)
                digitalTwinButton.onClick.AddListener(OnDigitalTwinButtonClicked);
            ResolveCloseButtonIfNeeded();
            if (closeButton != null)
                closeButton.onClick.AddListener(OnCloseClicked);
        }

        private void OnDisable()
        {
            if (digitalTwinButton != null)
                digitalTwinButton.onClick.RemoveListener(OnDigitalTwinButtonClicked);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        private void OnDigitalTwinButtonClicked()
        {
            ToggleView();
        }

        private void OnCloseClicked()
        {
            SetViewOpen(false);
        }

        public void ToggleView()
        {
            SetViewOpen(!_isOpen);
        }

        public void SetViewOpen(bool open)
        {
            _isOpen = open;

            if (digitalTwinPanel == null)
            {
                var go = GameObject.Find("DigitalTwinPanel");
                if (go != null) digitalTwinPanel = go;
                else
                {
                    foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                    {
                        foreach (Transform c in root.GetComponentsInChildren<Transform>(true))
                        {
                            if (c.name == "DigitalTwinPanel") { digitalTwinPanel = c.gameObject; break; }
                        }
                        if (digitalTwinPanel != null) break;
                    }
                }
                if (digitalTwinPanel == null) digitalTwinPanel = gameObject;
            }
            if (digitalTwinPanel != null)
                digitalTwinPanel.SetActive(_isOpen);
            ResolveCloseButtonIfNeeded();
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnCloseClicked);
                closeButton.onClick.AddListener(OnCloseClicked);
            }

            if (map3DMode != null)
            {
                if (_isOpen) map3DMode.Enable3DForTwin();
                else map3DMode.Disable3DForTwin();
            }

            if (mapViewRoot != null && hideMapWhenTwinOpen)
                mapViewRoot.SetActive(!_isOpen);

            if (buttonHighlightWhenOpen != null)
                buttonHighlightWhenOpen.SetActive(_isOpen);

            if (!_isOpen)
                RestoreMainRouteVisuals();
        }

        private void ResolveCloseButtonIfNeeded()
        {
            if (closeButton != null) return;
            if (digitalTwinPanel == null) return;

            Button fallback = null;
            foreach (var b in digitalTwinPanel.GetComponentsInChildren<Button>(true))
            {
                string n = b.gameObject.name.ToLowerInvariant();
                var txt = b.GetComponentInChildren<Text>(true);
                string t = txt != null ? txt.text.ToLowerInvariant() : string.Empty;
                if (n.Contains("close") || n.Contains("kapat") || n == "x" || t == "x" || t.Contains("close") || t.Contains("kapat"))
                {
                    closeButton = b;
                    break;
                }
                if (fallback == null && b != digitalTwinButton)
                    fallback = b;
            }
            if (closeButton == null)
                closeButton = fallback;
        }

        private void RestoreMainRouteVisuals()
        {
            var routeVisualizer = FindObjectOfType<RouteVisualizer>(true);
            if (routeVisualizer != null)
            {
                routeVisualizer.gameObject.SetActive(true);
                routeVisualizer.RefreshVisual();
            }

            var markerVisualizer = FindObjectOfType<RouteMarkerVisualizer>(true);
            if (markerVisualizer != null)
            {
                markerVisualizer.gameObject.SetActive(true);
                markerVisualizer.RefreshMarkers();
            }
        }
    }
}
