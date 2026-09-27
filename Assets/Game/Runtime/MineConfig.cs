using UnityEngine;

namespace Nubik
{
    [CreateAssetMenu(menuName = "Nubik/Mine balance")]
    public sealed class MineConfig : ScriptableObject
    {
        public int seed = 28092026;
        public int rows = 30;
        public int columns = 5;
        public int dirtHealth = 2;
        public float hitInterval = 0.42f;
        public float moveSpeed = 4f;
        public int[] pickDamage = { 1, 2, 4 };
        public int[] pickPrices = { 0, 60, 220 };
        public string[] pickNames = { "Обычная", "Медная", "Стальная" };

        public int BlockCount => rows * columns;
        public int Coins(int id) => 2 + (int)(Hash(id) % 3);
        public int Loot(int id) => id == 2 || Hash(id) % 9 == 0 ? 12 + (int)(Hash(id + 701) % 14) : 0;
        public bool IsCollection(int id) => id == columns * 3 + columns / 2;
        public uint Hash(int id)
        {
            unchecked
            {
                uint x = (uint)(id ^ seed);
                x = (x ^ (x >> 16)) * 0x45d9f3b;
                x = (x ^ (x >> 16)) * 0x45d9f3b;
                return x ^ (x >> 16);
            }
        }
    }
}
