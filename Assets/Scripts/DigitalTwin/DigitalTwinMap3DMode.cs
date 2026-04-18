using UnityEngine;
using Mapbox.Unity.Map;
using GroundStation.Map;
using System.Collections;

namespace GroundStation.DigitalTwin
{
    /// <summary>
    /// Digital Twin acikken haritayi 3D moda alir (terrain + bina).
    /// Kapaninca onceki ayarlari geri yukler.
    /// </summary>
    public class DigitalTwinMap3DMode : MonoBehaviour
    {
        [SerializeField] private AbstractMap abstractMap;
        [SerializeField] private MapboxBuildingsEnabler buildingsEnabler;

        [Header("3D Mode Settings")]
        [SerializeField] private bool useSatelliteStreetsInTwin = true;
        [SerializeField] private float terrainExaggeration = 2.5f;
        [SerializeField] private bool useBuildingsInTwin = false;
        [SerializeField] private bool forceDisableBuildingsForStability = true;
        [SerializeField] private int reinforceFrames = 4;
        [Tooltip("Kapanista map ayarlarini eskiye dondur. Hata aliyorsan kapali tut.")]
        [SerializeField] private bool restoreMapStateOnClose = false;

        private bool _cached;
        private bool _is3DModeActive;
        private ElevationSourceType _prevTerrainSource;
        private ElevationLayerType _prevTerrainType;
        private float _prevExaggeration;
        private ImagerySourceType _prevImagerySource;

        private void Awake()
        {
            if (abstractMap == null) abstractMap = FindObjectOfType<AbstractMap>();
            if (buildingsEnabler == null) buildingsEnabler = FindObjectOfType<MapboxBuildingsEnabler>();
        }

        public void Enable3DForTwin()
        {
            if (_is3DModeActive) return;
            if (abstractMap == null) abstractMap = FindObjectOfType<AbstractMap>();
            if (abstractMap == null) return;

            CacheCurrentStateIfNeeded();

            Apply3DSettings();

            if (useSatelliteStreetsInTwin && abstractMap.ImageLayer != null)
                abstractMap.ImageLayer.SetLayerSource(ImagerySourceType.MapboxSatelliteStreet);

            bool enableBuildings = useBuildingsInTwin && !forceDisableBuildingsForStability;
            if (enableBuildings)
            {
                if (buildingsEnabler == null) buildingsEnabler = FindObjectOfType<MapboxBuildingsEnabler>();
                if (buildingsEnabler != null) buildingsEnabler.EnsureBuildingsLayer();
            }
            else
            {
                DisableBuildingsLayerForStability();
            }

            SafeUpdateMap();
            StartCoroutine(Reinforce3DNextFrames());
            _is3DModeActive = true;
        }

        public void Disable3DForTwin()
        {
            if (!_is3DModeActive) return;
            if (!restoreMapStateOnClose)
            {
                _is3DModeActive = false;
                return;
            }
            if (!_cached) return;
            if (abstractMap == null) abstractMap = FindObjectOfType<AbstractMap>();
            if (abstractMap == null) return;

            if (abstractMap.Terrain != null)
            {
                abstractMap.Terrain.SetLayerSource(_prevTerrainSource);
                abstractMap.Terrain.SetElevationType(_prevTerrainType);
                abstractMap.Terrain.SetExaggerationFactor(_prevExaggeration);
            }

            if (abstractMap.ImageLayer != null)
                abstractMap.ImageLayer.SetLayerSource(_prevImagerySource);

            SafeUpdateMap();
            _is3DModeActive = false;
        }

        private void CacheCurrentStateIfNeeded()
        {
            if (_cached || abstractMap == null) return;

            if (abstractMap.Terrain != null)
            {
                _prevTerrainSource = abstractMap.Terrain.LayerSource;
                _prevTerrainType = abstractMap.Terrain.ElevationType;
                _prevExaggeration = abstractMap.Terrain.ExaggerationFactor;
            }
            else
            {
                _prevTerrainSource = ElevationSourceType.None;
                _prevTerrainType = ElevationLayerType.FlatTerrain;
                _prevExaggeration = 1f;
            }

            _prevImagerySource = abstractMap.ImageLayer != null
                ? abstractMap.ImageLayer.LayerSource
                : ImagerySourceType.MapboxStreets;

            _cached = true;
        }

        private void SafeUpdateMap()
        {
            if (abstractMap == null) return;
            try
            {
                if (abstractMap.Options?.scalingOptions?.scalingStrategy != null)
                    abstractMap.UpdateMap();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[DigitalTwinMap3DMode] UpdateMap skipped due to exception: " + e.Message);
            }
        }

        private void Apply3DSettings()
        {
            if (abstractMap == null || abstractMap.Terrain == null) return;
            abstractMap.Terrain.SetLayerSource(ElevationSourceType.MapboxTerrain);
            abstractMap.Terrain.SetElevationType(ElevationLayerType.TerrainWithElevation);
            abstractMap.Terrain.SetExaggerationFactor(terrainExaggeration);
        }

        private IEnumerator Reinforce3DNextFrames()
        {
            int frames = Mathf.Max(1, reinforceFrames);
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                if (!_is3DModeActive) yield break;
                Apply3DSettings();
                bool enableBuildings = useBuildingsInTwin && !forceDisableBuildingsForStability;
                if (enableBuildings && buildingsEnabler != null) buildingsEnabler.EnsureBuildingsLayer();
                SafeUpdateMap();
            }
        }

        private void DisableBuildingsLayerForStability()
        {
            if (abstractMap == null || abstractMap.VectorData == null) return;
            var buildings = abstractMap.VectorData.FindFeatureSubLayerWithName("Buildings");
            if (buildings == null) return;
            buildings.SetActive(false);
        }
    }
}
