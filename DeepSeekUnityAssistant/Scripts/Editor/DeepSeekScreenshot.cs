using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeepSeekUnity.Editor
{
    /// <summary>
    /// Снимает Scene View / Game View в PNG.
    /// Файлы сохраняются в Library/DeepSeek/screenshots/.
    /// </summary>
    public static class DeepSeekScreenshot
    {
        private static string DirPath =>
            Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Library", "DeepSeek", "screenshots");

        /// <summary>Скриншот Scene View. Возвращает путь к PNG или null.</summary>
        public static string CaptureSceneView(int width = 1280, int height = 720)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.camera == null)
            {
                Debug.LogWarning("[DeepSeek] SceneView недоступен");
                return null;
            }

            return RenderCamera(sceneView.camera, width, height, "scene");
        }

        /// <summary>Скриншот Game View (главной камеры). Возвращает путь к PNG или null.</summary>
        public static string CaptureGameView(int width = 1280, int height = 720)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[DeepSeek] Camera.main не найдена");
                return null;
            }

            return RenderCamera(cam, width, height, "game");
        }

        private static string RenderCamera(Camera cam, int width, int height, string prefix)
        {
            try
            {
                if (!Directory.Exists(DirPath))
                    Directory.CreateDirectory(DirPath);

                var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                rt.Create();

                var prevTarget = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = prevTarget;

                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                RenderTexture.active = null;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);

                byte[] png = tex.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(tex);

                string fileName = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string fullPath = Path.Combine(DirPath, fileName);
                File.WriteAllBytes(fullPath, png);

                Debug.Log($"[DeepSeek] Скриншот: {fullPath}");
                return fullPath;
            }
            catch (Exception e)
            {
                Debug.LogError($"[DeepSeek] Ошибка скриншота: {e.Message}");
                return null;
            }
        }

        /// <summary>Папка со скриншотами (для отладки).</summary>
        public static string ScreenshotDir => DirPath;
    }
}