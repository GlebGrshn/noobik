using System.Collections.Generic;
using UnityEngine;

namespace Nubik
{
    /// <summary>
    /// The chamber beyond the five-seal door. Built once and enabled only while inside.
    /// Cthulhu is rigged from pivots (body, chest, head, arms, wings) and posed every frame from the battle state:
    /// it sleeps until the harpoon is taken, winds up before each attack, slams when it lands, flinches when hit,
    /// rages below half health and sinks into the floor when defeated.
    /// </summary>
    public sealed class BossEncounter
    {
        public static readonly Vector3 Origin = new Vector3(55, -118, 0);
        public static Vector3 Spawn => Origin + new Vector3(0, .1f, -8.8f);
        public static Vector3 WeaponPoint => Origin + new Vector3(0, .8f, -6.7f);
        public BossBattle Battle { get; private set; } = new BossBattle();
        public bool Inside { get; private set; }
        /// <summary>Camera shake from slams and roars, 0..1.</summary>
        public float Shake { get; private set; }
        /// <summary>The defeated monster has finished sinking.</summary>
        public bool DeathDone => wonAt >= 0 && age - wonAt > DeathTime;

        private const float DeathTime = 3.4f;
        private static readonly Color EyeCalm = new Color(1, .7f, .24f), EyeRage = new Color(1, .26f, .12f);
        private readonly Transform root, monster, weapon, wave, beam;
        private readonly Transform[] rings = new Transform[3], impacts = new Transform[3];
        /// <summary>Sound cues for the game to play: name and volume.</summary>
        public System.Action<string, float> Sound;
        private readonly Transform body, chest, head;
        private readonly Transform[] arms = new Transform[2], wings = new Transform[2];
        private readonly Vector3 chestRest, headRest;
        private readonly List<Transform> tendrils = new List<Transform>();
        private readonly List<Vector3> rests = new List<Vector3>();
        private readonly HashSet<Collider> bodyParts = new HashSet<Collider>();
        private readonly HashSet<Collider> headParts = new HashSet<Collider>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly Renderer[] eyes;
        private readonly Material eyeMaterial;
        private readonly Shapes shapes;
        private float age, beamTime;

        // Pose, eased towards the battle state every frame.
        private float lean, roll, headYaw, headPitch, curl, eyeOpen, glow;
        private readonly float[] armUp = new float[2];
        // Moments that drive short, sharp motions.
        private float slamAt, hurtAt, roarAt, wonAt;
        private int slamArm, landedSeen, slamPattern, warnedAttack = -1;
        private bool slamCircle, wasEnraged;
        private BattlePhase lastPhase;

        public BossEncounter(Shapes shapes)
        {
            this.shapes = shapes;
            root = new GameObject("Cthulhu sanctum").transform; root.position = Origin;
            var stone = new Color(.19f, .27f, .29f);
            shapes.Box("Carved sanctum floor", new Vector3(0, -.22f, 0), new Vector3(21, .44f, 25), stone, root, true);
            foreach (float x in new[] { -10.5f, 10.5f })
                shapes.Box("Sanctum wall", new Vector3(x, 4, 0), new Vector3(.6f, 8, 25), stone * .72f, root, true);
            foreach (float z in new[] { -12.5f, 12.5f })
                shapes.Box("Sanctum wall", new Vector3(0, 4, z), new Vector3(21, 8, .6f), stone * .72f, root, true);
            shapes.Box("Sanctum ceiling", new Vector3(0, 8.1f, 0), new Vector3(21, .5f, 25), stone * .55f, root, true);
            for (int i = 0; i < 6; i++)
                foreach (float x in new[] { -9.5f, 9.5f })
                {
                    float z = -10 + i * 4;
                    shapes.Box("Carved pillar", new Vector3(x, 3, z), new Vector3(.8f, 6, .8f), stone, root);
                    shapes.Box("Rune slit", new Vector3(x * .985f, 2.5f, z - .43f), new Vector3(.22f, 2.5f, .04f), new Color(.21f, .8f, .71f), root, false, .7f);
                }
            for (int i = 0; i < 5; i++)
                shapes.Box("Door seal", new Vector3((i - 2) * .7f, 2.2f, -12.12f), new Vector3(.3f, .5f, .05f), Expedition.Keys[i].Color, root, false, .6f);
            shapes.Box("Weapon pedestal", new Vector3(0, .32f, -6.7f), new Vector3(1.3f, .64f, .8f), stone * 1.5f, root);
            weapon = new GameObject("Ancient harpoon").transform; weapon.SetParent(root, false); weapon.localPosition = new Vector3(0, .9f, -6.7f);
            shapes.Box("Harpoon stock", Vector3.zero, new Vector3(.95f, .17f, .24f), new Color(.34f, .49f, .46f), weapon);
            shapes.Box("Harpoon rail", new Vector3(.15f, .13f, 0), new Vector3(1.25f, .05f, .07f), new Color(.62f, .95f, .9f), weapon, false, .6f);

            // The rig: pivots sit at the joints; parts are built in monster space and then attached, keeping their places.
            monster = new GameObject("Cthulhu").transform; monster.SetParent(root, false);
            body = Pivot("Cthulhu hips", monster, new Vector3(0, .2f, 7.1f));
            chest = Pivot("Cthulhu chest", monster, chestRest = new Vector3(0, 3.4f, 7));
            head = Pivot("Cthulhu neck", chest, headRest = new Vector3(0, 4.3f, 6.4f));
            var skin = new Color(.24f, .53f, .40f);
            Body(Attach(shapes.Ball("Cthulhu torso", new Vector3(0, 2.8f, 7.1f), new Vector3(4.7f, 5.2f, 3.5f), skin, monster, true), body), false);
            Body(Attach(shapes.Ball("Cthulhu head", new Vector3(0, 5, 6.1f), new Vector3(3.7f, 3.5f, 3.1f), skin * 1.15f, monster, true), head), true);
            var eyeList = new List<Renderer>();
            foreach (float x in new[] { -.84f, .84f })
            {
                var eye = Attach(shapes.Ball("Cthulhu eye", new Vector3(x, 5.18f, 4.73f), new Vector3(.65f, .32f, .28f), EyeCalm, monster, false, .9f), head);
                eyeList.Add(eye.GetComponent<Renderer>());
                // The slit pupil rides on the eye and closes with it.
                Attach(shapes.Ball("Cthulhu pupil", new Vector3(x, 5.18f, 4.6f), new Vector3(.13f, .26f, .06f), new Color(.08f, .03f, .02f), monster), eye.transform);
                Attach(shapes.Box("Cthulhu brow", new Vector3(x, 5.47f, 4.73f), new Vector3(.95f, .18f, .35f), Quaternion.Euler(0, 0, x > 0 ? 15 : -15), skin * .7f, monster), head);
            }
            eyes = eyeList.ToArray();
            // Its own material: the glow changes every frame and must not touch anything else of that colour.
            eyeMaterial = new Material(eyes[0].sharedMaterial) { name = "Cthulhu eyes" };
            foreach (var eye in eyes) eye.sharedMaterial = eyeMaterial;
            for (int side = -1; side <= 1; side += 2)
            {
                int i = side < 0 ? 0 : 1;
                arms[i] = Pivot("Cthulhu shoulder", chest, new Vector3(side * 2.3f, 3.7f, 6.5f));
                Attach(shapes.Ball("Cthulhu arm", new Vector3(side * 2.5f, 2.3f, 6.3f), new Vector3(1.5f, 3.3f, 1.6f), skin, monster), arms[i]);
                Attach(shapes.Ball("Cthulhu claw", new Vector3(side * 2.7f, .7f, 5.7f), new Vector3(1.8f, .9f, 1.9f), skin * .75f, monster), arms[i]);
                for (int finger = 0; finger < 3; finger++)
                    Attach(shapes.Box("Cthulhu claw tip", new Vector3(side * (2.2f + finger * .42f), .36f, 4.8f), new Vector3(.18f, .22f, .7f), new Color(.56f, .64f, .48f), monster), arms[i]);
                wings[i] = Pivot("Cthulhu wing root", chest, new Vector3(side * 1.4f, 4, 7.5f));
                Wing(side, skin * .65f, wings[i]);
            }
            for (int n = 0; n < 7; n++)
                for (int segment = 0; segment < 7; segment++)
                {
                    float t = segment / 6f, x = (n - 3) * .4f;
                    var p = new Vector3(x + Mathf.Sin(n * 2 + t * 3) * t * .65f, 4.4f - t * (2.4f + n % 2 * .5f), 4.8f - Mathf.Sin(t * 2) * .95f);
                    var obj = Attach(shapes.Ball("Cthulhu tentacle", p, Vector3.one * Mathf.Lerp(.52f, .17f, t), skin * (1 - n % 2 * .13f), monster), head);
                    tendrils.Add(obj.transform); rests.Add(obj.transform.localPosition);
                }
            for (int i = 0; i < rings.Length; i++)
            {
                rings[i] = MakeRing("Tentacle warning", new Color(1, .28f, .13f), 2.6f, 2.3f);
                impacts[i] = MakeRing("Tentacle impact", new Color(.75f, .95f, .9f), 2.7f, 2.5f);
            }
            wave = shapes.Box("Wave warning", Vector3.zero, new Vector3(9.7f, .025f, 24), new Color(.84f, .23f, .15f), root, false, .6f).transform;
            beam = shapes.Box("Harpoon trail", Vector3.zero, new Vector3(.035f, .035f, 1), new Color(.47f, 1, .95f), root, false, .9f).transform;
            foreach (var p in new[] { new Vector3(-5, 5, -3), new Vector3(5, 5, 5) })
            {
                var obj = new GameObject("Sanctum light", typeof(Light)); obj.transform.SetParent(root, false); obj.transform.localPosition = p;
                var light = obj.GetComponent<Light>(); light.type = LightType.Point; light.range = 19; light.intensity = 2.7f; light.color = new Color(.38f, .76f, .73f);
            }
            root.gameObject.SetActive(false);
        }

        private static Transform Pivot(string name, Transform parent, Vector3 monsterSpace)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            // Nothing is rotated or scaled while building, so positions simply add up along the chain.
            pivot.localPosition = monsterSpace - MonsterSpace(parent);
            return pivot;
        }

        private static Vector3 MonsterSpace(Transform t)
        {
            var sum = Vector3.zero;
            for (; t != null && t.name != "Cthulhu"; t = t.parent) sum += t.localPosition;
            return sum;
        }

        private static GameObject Attach(GameObject part, Transform pivot)
        {
            part.transform.SetParent(pivot, true);
            return part;
        }

        private void Body(GameObject obj, bool isHead)
        {
            var collider = obj.GetComponent<Collider>(); collider.isTrigger = true;
            bodyParts.Add(collider); if (isHead) headParts.Add(collider);
        }

        private void Wing(int side, Color color, Transform pivot)
        {
            var mesh = new Mesh { name = "Cthulhu wing" };
            mesh.vertices = new[] { new Vector3(side * 1.4f, 4, 7.5f), new Vector3(side * 6, 6.8f, 9), new Vector3(side * 5.3f, 2.3f, 8.4f), new Vector3(side * 3.8f, 3, 8), new Vector3(side * 2.2f, 1.8f, 7.8f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 2, 1, 0, 3, 2, 0, 4, 3, 0 }; mesh.RecalculateNormals(); meshes.Add(mesh);
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            var obj = new GameObject("Cthulhu wing", typeof(MeshFilter), typeof(MeshRenderer)); obj.transform.SetParent(monster, false);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh; obj.GetComponent<MeshRenderer>().sharedMaterial = shapes.Mat(color);
            Attach(obj, pivot);
            var start = new Vector3(side * 1.4f, 4, 7.45f);
            foreach (var end in new[] { new Vector3(side * 6, 6.8f, 8.95f), new Vector3(side * 5.3f, 2.3f, 8.35f) })
                Attach(shapes.Box("Cthulhu wing rib", (start + end) / 2, new Vector3(.14f, .14f, Vector3.Distance(start, end)), Quaternion.LookRotation(end - start), color * 1.6f, monster), pivot);
        }

        private Transform MakeRing(string name, Color color, float outer, float inner)
        {
            var mesh = new Mesh { name = name }; var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i <= 48; i++)
            {
                float a = i * Mathf.PI * 2 / 48; var p = new Vector3(Mathf.Cos(a), .01f, Mathf.Sin(a));
                vertices.Add(p * outer); vertices.Add(p * inner);
                if (i == 48) continue; int j = i * 2;
                triangles.AddRange(new[] { j, j + 1, j + 2, j + 1, j + 3, j + 2 });
            }
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); meshes.Add(mesh);
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); obj.transform.SetParent(root, false);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh; obj.GetComponent<MeshRenderer>().sharedMaterial = shapes.Mat(color, .9f);
            obj.SetActive(false);
            return obj.transform;
        }

        public void Enter(bool armed)
        {
            Battle = new BossBattle(); age = 0; Inside = true; root.gameObject.SetActive(true);
            monster.gameObject.SetActive(true); monster.localPosition = Vector3.zero; monster.localRotation = Quaternion.identity;
            weapon.gameObject.SetActive(!armed);
            for (int i = 0; i < rings.Length; i++) { rings[i].gameObject.SetActive(false); impacts[i].gameObject.SetActive(false); }
            wave.gameObject.SetActive(false); beam.gameObject.SetActive(false);
            slamAt = hurtAt = roarAt = -10; wonAt = -1; Shake = 0; wasEnraged = false; lastPhase = BattlePhase.Waiting;
            landedSeen = 0; warnedAttack = -1;
            // Asleep until the harpoon leaves its pedestal; already awake on a retry.
            lean = armed ? 0 : -12; headPitch = armed ? -12 : -26; headYaw = roll = curl = 0; eyeOpen = armed ? 1 : .1f; glow = armed ? .6f : .12f;
            armUp[0] = armUp[1] = 0;
            if (armed) Battle.Begin();
            Pose(0, Spawn);
        }

        public void Arm() { weapon.gameObject.SetActive(false); Battle.Begin(); roarAt = age; Shake = .45f; Sound?.Invoke("roar", 1); }
        public void Exit() { Inside = false; root.gameObject.SetActive(false); }
        public bool IsBoss(Collider collider) => bodyParts.Contains(collider);

        public float Shoot(Collider collider)
        {
            if (!IsBoss(collider)) return 0;
            float damage = Battle.Shoot(headParts.Contains(collider));
            if (damage > 0) { hurtAt = age; Sound?.Invoke("boss_hit", headParts.Contains(collider) ? .9f : .6f); }
            return damage;
        }

        public void Trace(Vector3 from, Vector3 to)
        {
            beam.position = (from + to) / 2; beam.rotation = Quaternion.LookRotation(to - from);
            beam.localScale = new Vector3(.035f, .035f, Vector3.Distance(from, to)); beam.gameObject.SetActive(true); beamTime = .12f;
        }

        /// <summary>
        /// Advances the fight when <paramref name="live"/> (the player is actually playing) and always animates,
        /// so the monster keeps breathing behind menus and finishes its death behind the result card.
        /// </summary>
        public float Tick(float dt, Vector3 player, bool live = true)
        {
            age += dt; beamTime -= dt; if (beamTime <= 0) beam.gameObject.SetActive(false);
            float damage = 0;
            if (live) damage = Battle.Tick(dt, player - Origin);
            if (Battle.Landed != landedSeen)
            {
                // An attack came down: tentacles whip, an arm sweeps, or both arms pound the floor.
                landedSeen = Battle.Landed;
                slamAt = age; slamPattern = Battle.LastLanded;
                slamCircle = slamPattern == BossBattle.Circle || slamPattern == BossBattle.Rain;
                slamArm = slamPattern == BossBattle.WaveLeft ? 0 : slamPattern == BossBattle.WaveRight ? 1 : -1;
                Shake = Mathf.Max(Shake, slamPattern == BossBattle.Quake ? .95f : slamCircle ? .45f : .7f);
                Sound?.Invoke("slam", slamPattern == BossBattle.Quake ? 1 : .8f);
            }
            int attackId = Battle.Landed * 2 + (Battle.Chained ? 1 : 0);
            if (Battle.Phase == BattlePhase.Warning && warnedAttack != attackId) { warnedAttack = attackId; Sound?.Invoke("warn", .55f); }
            if (Battle.Phase != lastPhase)
            {
                if (Battle.Phase == BattlePhase.Won) { wonAt = age; Shake = .8f; Sound?.Invoke("roar", 1); }
                if (Battle.Phase == BattlePhase.Lost) { roarAt = age; Sound?.Invoke("roar", .8f); }
                lastPhase = Battle.Phase;
            }
            if (Battle.Enraged && !wasEnraged && Battle.Phase != BattlePhase.Won) { wasEnraged = true; roarAt = age; Shake = Mathf.Max(Shake, .6f); Sound?.Invoke("roar", 1); }
            Shake = Mathf.Max(0, Shake - dt * 1.6f);

            bool warning = Battle.Phase == BattlePhase.Warning;
            float warn = Warn;
            float sinceSlam = age - slamAt;
            bool circles = warning && (Battle.Pattern == BossBattle.Circle || Battle.Pattern == BossBattle.Rain);
            float size = (Battle.Pattern == BossBattle.Rain ? BossBattle.RainRadius : BossBattle.CircleRadius) / BossBattle.CircleRadius;
            float slamSize = (slamPattern == BossBattle.Rain ? BossBattle.RainRadius : BossBattle.CircleRadius) / BossBattle.CircleRadius;
            for (int i = 0; i < rings.Length; i++)
            {
                // Circles close in from wide to their real size, then tremble right before the hit.
                rings[i].gameObject.SetActive(circles && i < Battle.TargetCount);
                rings[i].localPosition = new Vector3(Battle.Targets[i].x, .04f + i * .003f, Battle.Targets[i].y);
                rings[i].localScale = Vector3.one * size * (Mathf.Lerp(1.35f, 1, Mathf.Clamp01(warn * 3)) + (warn > .75f ? Mathf.Sin(age * 50 + i) * .015f : 0));
                int count = slamPattern == BossBattle.Rain ? 3 : 1;
                impacts[i].gameObject.SetActive(slamCircle && sinceSlam < .35f && i < count);
                impacts[i].localPosition = new Vector3(Battle.Targets[i].x, .05f, Battle.Targets[i].y);
                impacts[i].localScale = Vector3.one * slamSize * (1 + sinceSlam * 1.6f);
            }
            // Waves light half of the floor, the quake all of it; the mark stays a moment after the hit so it reads.
            bool quake = warning ? Battle.Pattern == BossBattle.Quake : slamPattern == BossBattle.Quake;
            bool waves = warning ? Battle.Pattern == BossBattle.WaveLeft || Battle.Pattern == BossBattle.WaveRight || quake : !slamCircle && sinceSlam < .2f;
            wave.gameObject.SetActive(waves);
            int wavePattern = warning ? Battle.Pattern : slamPattern;
            wave.localPosition = new Vector3(quake ? 0 : wavePattern == BossBattle.WaveLeft ? -5.3f : 5.3f, .02f + (warning ? 0 : .01f), 0);
            wave.localScale = new Vector3(quake ? 20.4f : 9.7f, .025f, 24);

            Pose(dt, player);
            return damage;
        }

        private float Warn => Battle.Phase == BattlePhase.Warning ? Mathf.Clamp01(1 - Battle.Remaining / Mathf.Max(.01f, Battle.Duration)) : 0;

        private static float Ease(float current, float target, float rate, float dt) => Mathf.Lerp(current, target, 1 - Mathf.Exp(-rate * dt));

        private void Pose(float dt, Vector3 player)
        {
            var phase = Battle.Phase;
            bool asleep = phase == BattlePhase.Waiting;
            float warn = Warn, rage = Battle.Enraged && phase != BattlePhase.Won ? 1 : 0, tempo = 1 + rage * .6f;
            // Short motions: strike 0.12 s in, settle over 0.9 s; flinch 0.3 s; roar 1.6 s; death over DeathTime.
            float since = age - slamAt;
            float strike = since < 0 ? 0 : since < .12f ? since / .12f : Mathf.Clamp01(1 - (since - .12f) / .9f);
            float hurt = Mathf.Clamp01(1 - (age - hurtAt) / .3f);
            float roar = Mathf.Clamp01(1 - (age - roarAt) / 1.6f) * Mathf.Clamp01((age - roarAt) / .25f);
            float death = wonAt < 0 ? 0 : Mathf.Clamp01((age - wonAt) / DeathTime);
            float breath = Mathf.Sin(age * (asleep ? .7f : 1.3f * tempo));
            bool circle = phase == BattlePhase.Warning && (Battle.Pattern == BossBattle.Circle || Battle.Pattern == BossBattle.Rain);
            bool quake = phase == BattlePhase.Warning && Battle.Pattern == BossBattle.Quake;
            int windArm = phase == BattlePhase.Warning && (Battle.Pattern == BossBattle.WaveLeft || Battle.Pattern == BossBattle.WaveRight) ? Battle.Pattern - 1 : -1;
            float circleStrike = slamCircle ? strike : 0, waveStrike = slamCircle ? 0 : strike;

            // Head follows the player within the neck's reach; sleeps and dies looking down.
            var local = monster.InverseTransformPoint(player + Vector3.up * 1.5f) - headRest;
            float yawTarget = Mathf.Clamp(-Mathf.Atan2(local.x, -local.z) * Mathf.Rad2Deg, -40, 40);
            float pitchTarget = Mathf.Clamp(Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -32, 12);
            if (asleep) { yawTarget = 0; pitchTarget = -26; }
            if (death > 0) { yawTarget = 0; pitchTarget = -34; }
            headYaw = Ease(headYaw, yawTarget, 3.5f * tempo, dt);
            headPitch = Ease(headPitch, pitchTarget + (phase == BattlePhase.Recovery ? -6 : 0), 3, dt);

            // Chest: slumps asleep, rears back before the tentacles fall, leans into the slam, rears to roar.
            float leanTarget = asleep ? -12 : circle || quake ? warn * 12 : 0;
            float rollTarget = windArm < 0 ? 0 : (windArm == 0 ? 1 : -1) * warn * 7;
            lean = Ease(lean, leanTarget, 4, dt);
            roll = Ease(roll, rollTarget, 5, dt);
            curl = Ease(curl, circle ? warn : 0, 7, dt);
            for (int i = 0; i < 2; i++) armUp[i] = Ease(armUp[i], windArm == i || quake ? warn : circle ? warn * .3f : 0, 8, dt);
            float eyeTarget = asleep ? .1f : phase == BattlePhase.Warning ? .6f : phase == BattlePhase.Recovery ? 1.35f : 1;
            float glowTarget = asleep ? .12f : phase == BattlePhase.Warning ? .7f : phase == BattlePhase.Recovery ? 2.3f : 1;
            eyeOpen = Ease(eyeOpen, eyeTarget, 6, dt);
            glow = Ease(glow, glowTarget, 5, dt);

            body.localScale = new Vector3(1 + .018f * breath, 1 + .03f * breath, 1 + .018f * breath);
            chest.localPosition = chestRest + Vector3.up * (.07f * breath - death * .6f);
            float sway = asleep ? .3f : 1 + rage * .5f;
            chest.localRotation = Quaternion.Euler(lean - circleStrike * 18 + roar * 10 + hurt * 3 - death * 22,
                Mathf.Sin(age * .5f) * 2.5f * sway, roll + Mathf.Sin(age * .8f) * 1.5f * sway + (waveStrike > 0 && slamArm >= 0 ? (slamArm == 0 ? -1 : 1) * waveStrike * 6 : 0));
            head.localRotation = Quaternion.Euler(headPitch + hurt * 12 + roar * 18, headYaw, Mathf.Sin(age * 60) * hurt * 3);

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1 : 1;
                // Raised high to wind up, then swept forward and down onto its half of the floor.
                float swingDown = !slamCircle && (slamArm == i || slamArm < 0) ? waveStrike : circleStrike * .4f;
                float raised = Mathf.Lerp(10 + Mathf.Sin(age * .9f + i) * 3, 150, armUp[i]) + roar * 35 - death * 8;
                float z = side * Mathf.Lerp(raised, 30, swingDown);
                float x = Mathf.Lerp(-armUp[i] * 25 - roar * 10, 70, swingDown) + death * 20;
                arms[i].localRotation = Quaternion.Euler(x, 0, z);
                // Wings beat slowly, faster in rage, spread wide to roar and fold in death.
                float flap = Mathf.Sin(age * (1.1f + rage * 1.2f) * Mathf.PI * .5f) * (asleep ? 3 : 10 + rage * 8);
                float spread = roar * 28 + warn * 10 - death * 30;
                wings[i].localRotation = Quaternion.Euler(0, side * (6 + flap + spread), side * (flap * .45f + spread * .3f));
            }

            // Face tentacles: a travelling wave, curled up before they fall, whipped at the ground, limp in death.
            for (int k = 0; k < tendrils.Count; k++)
            {
                int n = k / 7;
                float t = k % 7 / 6f, spreadX = n - 3;
                var offset = new Vector3(Mathf.Sin(age * 2.3f * tempo - t * 4 + n * .9f) * .22f * t, Mathf.Cos(age * 1.7f * tempo - t * 3 + n) * .08f * t,
                    Mathf.Sin(age * 1.4f + n * 1.3f) * .1f * t);
                offset += new Vector3(spreadX * .18f * t, t * t * 1.7f, -t * .7f) * curl;
                offset += new Vector3(spreadX * .1f * t, -t * t * 1.1f, -t * 2.2f) * circleStrike;
                offset += new Vector3(0, -t * t * 1.3f, t * .35f) * Mathf.Max(death, asleep ? .35f : 0);
                tendrils[k].localPosition = rests[k] + offset;
            }

            var eyeColor = Color.Lerp(EyeCalm, EyeRage, rage);
            foreach (var eye in eyes) eye.transform.localScale = new Vector3(.65f, .32f * Mathf.Max(.05f, eyeOpen * (1 - death)), .28f);
            eyeMaterial.SetColor("_Emission", (eyeColor * glow + Color.white * hurt * 2.5f) * (1 - death));

            // Death: shudders, slumps forward and sinks through the floor.
            float sink = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.25f, 1, death));
            float shudder = death > 0 && death < .6f ? Mathf.Sin(age * 45) * .09f * (1 - death) : 0;
            monster.localPosition = new Vector3(shudder, -sink * 7.6f, 0);
            monster.localRotation = Quaternion.Euler(-death * 12, 0, shudder * 20);
            if (DeathDone && monster.gameObject.activeSelf) monster.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            foreach (var mesh in meshes) Object.Destroy(mesh);
            if (eyeMaterial != null) Object.Destroy(eyeMaterial);
        }
    }
}
