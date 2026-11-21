// ============================================================================
//  Overlay.cs — рисование окна и плашек.
//  Отдельная часть мода, которая ВКЛЮЧЕНА только когда есть что рисовать
//  (открыто окно, идёт сегмент или висит сообщение). В остальное время игра
//  её не вызывает вовсе — в обычной игре мод на рисование ничего не тратит.
//  Включает и выключает её Plugin.Update.
// ============================================================================
using UnityEngine;

namespace Encore
{
    internal sealed class Overlay : MonoBehaviour
    {
        internal Plugin Owner;

        void Awake()
        {
            // Окно собрано из готовых прямоугольников, раскладка Unity не нужна:
            // без неё игра зовёт OnGUI один раз за кадр вместо двух и не сорит памятью.
            useGUILayout = false;
            enabled = false;
        }

        void OnGUI()
        {
            long started = Load.Begin();
            Load.CountDraw();
            Owner.DrawGui();
            Load.End(started);
        }

        void LateUpdate()
        {
            long started = Load.Begin();
            Owner.AfterFrame();
            Load.End(started);
        }
    }

    /// Запоминает ширину одной подписи: мерить текст каждый кадр незачем, пока он не изменился.
    internal sealed class TextWidth
    {
        readonly GUIContent content = new GUIContent();
        string text;
        GUIStyle style;
        int version = -1;
        float width;

        internal float Of(string newText, GUIStyle newStyle)
        {
            if (newText != text || newStyle != style || version != Theme.Version)
            {
                text = newText;
                style = newStyle;
                version = Theme.Version;
                content.text = newText;
                width = newStyle.CalcSize(content).x;
            }
            return width;
        }
    }
}
