using System;
using System.Collections.Generic;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>
    /// Grille spatiale reconstruite à chaque frame (tri par comptage, aucune allocation).
    /// Sert à trouver vite l'ennemi le plus proche, les collisions des balles et la séparation entre ennemis.
    /// Remplace la physique Unity, beaucoup trop chère avec des centaines d'ennemis.
    /// </summary>
    public static class EnemyGrid
    {
        const float CellSize = 1.5f;
        const float Margin = 3f;

        static Vector2 origin;
        static int cols, rows;
        static int[] cellStart = Array.Empty<int>();
        static int[] cursor = Array.Empty<int>();
        static Enemy[] sorted = new Enemy[256];
        static int[] enemyCell = new int[256];
        static int count;

        public static void Clear() => count = 0;

        public static void Rebuild(List<Enemy> enemies, Rect bounds)
        {
            origin = bounds.min - Vector2.one * Margin;
            cols = Mathf.CeilToInt((bounds.width + Margin * 2f) / CellSize);
            rows = Mathf.CeilToInt((bounds.height + Margin * 2f) / CellSize);
            int cells = cols * rows;
            if (cellStart.Length != cells + 1)
            {
                cellStart = new int[cells + 1];
                cursor = new int[cells];
            }

            count = enemies.Count;
            if (sorted.Length < count)
            {
                int size = Mathf.NextPowerOfTwo(count);
                sorted = new Enemy[size];
                enemyCell = new int[size];
            }

            Array.Clear(cursor, 0, cells);
            for (int i = 0; i < count; i++)
            {
                int c = CellIndex(enemies[i].Position);
                enemyCell[i] = c;
                cursor[c]++;
            }

            cellStart[0] = 0;
            for (int c = 0; c < cells; c++)
            {
                cellStart[c + 1] = cellStart[c] + cursor[c];
                cursor[c] = cellStart[c];
            }

            for (int i = 0; i < count; i++)
                sorted[cursor[enemyCell[i]]++] = enemies[i];
        }

        /// <summary>Remplit <paramref name="results"/> avec les ennemis vivants qui touchent le cercle.</summary>
        public static void Query(Vector2 center, float radius, List<Enemy> results)
        {
            results.Clear();
            if (count == 0) return;

            GetCellRange(center, radius + 1f, out int x0, out int x1, out int y0, out int y1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int c = y * cols + x;
                for (int k = cellStart[c]; k < cellStart[c + 1]; k++)
                {
                    var e = sorted[k];
                    if (!e.IsAlive) continue;
                    float r = radius + e.Radius;
                    if ((e.Position - center).sqrMagnitude <= r * r) results.Add(e);
                }
            }
        }

        public static Enemy FindNearest(Vector2 position, float maxRange)
        {
            if (count == 0) return null;

            Enemy best = null;
            float bestSqr = maxRange * maxRange;
            GetCellRange(position, maxRange, out int x0, out int x1, out int y0, out int y1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int c = y * cols + x;
                for (int k = cellStart[c]; k < cellStart[c + 1]; k++)
                {
                    var e = sorted[k];
                    if (!e.IsAlive) continue;
                    float d = (e.Position - position).sqrMagnitude;
                    if (d < bestSqr)
                    {
                        bestSqr = d;
                        best = e;
                    }
                }
            }
            return best;
        }

        static int CellIndex(Vector2 p) => CellY(p.y) * cols + CellX(p.x);
        static int CellX(float x) => Mathf.Clamp((int)((x - origin.x) / CellSize), 0, cols - 1);
        static int CellY(float y) => Mathf.Clamp((int)((y - origin.y) / CellSize), 0, rows - 1);

        static void GetCellRange(Vector2 c, float r, out int x0, out int x1, out int y0, out int y1)
        {
            x0 = CellX(c.x - r);
            x1 = CellX(c.x + r);
            y0 = CellY(c.y - r);
            y1 = CellY(c.y + r);
        }
    }
}
