// ============================================================================
//  PaintCore.cs — рисование маленьких картинок окна в памяти (без Unity).
//  Зачем: у мода нет файлов-картинок. Рамки, кнопки, значки и узор считаются
//  по формулам при запуске — так они чёткие при любом разрешении экрана.
//  Каждая функция возвращает точки RGBA (по 4 байта), строки сверху вниз.
//  Что менять: форму значков — в Icon*, скругление и рамку — в Rounded.
// ============================================================================
using System;
using System.Collections.Generic;

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


        // ---- узор в стиле игры (0.28.0) -----------------------------------------------

        /// Тонкая линия, тающая к обоим концам: в середине плотная. Один пиксель высотой (или шириной, если класть столбцом).
        internal static byte[] Taper(int length, Rgba color, float peak)
        {
            byte[] px = new byte[length * 4];
            for (int x = 0; x < length; x++)
            {
                float t = length > 1 ? x / (float)(length - 1) : 0.5f;
                float edge = Math.Min(t, 1f - t) * 2f;          // 0 на концах, 1 в середине
                float k = Clamp01(edge * 2.2f);                 // плотная уже с трети длины
                Put(px, x * 4, color, peak * k * k * (3f - 2f * k));
            }
            return px;
        }

        /// Кольцо: тонкий светлый круг (как гнёзда инструментов в игре). thickness — в точках картинки.
        internal static byte[] Ring(int n, float thickness, Rgba color)
        {
            byte[] px = new byte[n * n * 4];
            float half = n / 2f, outer = half - 0.5f, mid = outer - thickness / 2f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - half, dy = y + 0.5f - half;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    Put(px, (y * n + x) * 4, color, Clamp01(thickness / 2f + 0.5f - Math.Abs(d - mid)));
                }
            }
            return px;
        }

        /// Мягкий светлый круг (подложка под значком): плотный в середине, к краю тает.
        internal static byte[] Disc(int n, Rgba color, float power)
        {
            byte[] px = new byte[n * n * 4];
            float half = n / 2f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - half, dy = y + 0.5f - half;
                    float t = 1f - (float)Math.Sqrt(dx * dx + dy * dy) / half;
                    Put(px, (y * n + x) * 4, color, t <= 0f ? 0f : (float)Math.Pow(t, power));
                }
            }
            return px;
        }

        /// Угловой завиток окна (левый верхний угол): две тающие линии вдоль краёв, две дуги с шипами и ромб.
        /// Если у игры нашёлся её собственный завиток (overscan_corner_fleur009), рисуется он, а это — запасной.
        internal static byte[] Fleur(int n, Rgba color)
        {
            float k = n / 100f;                                   // рисуем в клетке 100 x 100, потом растягиваем
            List<float[]> strokes = new List<float[]>();
            strokes.Add(new[] { 5f, 5f, 1.5f, 40f, 5f, 1.1f, 100f, 5f, 0.1f });          // линия вдоль верха
            strokes.Add(new[] { 5f, 5f, 1.5f, 5f, 40f, 1.1f, 5f, 100f, 0.1f });          // и вдоль левого края
            strokes.Add(Arc(5f, 5f, 46f, 4f, 86f, 1.7f));                               // большая дуга
            strokes.Add(Arc(5f, 5f, 30f, 8f, 82f, 1.3f));                              // малая дуга
            float[] outerAngles = { 22f, 45f, 68f };
            for (int i = 0; i < outerAngles.Length; i++)                                // шипы наружу
                strokes.Add(Thorn(5f, 5f, 46f, outerAngles[i], 12f, 1.5f));
            float[] innerAngles = { 30f, 60f };
            for (int i = 0; i < innerAngles.Length; i++)                                // и внутрь
                strokes.Add(Thorn(5f, 5f, 30f, innerAngles[i], -8f, 1.2f));
            float[] dots = { 5f + 46f * Cos(4f), 5f + 46f * Sin(4f), 5f + 46f * Cos(86f), 5f + 46f * Sin(86f) };

            byte[] px = new byte[n * n * 4];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float fx = (x + 0.5f) / k, fy = (y + 0.5f) / k;
                    float best = float.MaxValue;
                    for (int s = 0; s < strokes.Count; s++)
                        best = Math.Min(best, StrokeDist(fx, fy, strokes[s]));
                    for (int i = 0; i < dots.Length; i += 2)
                    {
                        float dx = fx - dots[i], dy = fy - dots[i + 1];
                        best = Math.Min(best, (float)Math.Sqrt(dx * dx + dy * dy) - 2.1f);
                    }
                    float diamond = (Math.Abs(fx - 14f) + Math.Abs(fy - 14f)) * 0.7071f - 3.4f;   // ромб в самом углу
                    best = Math.Min(best, diamond);
                    Put(px, (y * n + x) * 4, color, Clamp01(0.5f - best * k));
                }
            }
            return px;
        }

        static float Cos(float deg) { return (float)Math.Cos(deg * Math.PI / 180.0); }
        static float Sin(float deg) { return (float)Math.Sin(deg * Math.PI / 180.0); }

        /// Дуга вокруг (cx, cy): точки с толщиной, которая растёт к середине и сходит на нет к концам.
        static float[] Arc(float cx, float cy, float radius, float fromDeg, float toDeg, float width)
        {
            const int Steps = 24;
            float[] p = new float[(Steps + 1) * 3];
            for (int i = 0; i <= Steps; i++)
            {
                float t = i / (float)Steps;
                float a = fromDeg + (toDeg - fromDeg) * t;
                float w = 0.15f + width * (float)Math.Sin(t * Math.PI);
                p[i * 3] = cx + radius * Cos(a);
                p[i * 3 + 1] = cy + radius * Sin(a);
                p[i * 3 + 2] = w;
            }
            return p;
        }

        /// Шип: короткая линия от дуги наружу (length > 0) или внутрь (length < 0), от толстого основания к острию.
        static float[] Thorn(float cx, float cy, float radius, float deg, float length, float width)
        {
            float x0 = cx + radius * Cos(deg), y0 = cy + radius * Sin(deg);
            float r1 = radius + length;
            float bx = x0 - (length > 0f ? 1.5f : -1.5f) * Sin(deg), by = y0 + (length > 0f ? 1.5f : -1.5f) * Cos(deg);
            return new[] { x0, y0, width, cx + r1 * Cos(deg + (length > 0f ? 6f : -6f)), cy + r1 * Sin(deg + (length > 0f ? 6f : -6f)), 0.08f };
        }

        /// Расстояние от точки до линии с переменной толщиной (минус толщина в этой точке): меньше нуля — внутри.
        static float StrokeDist(float px, float py, float[] p)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 5 < p.Length; i += 3)
            {
                float ax = p[i], ay = p[i + 1], aw = p[i + 2], bx = p[i + 3], by = p[i + 4], bw = p[i + 5];
                float vx = bx - ax, vy = by - ay;
                float len2 = vx * vx + vy * vy;
                float t = len2 > 0f ? Clamp01(((px - ax) * vx + (py - ay) * vy) / len2) : 0f;
                float dx = px - (ax + vx * t), dy = py - (ay + vy * t);
                float d = (float)Math.Sqrt(dx * dx + dy * dy) - (aw + (bw - aw) * t);
                if (d < best) best = d;
            }
            return best;
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
