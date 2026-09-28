using UnityEngine;

namespace Nubik
{
    public enum BattlePhase { Waiting, Warning, Recovery, Won, Lost }

    /// <summary>Combat clock advances only while playing. Attacks have readable warnings and a recovery window.</summary>
    public sealed class BossBattle
    {
        public const float MaxHealth = 720;
        public float Health { get; private set; } = MaxHealth;
        public BattlePhase Phase { get; private set; }
        public float Remaining { get; private set; }
        /// <summary>Full length of the current warning or recovery, for animation.</summary>
        public float Duration { get; private set; }
        public int Pattern { get; private set; }
        public Vector2 Target { get; private set; }
        public bool Enraged => Health < MaxHealth * .5f;
        public string Cue => Phase == BattlePhase.Won ? "Ктулху повержен" : Phase == BattlePhase.Lost ? "" : Phase == BattlePhase.Waiting ? "Возьми гарпун у входа" : Phase == BattlePhase.Recovery ? "ГЛАЗА ОТКРЫТЫ · целься в голову"
            : Pattern == 0 ? "ЩУПАЛЬЦА · выйди из круга" : Pattern == 1 ? "ВОЛНА СЛЕВА · уходи вправо" : "ВОЛНА СПРАВА · уходи влево";
        private int attack;
        public void Begin()
        {
            if (Phase != BattlePhase.Waiting) return;
            Phase = BattlePhase.Recovery; Remaining = Duration = 2;
        }
        public float Tick(float dt, Vector3 player)
        {
            if (Phase == BattlePhase.Waiting || Phase == BattlePhase.Won || Phase == BattlePhase.Lost) return 0;
            Remaining -= Mathf.Max(0, dt);
            if (Remaining > 0) return 0;
            if (Phase == BattlePhase.Recovery)
            {
                Pattern = attack++ % 3;
                Target = new Vector2(Mathf.Clamp(player.x, -8, 8), Mathf.Clamp(player.z, -10, 9));
                Phase = BattlePhase.Warning; Remaining = Duration = Enraged ? 1.15f : 1.8f;
                return 0;
            }
            bool hit = Pattern == 0 ? Vector2.Distance(new Vector2(player.x, player.z), Target) < 2.6f
                : Pattern == 1 ? player.x < -.35f : player.x > .35f;
            Phase = BattlePhase.Recovery; Remaining = Duration = Enraged ? 1.25f : 1.7f;
            return hit && player.y < 1.25f ? (Enraged ? 29 : 22) : 0;
        }
        public float Shoot(bool head)
        {
            if (Phase != BattlePhase.Warning && Phase != BattlePhase.Recovery) return 0;
            float damage = head ? (Phase == BattlePhase.Recovery ? 48 : 18) : 10;
            Health = Mathf.Max(0, Health - damage);
            if (Health == 0) Phase = BattlePhase.Won;
            return damage;
        }
        public void Lose() { if (Phase != BattlePhase.Won) Phase = BattlePhase.Lost; }
    }
}
