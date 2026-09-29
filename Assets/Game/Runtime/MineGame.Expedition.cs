using UnityEngine;
using UnityEngine.Rendering;

namespace Nubik
{
    public sealed partial class MineGame
    {
        private BossEncounter boss;
        private Transform drillRotor;
        private bool modelDrill, modelWeapon, battleResultShown;
        private float arenaFuel;
        public bool InBoss => boss != null && boss.Inside;
        public BossBattle Battle => boss?.Battle;
        public bool UsingDrill => Progress.tool == config.tools.Length - 1 && Fuel >= config.drillFuelPerHit;
        public bool Refuelling { get; private set; }
        private Vector3 DoorLanding => new Vector3(0, config.FloorY + .15f, -.8f);
        private bool NearDoor => !InBoss && Vector3.Distance(body.transform.position + Vector3.up, yard.DoorPoint) < 3.2f;
        public string Interaction
        {
            get
            {
                if (InBoss)
                {
                    if (Battle.Phase == BattlePhase.Won) return "Вернуться домой";
                    if (Battle.Phase == BattlePhase.Lost) return "Повторить бой";
                    return !Progress.hasWeapon && Vector3.Distance(body.transform.position, BossEncounter.WeaponPoint) < 3.2f ? "Взять гарпун" : "";
                }
                if (NearDoor && Progress.KeyCount == 5 && !Progress.finished) return "Открыть дверь";
                return Station == Station.Counter ? "Скупка" : Station == Station.Workbench ? "Мастерская" : "";
            }
        }
        public void Interact()
        {
            if (InBoss)
            {
                if (Battle.Phase == BattlePhase.Won) { ReturnFromBoss(); return; }
                if (Battle.Phase == BattlePhase.Lost) { RetryBoss(); return; }
                if (!Progress.hasWeapon && Vector3.Distance(body.transform.position, BossEncounter.WeaponPoint) < 3.2f)
                {
                    Progress.hasWeapon = true; boss.Arm(); BuildTool(); SaveNow();
                    hud.Announce(UiGlyph.Kind.Key, new Color(.4f, 1, .9f), "ДРЕВНИЙ ГАРПУН", "Разорви последнюю печать", "Стреляй в голову между атаками. Заряды восстанавливаются сами.");
                }
                return;
            }
            if (NearDoor && !Progress.finished)
            {
                if (!Progress.OpenDoor()) { hud.Notify("Нужны все пять ключей: " + Progress.KeyCount + " / 5"); return; }
                SaveNow(); StartBattle(); return;
            }
            if (Station != Station.None) OpenHouse();
        }
        private void StartBattle()
        {
            if (!InBoss) arenaFuel = Fuel;
            else Fuel = arenaFuel;
            boss.Enter(Progress.hasWeapon); battleResultShown = false;
            hud.ClosePanel(); MenuOpen = false; Progress.health = MaxHealth;
            Teleport(BossEncounter.Spawn); SetView(0, 0); BuildTool(); Engage();
            hud.FadeIn(.9f); nextHit = Time.time + .3f;
        }
        public void RetryBoss()
        {
            if (!InBoss || Battle.Phase != BattlePhase.Lost) return;
            // A retry restores the arena-start petrol reserve; it never creates fuel for the mine.
            StartBattle();
        }
        public void ReturnFromBoss()
        {
            if (!InBoss) return;
            boss.Exit(); hud.ClosePanel(); Teleport(Yard.HomeSpawn); SetView(Yard.HomeYaw, 5);
            Progress.health = MaxHealth; Fuel = FuelMax; BuildTool(); Engage(); hud.FadeIn(.8f); SaveNow();
        }
        private void UpdateExpedition(float dt)
        {
            yard.SetSeals(Progress.keys);
            Refuelling = !InBoss && (Yard.InsideHouse(body.transform.position) || Yard.AtFuelPump(body.transform.position)) && Fuel < FuelMax;
            if (Refuelling && Active) { Progress.Refill(config.refillRate * dt, config); saveDirty = true; }
            if (modelDrill != UsingDrill || modelWeapon != (InBoss && Progress.hasWeapon)) BuildTool();
            // Slams and roars shake the view.
            float shake = Mathf.Max(InBoss ? boss.Shake : 0, blastShake);
            head.localPosition = Vector3.up * EyeHeight + (Vector3)Random.insideUnitCircle * shake * .07f;
            if (!InBoss) return;
            // The monster animates behind menus too; the fight itself only runs while playing.
            float damage = boss.Tick(dt, body.transform.position, Active);
            if (damage > 0)
            {
                hud.Hurt(damage / MaxHealth); sound.Play("hurt", .85f); saveDirty = true;
                if (Progress.Hurt(damage, config)) LoseBattle();
            }
            if (Battle.Phase == BattlePhase.Won && !Progress.finished)
            {
                // The victory is saved at once; the result card waits until the monster has sunk.
                Progress.finished = true; SaveNow(); sound.Play("door", 1, 1, 0);
                hud.Announce(UiGlyph.Kind.Key, new Color(.4f, 1, .9f), "ПОСЛЕДНЯЯ ПЕЧАТЬ", "Ктулху повержен", "Древнее чудовище уходит обратно в глубину.");
            }
            if (Battle.Phase == BattlePhase.Won && !battleResultShown && boss.DeathDone)
            {
                battleResultShown = true; ReleaseMouse(); hud.ShowBattleResult(true);
            }
        }
        private void LoseBattle()
        {
            Battle.Lose(); battleResultShown = true; ReleaseMouse(); hud.ShowBattleResult(false);
        }
        private void FireHarpoon()
        {
            nextHit = Time.time + .45f;
            if (!Progress.hasWeapon || Battle.Phase == BattlePhase.Won || Battle.Phase == BattlePhase.Lost) return;
            swing = 0; var origin = view.transform.position; var direction = view.transform.forward;
            var to = origin + direction * 35;
            if (Physics.Raycast(origin, direction, out var hit, 35, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                to = hit.point; float damage = boss.Shoot(hit.collider);
                if (damage > 0) { hud.HitFeedback(); hud.Popup(to, "-" + damage, Amber); Burst(to, -direction, new Color(.4f, 1, .9f), 4); }
            }
            boss.Trace(origin + view.transform.right * .18f - view.transform.up * .12f, to);
            sound.Play("harpoon", .8f);
        }
        private void BuildSealKey(LootItem item, Transform parent)
        {
            parent.rotation = Quaternion.identity;
            var color = item.Color;
            shapes.Box("Seal key shaft", new Vector3(0, -.03f, 0), new Vector3(.085f, .46f, .07f), color, parent, false, .55f);
            foreach (float y in new[] { -.21f, -.10f }) shapes.Box("Seal key tooth", new Vector3(.10f, y, 0), new Vector3(.17f, .07f, .07f), color, parent, false, .55f);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                shapes.Box("Seal key bow", new Vector3(Mathf.Cos(angle) * .14f, .3f + Mathf.Sin(angle) * .14f, 0),
                    new Vector3(.13f, .065f, .075f), Quaternion.Euler(0, 0, i * 45 + 90), color, parent, false, .55f);
            }
        }
        private bool BuildPoweredTool()
        {
            drillRotor = null;
            if (!modelDrill && !modelWeapon) return false;
            load = new GameObject("Empty load").transform; load.SetParent(tool, false); loadParts = new Renderer[0];
            var amber = new Color(.91f, .59f, .18f); var metal = new Color(.3f, .4f, .44f);
            void Part(string name, Vector3 at, Vector3 size, Color color, Transform parent = null, float angle = 0)
            {
                var obj = shapes.Make(name, cubePrefab, at, size, Quaternion.Euler(0, 0, angle), shapes.Mat(color, .12f, false), parent ?? tool, false);
                obj.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            Part("Motor housing", new Vector3(0, 0, -.04f), new Vector3(.12f, .11f, .18f), modelWeapon ? new Color(.25f, .52f, .48f) : amber);
            Part("Metal handle", new Vector3(.07f, -.06f, -.15f), new Vector3(.035f, .12f, .09f), metal);
            Part("Glove", new Vector3(.085f, -.08f, -.15f), new Vector3(.065f, .07f, .09f), new Color(.2f, .38f, .32f));
            Part("Metal barrel", new Vector3(0, 0, .14f), new Vector3(modelWeapon ? .06f : .02f, modelWeapon ? .06f : .02f, .34f), metal);
            if (modelWeapon)
            {
                Part("Harpoon grip", new Vector3(.07f, -.075f, -.12f), new Vector3(.03f, .09f, .045f), new Color(.12f, .15f, .15f));
                Part("Harpoon sight metal", new Vector3(0, .075f, -.03f), new Vector3(.018f, .025f, .05f), metal);
                Part("Harpoon rail", new Vector3(0, .053f, .07f), new Vector3(.02f, .016f, .46f), new Color(.5f, 1, .9f));
                foreach (float x in new[] { -.06f, .06f }) Part("Harpoon prong", new Vector3(x, .03f, .23f), new Vector3(.015f, .05f, .16f), metal);
            }
            else
            {
                drillRotor = new GameObject("Drill rotor").transform; drillRotor.SetParent(tool, false);
                for (int i = 0; i < 9; i++)
                {
                    float z = .025f + i * .041f, width = Mathf.Lerp(.155f, .018f, i / 8f);
                    Part("Drill spiral", new Vector3(0, 0, z), new Vector3(width, .023f, .025f), new Color(.63f, .7f, .72f), drillRotor, i * 47);
                }
                for (int i = 0; i < 4; i++) Part("Motor vent", new Vector3(.073f, .015f, -.14f + i * .032f), new Vector3(.005f, .055f, .014f), metal);
                // Petrol tank with its cap on top, a chuck holding the bit and the pull-start grip.
                Part("Fuel tank painted", new Vector3(0, .066f, -.11f), new Vector3(.06f, .026f, .07f), new Color(.6f, .16f, .12f));
                Part("Tank cap metal", new Vector3(.016f, .082f, -.125f), new Vector3(.016f, .008f, .016f), new Color(.55f, .57f, .58f));
                Part("Drill chuck metal", new Vector3(0, 0, .045f), new Vector3(.045f, .045f, .035f), new Color(.5f, .53f, .55f));
                Part("Pull grip", new Vector3(-.075f, .04f, -.1f), new Vector3(.016f, .022f, .05f), new Color(.1f, .1f, .1f));
                Part("Fuel hose", new Vector3(-.077f, -.016f, -.16f), new Vector3(.018f, .025f, .25f), new Color(.15f, .2f, .19f));
            }
            return true;
        }
    }
}
