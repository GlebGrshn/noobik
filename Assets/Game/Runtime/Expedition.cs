using UnityEngine;

namespace Nubik
{
    public sealed class SealKey
    {
        public readonly string Name, Clue;
        public readonly int Depth;
        public readonly Vector3 Position;
        public readonly Color Color;
        public SealKey(string name, string clue, int depth, Vector3 at, Color color)
        { Name = name; Clue = clue; Depth = depth; Position = at; Color = color; }
    }

    public static class Expedition
    {
        public static readonly SealKey[] Keys =
        {
            new SealKey("Ключ сада", "У рыжего дерева справа от дома", 0, new Vector3(12.8f, .8f, 9), new Color(1, .77f, .25f)),
            new SealKey("Ключ корней", "В лагере на 18 м, за лежанкой", 18, new Vector3(-1.45f, -17.15f, 3.25f), new Color(.65f, .9f, .35f)),
            new SealKey("Ключ шахтёра", "В старой штольне на 48 м", 48, new Vector3(.25f, -47.15f, 3.15f), new Color(.9f, .61f, .28f)),
            new SealKey("Ключ кристаллов", "На столе в гроте на 88 м", 88, new Vector3(-1.45f, -86.78f, 2.85f), new Color(.35f, .95f, .92f)),
            new SealKey("Ключ глубины", "На 110 м: угол шахты под дальней правой стойкой", 110, new Vector3(4.7f, -109.15f, 4.7f), new Color(.8f, .48f, 1))
        };
        public static int NextKey(GameProgress progress)
        { for (int i = 0; i < Keys.Length; i++) if (!progress.HasKey(i)) return i; return -1; }
        public static string Objective(GameProgress progress)
        {
            if (progress.finished) return "Ктулху побеждён · двор спасён";
            int next = NextKey(progress);
            // Only the count: where to look is written in the journal.
            return next >= 0 ? "Ключи " + progress.KeyCount + "/5 · дверь на 120 м"
                : progress.doorOpened ? "Вернись к порталу · 120 м" : "Открой дверь · 120 м";
        }
        public static void Prepare(VoxelTerrain terrain)
        {
            // The last key has its own alcove, connected to the rest only by the player's digging.
            terrain.CarveRoom(new Vector3(4.7f, -108.55f, 4.7f), new Vector3(1.3f, 1.45f, 1.3f));
        }
        public static void Decorate(Shapes shapes)
        {
            var root = new GameObject("Key pedestals").transform;
            for (int i = 0; i < Keys.Length; i++)
            {
                if (i == 3) continue; // Already on the survey table.
                var p = Keys[i].Position;
                shapes.Box("Carved seal pedestal", p + Vector3.down * .56f, new Vector3(.65f, .42f, .65f), new Color(.3f, .35f, .34f), root);
                shapes.Box("Seal pedestal rim", p + Vector3.down * .31f, new Vector3(.72f, .08f, .72f), Keys[i].Color * .65f, root, false, .15f);
            }
        }
    }
}
