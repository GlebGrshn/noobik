using UnityEngine;

namespace Nubik
{
    public enum BattlePhase { Waiting, Warning, Recovery, Won, Lost }

    /// <summary>
    /// Combat clock advances only while playing. Every attack is marked on the floor before it lands and leaves a short
    /// window when the head is open. Attacks: 0 circle under the player, 1 left half, 2 right half, 3 three circles,
    /// 4 quake over the whole floor (jump). Below half health the pauses shorten and waves come in pairs.
    /// </summary>
    public sealed class BossBattle
    {
        public const float MaxHealth = 720;
        /// <summary>Harpoon damage: body, head while it prepares an attack, head in the open window after one.</summary>
        public const float BodyDamage = 5, HeadDamage = 9, OpenHeadDamage = 26;
        public const int Circle = 0, WaveLeft = 1, WaveRight = 2, Rain = 3, Quake = 4;
        public const float CircleRadius = 2.6f, RainRadius = 2f;

        public float Health { get; private set; } = MaxHealth;
        public BattlePhase Phase { get; private set; }
        public float Remaining { get; private set; }
        /// <summary>Full length of the current warning or recovery, for animation.</summary>
        public float Duration { get; private set; }
        public int Pattern { get; private set; }
        /// <summary>First marked spot (the circle, or the first of three).</summary>
        public Vector2 Target => Targets[0];
        public readonly Vector2[] Targets = new Vector2[3];
        public int TargetCount { get; private set; }
        /// <summary>This attack follows the previous one without a pause.</summary>
        public bool Chained { get; private set; }
        /// <summary>Attacks resolved so far, and the last one's pattern: the visuals slam on each.</summary>
        public int Landed { get; private set; }
        public int LastLanded { get; private set; }
        public bool Enraged => Health < MaxHealth * .5f;

        public string Cue => Phase == BattlePhase.Won ? "Ктулху повержен" : Phase == BattlePhase.Lost ? "" : Phase == BattlePhase.Waiting ? "Возьми гарпун у входа"
            : Phase == BattlePhase.Recovery ? "ГЛАЗА ОТКРЫТЫ · целься в голову"
            : Pattern == Circle ? "ЩУПАЛЬЦА · выйди из круга" : Pattern == WaveLeft ? "ВОЛНА СЛЕВА · уходи вправо" : Pattern == WaveRight ? "ВОЛНА СПРАВА · уходи влево"
            : Pattern == Rain ? "ГРАД ЩУПАЛЕЦ · беги из кругов" : "ЗЕМЛЕТРЯСЕНИЕ · прыгай!";

        private static readonly int[] Calm = { Circle, WaveLeft, Rain, WaveRight, Quake };
        private static readonly int[] Rage = { Rain, WaveLeft, Circle, Quake, WaveRight, Rain, Circle };
        private int attack;

        public void Begin()
        {
            if (Phase != BattlePhase.Waiting) return;
            Phase = BattlePhase.Recovery; Remaining = Duration = 2;
        }

        private float WarningTime(int pattern)
        {
            if (Chained) return .8f;
            float calm = pattern == Rain ? 1.7f : pattern == Quake ? 1.6f : 1.5f;
            return Enraged ? calm * .7f : calm;
        }

        public float Tick(float dt, Vector3 player)
        {
            if (Phase == BattlePhase.Waiting || Phase == BattlePhase.Won || Phase == BattlePhase.Lost) return 0;
            Remaining -= Mathf.Max(0, dt);
            if (Remaining > 0) return 0;
            if (Phase == BattlePhase.Recovery)
            {
                var order = Enraged ? Rage : Calm;
                Start(order[attack++ % order.Length], player, false);
                return 0;
            }
            float damage = Hits(player) ? Damage : 0;
            Landed++;
            LastLanded = Pattern;
            // In rage a wave is answered at once by the other one.
            if (Enraged && !Chained && (Pattern == WaveLeft || Pattern == WaveRight)) Start(Pattern == WaveLeft ? WaveRight : WaveLeft, player, true);
            else
            {
                Chained = false;
                Phase = BattlePhase.Recovery; Remaining = Duration = Enraged ? .95f : 1.3f;
            }
            return damage;
        }

        private void Start(int pattern, Vector3 player, bool chained)
        {
            Pattern = pattern;
            Chained = chained;
            var at = new Vector2(Mathf.Clamp(player.x, -8, 8), Mathf.Clamp(player.z, -10, 9));
            Targets[0] = at;
            TargetCount = pattern == Circle ? 1 : pattern == Rain ? 3 : 0;
            if (pattern == Rain)
            {
                // Two more circles around the first, turning with every attack so they never repeat exactly.
                float angle = attack * 1.7f;
                for (int i = 1; i < 3; i++)
                {
                    var offset = new Vector2(Mathf.Cos(angle + i * 2.3f), Mathf.Sin(angle + i * 2.3f)) * 3.4f;
                    Targets[i] = new Vector2(Mathf.Clamp(at.x + offset.x, -8, 8), Mathf.Clamp(at.y + offset.y, -10, 9));
                }
            }
            Phase = BattlePhase.Warning;
            Remaining = Duration = WarningTime(pattern);
        }

        private float Damage => (Pattern == Rain ? 18 : Pattern == Quake ? 16 : 22) + (Enraged ? 7 : 0);

        private bool Hits(Vector3 player)
        {
            var flat = new Vector2(player.x, player.z);
            switch (Pattern)
            {
                case Circle: return player.y < 1.25f && Vector2.Distance(flat, Targets[0]) < CircleRadius;
                case WaveLeft: return player.y < 1.25f && player.x < -.35f;
                case WaveRight: return player.y < 1.25f && player.x > .35f;
                case Rain:
                    if (player.y >= 1.25f) return false;
                    for (int i = 0; i < TargetCount; i++) if (Vector2.Distance(flat, Targets[i]) < RainRadius) return true;
                    return false;
                default: return player.y < .5f; // the quake shakes the whole floor: be in the air
            }
        }

        public float Shoot(bool head)
        {
            if (Phase != BattlePhase.Warning && Phase != BattlePhase.Recovery) return 0;
            float damage = head ? (Phase == BattlePhase.Recovery ? OpenHeadDamage : HeadDamage) : BodyDamage;
            Health = Mathf.Max(0, Health - damage);
            if (Health == 0) Phase = BattlePhase.Won;
            return damage;
        }

        public void Lose() { if (Phase != BattlePhase.Won) Phase = BattlePhase.Lost; }
    }
}
