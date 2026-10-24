using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TMPTextOnPath.Editor
{
    static class TextOnPathMenu
    {
        const string k_SampleText = "TEXT ON PATH • TEXT ON PATH • ";

        [MenuItem("GameObject/Text On Path/Text On Path (3D)", false, 10)]
        static void Create3D(MenuCommand command)
        {
            var go = new GameObject("Text On Path");
            GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
            var text = go.AddComponent<TextMeshPro>();
            SetupText(text, k_SampleText);
            go.AddComponent<TextOnPath>();
            Undo.RegisterCreatedObjectUndo(go, "Create Text On Path");
            Selection.activeGameObject = go;
        }

        [MenuItem("GameObject/Text On Path/Text On Path (UI)", false, 11)]
        static void CreateUI(MenuCommand command)
        {
            Transform parent = GetOrCreateCanvasParent(command.context as GameObject);
            GameObject go = CreateUIText("Text On Path (UI)", parent, k_SampleText, out _);
            go.AddComponent<TextOnPath>();
            Selection.activeGameObject = go;
        }

        /// <summary>
        /// Two texts sharing one circle: the top text defines the path, the bottom text references it
        /// and runs in reverse so it stays readable.
        /// </summary>
        [MenuItem("GameObject/Text On Path/Circular Badge (UI)", false, 12)]
        static void CreateCircularBadge(MenuCommand command)
        {
            Transform parent = GetOrCreateCanvasParent(command.context as GameObject);
            var root = new GameObject("Circular Badge", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Create Circular Badge");
            GameObjectUtility.SetParentAndAlign(root, parent.gameObject);
            ((RectTransform)root.transform).sizeDelta = new Vector2(500f, 500f);

            const float gap = 10f;

            CreateUIText("Top Text", root.transform, "TOP TEXT", out TextMeshProUGUI top);
            var topPath = top.gameObject.AddComponent<TextOnPath>();
            topPath.Shape = PathShape.Circle;
            topPath.Radius = 200f;
            topPath.StartAngle = 90f;
            topPath.Clockwise = true;
            topPath.StartOffset = 0f;
            topPath.Alignment = PathTextAlignment.Center;
            topPath.VerticalOffset = gap;

            CreateUIText("Bottom Text", root.transform, "BOTTOM TEXT", out TextMeshProUGUI bottom);
            var bottomPath = bottom.gameObject.AddComponent<TextOnPath>();
            bottomPath.Shape = PathShape.Reference;
            bottomPath.Reference = topPath;
            bottomPath.StartOffset = 0.5f;
            bottomPath.Alignment = PathTextAlignment.Center;
            bottomPath.Reverse = true;
            bottomPath.VerticalAnchor = PathVerticalAnchor.Top;
            bottomPath.VerticalOffset = -gap;

            Selection.activeGameObject = root;
        }

        static GameObject CreateUIText(string name, Transform parent, string content, out TextMeshProUGUI text)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            GameObjectUtility.SetParentAndAlign(go, parent.gameObject);
            ((RectTransform)go.transform).sizeDelta = new Vector2(400f, 100f);
            text = go.AddComponent<TextMeshProUGUI>();
            SetupText(text, content);
            return go;
        }

        static void SetupText(TMP_Text text, string content)
        {
            text.text = content;
            text.alignment = TextAlignmentOptions.Center;
#if UNITY_2023_2_OR_NEWER
            text.textWrappingMode = TextWrappingModes.NoWrap;
#else
            text.enableWordWrapping = false;
#endif
            text.overflowMode = TextOverflowModes.Overflow;
        }

        static Transform GetOrCreateCanvasParent(GameObject context)
        {
            if (context != null && context.GetComponentInParent<Canvas>() != null)
                return context.transform;

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            canvasGo.layer = LayerMask.NameToLayer("UI");
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            if (context != null)
                GameObjectUtility.SetParentAndAlign(canvasGo, context);
            return canvasGo.transform;
        }
    }
}
