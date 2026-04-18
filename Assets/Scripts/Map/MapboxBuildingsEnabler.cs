using UnityEngine;
using Mapbox.Unity.Map;

namespace GroundStation.Map
{
    /// <summary>
    /// Map objesine eklenir. Sadece Vector kapaliysa acip EKSIKSE bina katmani ekler;
    /// mevcut katmanlara DOKUNMAZ – haritanin baslangic calismasi bozulmaz.
    /// </summary>
    public class MapboxBuildingsEnabler : MonoBehaviour
    {
        [SerializeField] private AbstractMap abstractMap;
        [Tooltip("True = sadece eksikse ekle, mevcut katmanlari degistirme (onerilir).")]
        [SerializeField] private bool onlyAddIfMissing = true;

        private bool _done;

        private void Start()
        {
            EnsureBuildingsLayer();
        }

        public void EnsureBuildingsLayer()
        {
            if (_done && onlyAddIfMissing) return;
            if (abstractMap == null) abstractMap = GetComponent<AbstractMap>();
            if (abstractMap == null) abstractMap = FindObjectOfType<AbstractMap>();
            if (abstractMap == null)
            {
                Debug.LogWarning("[MapboxBuildingsEnabler] AbstractMap bulunamadi.");
                return;
            }

            var vectorData = abstractMap.VectorData;
            if (vectorData == null) return;

            if (!vectorData.IsLayerActive)
                vectorData.SetLayerSource(VectorSourceType.MapboxStreets);

            var buildingLayer = vectorData.FindFeatureSubLayerWithName("Buildings");
            if (buildingLayer == null)
                vectorData.AddPolygonFeatureSubLayer("Buildings", "building");

            if (_done) return;
            _done = true;
            StartCoroutine(UpdateMapDelayed());
        }

        private System.Collections.IEnumerator UpdateMapDelayed()
        {
            yield return null;
            if (abstractMap == null) yield break;
            if (abstractMap.Options != null && abstractMap.Options.scalingOptions != null && abstractMap.Options.scalingOptions.scalingStrategy != null)
                abstractMap.UpdateMap();
        }
    }
}
