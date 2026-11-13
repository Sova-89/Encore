// ============================================================================
//  PaintCore.cs — рисование маленьких картинок окна в памяти (без Unity).
//  Зачем: у мода нет файлов-картинок. Рамки, кнопки, значки и узор считаются
//  по формулам при запуске — так они чёткие при любом разрешении экрана.
//  Каждая функция возвращает точки RGBA (по 4 байта), строки сверху вниз.
//  Что менять: форму значков — в Icon*, скругление и рамку — в Rounded.
// ============================================================================
using System;

namespace Encore
{
    /// Цвет: четыре доли от 0 до 1 (красный, зелёный, синий, непрозрачность).
    internal struct Rgba
    {
        public float R, G, B, A;

        public Rgba(float r, float g, float b, float a) { R = r; G = g; B = b; A = a; }

        internal static readonly Rgba Clear = new Rgba(0f, 0f, 0f, 0f);
    }

    internal static class PaintCore
    {
        /// Сторона картинки со скруглёнными углами: два радиуса и 4 точки на растягиваемую середину.
        internal static int RoundedSize(int radius)
        {
            return radius * 2 + 4;
        }

        /// Прямоугольник со скруглёнными углами: заливка и рамка толщиной borderWidth точек.
        internal static byte[] Rounded(int radius, Rgba fill, Rgba border, int borderWidth)
        {
            int n = RoundedSize(radius);
            byte[] px = new byte[n * n * 4];
            float half = n / 2f;
            float inner = half - radius;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    // расстояние от точки до края фигуры: меньше нуля — внутри
                    float dx = Math.Abs(x + 0.5f - half) - inner;
                    float dy = Math.Abs(y + 0.5f - half) - inner;
                    float ox = Math.Max(dx, 0f), oy = Math.Max(dy, 0f);
                    float d = (float)Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(dx, dy), 0f) - radius;

                    float cover = Clamp01(0.5f - d);
                    float mix = border.A > 0f ? Clamp01(0.5f + d + borderWidth) : 0f;
                    Put(px, (y * n + x) * 4, Blend(fill, border, mix), cover);
                }
            }
            return px;
        }

        /// Значок из отрезков. Координаты — от середины картинки, y вниз; halfWidth — половина толщины линии.
        internal static byte[] Lines(int n, float[] segments, float halfWidth, Rgba color)
        {
            byte[] px = new byte[n * n * 4];
            float half = n / 2f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float cx = x + 0.5f - half, cy = y + 0.5f - half;
                    float best = float.MaxValue;
                    for (int i = 0; i + 3 < segments.Length; i += 4)
                    {
                        float dist = DistToSegment(cx, cy, segments[i], segments[i + 1], segments[i + 2], segments[i + 3]);
                        best = Math.Min(best, dist);
                    }
                    Put(px, (y * n + x) * 4, color, Clamp01(halfWidth + 0.5f - best));
                }
            }
            return px;
        }

        // Формы значков: пары точек «откуда — куда», q — размах значка от середины.
        internal static float[] ArrowUp(float q)
        {
            return new[] { -q, q * 0.5f, 0f, -q * 0.55f, 0f, -q * 0.55f, q, q * 0.5f };
        }

        internal static float[] ArrowDown(float q)
        {
            return new[] { -q, -q * 0.5f, 0f, q * 0.55f, 0f, q * 0.55f, q, -q * 0.5f };
        }

        internal static float[] Cross(float q)
        {
            float c = q * 0.8f;
            return new[] { -c, -c, c, c, -c, c, c, -c };
        }

        internal static float[] Plus(float q)
        {
            return new[] { -q, 0f, q, 0f, 0f, -q, 0f, q };
        }

        /// Ромб узора под заголовком: контур радиусом outer и закрашенная середина радиусом inner.
        internal static byte[] Diamond(int n, float outer, float inner, float halfWidth, Rgba color)
        {
            const float Sqrt2 = 1.41421356f;
            byte[] px = new byte[n * n * 4];
            float half = n / 2f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float sum = Math.Abs(x + 0.5f - half) + Math.Abs(y + 0.5f - half);
                    float ring = Clamp01(halfWidth + 0.5f - Math.Abs(sum - outer) / Sqrt2);
                    float core = Clamp01(0.5f - (sum - inner) / Sqrt2);
                    Put(px, (y * n + x) * 4, color, Math.Max(ring, core));
                }
            }
            return px;
        }

        /// Полоска в одну точку высотой: слева плотная, к правому краю тает.
        internal static byte[] FadingLine(int width, Rgba color, float power)
        {
            byte[] px = new byte[width * 4];
            for (int x = 0; x < width; x++)
            {
                float t = 1f - x / (float)(width - 1);
                Put(px, x * 4, color, (float)Math.Pow(t, power));
            }
            return px;
        }

        // ---- мелочи ------------------------------------------------------------

        static Rgba Blend(Rgba fill, Rgba border, float mix)
        {
            float a = fill.A * (1f - mix) + border.A * mix;
            if (a <= 0.0001f) return new Rgba(border.R, border.G, border.B, 0f);
            float wf = fill.A * (1f - mix) / a, wb = border.A * mix / a;
            return new Rgba(fill.R * wf + border.R * wb, fill.G * wf + border.G * wb, fill.B * wf + border.B * wb, a);
        }

        static void Put(byte[] px, int at, Rgba c, float cover)
        {
            px[at] = ToByte(c.R);
            px[at + 1] = ToByte(c.G);
            px[at + 2] = ToByte(c.B);
            px[at + 3] = ToByte(c.A * cover);
        }

        static float DistToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay;
            float len2 = vx * vx + vy * vy;
            float t = len2 > 0f ? Clamp01(((px - ax) * vx + (py - ay) * vy) / len2) : 0f;
            float dx = px - (ax + vx * t), dy = py - (ay + vy * t);
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

        static byte ToByte(float v) { return (byte)Math.Round(Clamp01(v) * 255f); }
    }
}
