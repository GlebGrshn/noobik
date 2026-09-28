using UnityEngine;

namespace Nubik
{
    public enum SecretLook { Gnome, Letter, Capsule, Skull, Statue, Mushroom, Safe }

    public sealed class Secret
    {
        public readonly string Name, Note;
        public readonly int Reward;
        public readonly Vector3 Position;
        /// <summary>Half size of the hidden pocket carved around the secret; zero on the surface.</summary>
        public readonly Vector3 Pocket;
        public readonly Color Color;
        public readonly SecretLook Look;

        public Secret(string name, string note, int reward, Vector3 at, Vector3 pocket, Color color, SecretLook look)
        { Name = name; Note = note; Reward = reward; Position = at; Pocket = pocket; Color = color; Look = look; }

        public bool OnSurface => Position.y > -0.5f;
    }

    /// <summary>
    /// Things nobody tells you about. The journal counts them but never says where; the letter in the
    /// mailbox is the only hint, and it points at one of the others.
    /// </summary>
    public static class Secrets
    {
        /// <summary>Index of the statue: selling ore pays a little more once it is found.</summary>
        public const int Statue = 4;
        public const float StatueBonus = 0.1f;

        public static readonly Secret[] All =
        {
            new Secret("Садовый гном", "Прятался в дальнем углу сада. Бывший хозяин оставил под ним пару монет.", 40,
                new Vector3(16.2f, 0.25f, -14.5f), Vector3.zero, new Color(0.92f, 0.25f, 0.2f), SecretLook.Gnome),
            new Secret("Письмо в почтовом ящике", "«Капсулу я закопал в углу участка ближе к дому, по левую руку, если стоять к дому лицом. Метров девять вглубь.»", 20,
                new Vector3(-15.8f, 1.05f, 14.5f), Vector3.zero, new Color(0.96f, 0.92f, 0.8f), SecretLook.Letter),
            new Secret("Капсула времени", "Внутри — детский рисунок двора и немного монет. Похоже, копать тут начали давно.", 150,
                new Vector3(-5.2f, -9.15f, 5.2f), new Vector3(0.7f, 0.65f, 0.7f), new Color(0.7f, 0.76f, 0.8f), SecretLook.Capsule),
            new Secret("Окаменелый череп", "Огромный череп неизвестного зверя. Музей хорошо заплатил бы за такое.", 300,
                new Vector3(5f, -36.2f, -5f), new Vector3(0.95f, 0.8f, 0.95f), new Color(0.9f, 0.84f, 0.7f), SecretLook.Skull),
            new Secret("Золотой Нубик", "Кто-то построил комнату ради статуэтки. Теперь скупщик платит тебе на 10% больше.", 500,
                new Vector3(-5f, -64.2f, -5f), new Vector3(1.1f, 1.2f, 1.1f), new Color(1f, 0.74f, 0.3f), SecretLook.Statue),
            new Secret("Светящийся гриб", "Гриб светится даже в полной темноте. Учёные из города купили образец.", 700,
                new Vector3(5.2f, -93.2f, 5.2f), new Vector3(0.9f, 0.85f, 0.9f), new Color(0.55f, 1f, 0.75f), SecretLook.Mushroom),
            new Secret("Старый сейф", "Замок проржавел и открылся от удара. Чей-то клад ждал десятки лет.", 1200,
                new Vector3(-5f, -115.2f, 5f), new Vector3(0.9f, 0.85f, 0.9f), new Color(0.35f, 0.4f, 0.45f), SecretLook.Safe),
        };

        /// <summary>Carves the hidden pockets: reachable only by digging into them.</summary>
        public static void Prepare(VoxelTerrain terrain)
        {
            foreach (var secret in All)
                if (!secret.OnSurface) terrain.CarveRoom(secret.Position + Vector3.up * (secret.Pocket.y - 0.35f), secret.Pocket);
        }

        /// <summary>Keeps generated ore out of the air of a pocket: it moves into the nearest side wall.</summary>
        public static Vector3 Embed(Vector3 position)
        {
            foreach (var secret in All)
            {
                if (secret.OnSurface) continue;
                var center = secret.Position + Vector3.up * (secret.Pocket.y - 0.35f);
                var p = position - center;
                var reach = secret.Pocket + Vector3.one * 0.3f;
                if (Mathf.Abs(p.x) > reach.x || Mathf.Abs(p.y) > reach.y || Mathf.Abs(p.z) > reach.z) continue;
                position.x = center.x + (p.x < 0 ? -1 : 1) * (secret.Pocket.x + 0.3f);
            }
            return position;
        }

        /// <summary>Surface props that hold the first two secrets.</summary>
        public static void Decorate(Shapes shapes)
        {
            var root = new GameObject("Secret props").transform;
            var post = new Color(0.4f, 0.28f, 0.2f);
            var mailbox = All[1].Position;
            shapes.Box("Mailbox post wood", mailbox + new Vector3(0, -0.55f, 0), new Vector3(0.1f, 1.1f, 0.1f), post, root, true);
            shapes.Box("Mailbox metal", mailbox + new Vector3(0, 0.1f, 0), new Vector3(0.36f, 0.3f, 0.55f), new Color(0.24f, 0.42f, 0.62f), root);
            shapes.Box("Mailbox flag metal", mailbox + new Vector3(0.2f, 0.25f, 0.1f), new Vector3(0.03f, 0.22f, 0.08f), new Color(0.9f, 0.25f, 0.2f), root);
            // A few stones hide the gnome from the lawn.
            var gnome = All[0].Position;
            for (int i = 0; i < 4; i++)
                shapes.Ball("Stone", gnome + new Vector3(Mathf.Cos(i * 1.1f + 2.2f) * .42f, -.19f, Mathf.Sin(i * 1.1f + 2.2f) * .42f), new Vector3(.26f, .17f, .22f), new Color(.5f, .52f, .48f), root);
        }

        public static int Count(GameProgress progress)
        {
            int n = 0;
            for (int i = 0; i < All.Length; i++) if (progress.HasSecret(i)) n++;
            return n;
        }
    }
}
