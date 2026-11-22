using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ArmWrestlingVR
{
    public enum ArenaTheme { NeonArena, TrainingGym, CompetitionHall }

    // Owns only the room around the calibrated device. The table, avatars and HUD are siblings.
    public sealed class ArenaEnvironment : MonoBehaviour
    {
        [SerializeField] private Transform scenery;
        [SerializeField] private ArenaTheme selected;
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private Camera eye;
        private Material temporarySkybox;
        private Material originalSkybox;
        private bool captured, originalFog;
        private Color originalAmbient, originalFogColor, originalBackground;
        private AmbientMode originalAmbientMode;
        private FogMode originalFogMode;
        private CameraClearFlags originalClear;
        private float originalIntensity, originalDensity;

        internal void Select(ArenaTheme theme)
        {
            if (scenery != null && selected == theme) return;
            if (!captured)
            {
                eye = transform.parent != null ? transform.parent.GetComponentInChildren<Camera>(true) : null;
                if (eye != null)
                {
                    originalClear = eye.clearFlags;
                    originalBackground = eye.backgroundColor;
                }
                originalAmbientMode = RenderSettings.ambientMode;
                originalAmbient = RenderSettings.ambientLight;
                originalIntensity = RenderSettings.ambientIntensity;
                originalSkybox = RenderSettings.skybox;
                originalFog = RenderSettings.fog;
                originalFogMode = RenderSettings.fogMode;
                originalFogColor = RenderSettings.fogColor;
                originalDensity = RenderSettings.fogDensity;
                captured = true;
            }
            RestoreSettings();
            if (scenery != null)
            {
                scenery.gameObject.SetActive(false);
                scenery.name = "Replaced Arena Environment";
                Release(scenery.gameObject);
            }
            ReleaseResources();
            scenery = new GameObject("Arena Environment").transform;
            scenery.SetParent(transform, false);
            selected = theme;
            bool sky = false;
            if (theme == ArenaTheme.NeonArena)
                sky = ArenaFactory.BuildNeonVenue(scenery, out temporarySkybox);
            else
                VenueInterior.Build(scenery, theme, ownedMeshes);

            if (eye != null) eye.clearFlags = sky ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        }

        private void RestoreSettings()
        {
            RenderSettings.skybox = originalSkybox;
            RenderSettings.ambientLight = originalAmbient;
            RenderSettings.ambientMode = originalAmbientMode;
            RenderSettings.ambientIntensity = originalIntensity;
            RenderSettings.fog = originalFog;
            RenderSettings.fogMode = originalFogMode;
            RenderSettings.fogColor = originalFogColor;
            RenderSettings.fogDensity = originalDensity;
            if (eye != null)
            {
                eye.clearFlags = originalClear;
                eye.backgroundColor = originalBackground;
            }
            if (temporarySkybox != null || ArenaFactory.UseHdriSky) DynamicGI.UpdateEnvironment();
        }

        private void ReleaseResources()
        {
            foreach (Mesh mesh in ownedMeshes) Release(mesh);
            ownedMeshes.Clear();
            if (temporarySkybox != null) Release(temporarySkybox);
            temporarySkybox = null;
        }

        private void OnDestroy()
        {
            if (captured && gameObject.scene.isLoaded && gameObject.scene == SceneManager.GetActiveScene()) RestoreSettings();
            ReleaseResources();
        }

        internal static void Release(Object item)
        {
            if (Application.isPlaying) Object.Destroy(item);
            else Object.DestroyImmediate(item);
        }
    }
}
