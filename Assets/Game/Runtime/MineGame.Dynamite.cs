using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nubik
{
    /// <summary>
    /// Dynamite: bought in the workshop, rides in the backpack, thrown with G. The lit bundle flies in an arc,
    /// bounces off walls, settles and blows a sphere out of any rock above the bedrock. Standing close hurts.
    /// Finds in the blast are uncovered, never destroyed.
    /// </summary>
    public sealed partial class MineGame
    {
        private sealed class Charge { public Transform obj; public Vector3 velocity; public float fuse; public bool resting; public Light spark; }
        private sealed class Blast { public Transform flash; public Light light; public readonly List<Transform> smoke = new List<Transform>(); public float age; }

        private readonly List<Charge> charges = new List<Charge>();
        private readonly List<Blast> blasts = new List<Blast>();
        private float blastShake;

        /// <summary>Where the last charge went off (tests and debugging).</summary>
        public Vector3? LastBlast { get; private set; }
        public int ChargesInFlight => charges.Count;

        public void ThrowDynamite()
        {
            if (!Active) return;
            if (InBoss) { hud.Notify("Здесь динамит бесполезен"); return; }
            if (Yard.InsideHouse(body.transform.position)) { hud.Notify("Только не в доме!"); return; }
            if (!Progress.UseDynamite()) { hud.Notify("Динамита нет — купи в мастерской"); return; }
            saveDirty = true;
            var eye = view.transform;
            var start = eye.position + eye.forward * .45f - eye.up * .12f;
            // Nothing solid between the eye and the hand: otherwise the bundle starts inside a wall.
            if (Physics.Raycast(eye.position, eye.forward, out var close, .5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                start = close.point + close.normal * .08f;
            charges.Add(new Charge { obj = BuildBundle(start), velocity = eye.forward * 9 + Vector3.up * 2.5f, fuse = config.dynamiteFuse });
            sound.Play("fuse", .8f);
            hud.Notify("Фитиль горит — отойди подальше!");
        }

        private Transform BuildBundle(Vector3 at)
        {
            var root = new GameObject("Dynamite").transform;
            root.position = at;
            var red = new Color(.78f, .16f, .12f);
            for (int i = 0; i < 3; i++)
                shapes.Box("Dynamite paper", new Vector3((i - 1) * .055f, 0, 0), new Vector3(.05f, .05f, .22f), red, root);
            shapes.Box("Dynamite tape", Vector3.zero, new Vector3(.17f, .056f, .04f), new Color(.12f, .1f, .09f), root);
            shapes.Box("Dynamite fuse", new Vector3(0, .02f, .14f), new Vector3(.012f, .012f, .08f), new Color(.85f, .8f, .65f), root);
            var spark = shapes.Ball("Dynamite spark", new Vector3(0, .02f, .18f), Vector3.one * .035f, new Color(1f, .75f, .3f), root, false, 1);
            var light = spark.AddComponent<Light>();
            light.type = LightType.Point; light.range = 2.2f; light.intensity = 1.6f; light.color = new Color(1f, .6f, .25f); light.shadows = LightShadows.None;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
            return root;
        }

        private void UpdateCharges(float dt)
        {
            for (int i = charges.Count - 1; i >= 0; i--)
            {
                var charge = charges[i];
                charge.fuse -= dt;
                if (!charge.resting)
                {
                    charge.velocity.y -= config.gravity * dt;
                    var step = charge.velocity * dt;
                    var from = charge.obj.position;
                    if (step.sqrMagnitude > 1e-8f && Physics.Raycast(from, step.normalized, out var hit, step.magnitude + .05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        charge.obj.position = hit.point + hit.normal * .05f;
                        // Floors stop it; walls knock it back a little.
                        if (hit.normal.y > .55f) charge.resting = true;
                        else charge.velocity = Vector3.Reflect(charge.velocity, hit.normal) * .3f;
                        sound.Play("dig_dirt", .25f, 1.6f);
                    }
                    else charge.obj.position += step;
                    charge.obj.Rotate(new Vector3(420, 150, 0) * dt, Space.Self);
                    if (charge.obj.position.y < config.FloorY - 2) charge.fuse = 0;
                }
                if (charge.spark == null) charge.spark = charge.obj.GetComponentInChildren<Light>();
                if (charge.spark != null) charge.spark.intensity = 1.2f + Mathf.PerlinNoise(Time.time * 18, i) * 1.4f;
                if (charge.fuse > 0) continue;
                var at = charge.obj.position;
                Destroy(charge.obj.gameObject);
                charges.RemoveAt(i);
                Explode(at);
            }
            blastShake = Mathf.Max(0, blastShake - dt * 1.8f);
            AnimateBlasts(dt);
        }

        private void Explode(Vector3 at)
        {
            LastBlast = at;
            float radius = config.dynamite.power;
            var result = terrain.Dig(at, radius, config.dynamiteDamage);
            if (result.Changed)
            {
                RebuildDirty();
                RevealLoot(at, radius);
                saveDirty = true;
            }
            BlastMeteor(at);
            Burst(at, Vector3.up, result.Rock.color, 18);
            Burst(at, Vector3.up, new Color(.3f, .28f, .26f), 10);
            sound.Play("boom", 1, 1, .08f);
            SpawnBlast(at, radius);

            // The blast reaches a little beyond the hole it digs.
            var chest = body.transform.position + Vector3.up * .9f;
            float distance = Vector3.Distance(chest, at), reach = radius + 1.8f;
            blastShake = Mathf.Max(blastShake, Mathf.Clamp01(1.2f - distance / 12f));
            hud.Flash(Mathf.Clamp01(1.1f - distance / 10f));
            if (distance >= reach || InBoss) return;
            float damage = config.dynamiteHurt * (1 - distance / reach);
            sound.Play("hurt", .9f);
            hud.Hurt(damage / MaxHealth);
            hud.Popup(view.transform.position + view.transform.forward * 1.2f - Vector3.up * .3f, "-" + Mathf.CeilToInt(damage), Danger);
            saveDirty = true;
            if (Progress.Hurt(damage, config)) WakeAtHome("ВЗРЫВ", "Ты стоял слишком близко", true);
        }

        private void SpawnBlast(Vector3 at, float radius)
        {
            var blast = new Blast();
            blast.flash = shapes.Ball("Blast flash", at, Vector3.one * .5f, new Color(1f, .62f, .2f), null, false, 1).transform;
            blast.flash.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            blast.light = blast.flash.gameObject.AddComponent<Light>();
            blast.light.type = LightType.Point; blast.light.range = radius * 5; blast.light.intensity = 6; blast.light.color = new Color(1f, .7f, .35f);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3;
                var puff = shapes.Ball("Blast smoke puff", at + new Vector3(Mathf.Cos(angle), .2f, Mathf.Sin(angle)) * .5f, Vector3.one * .6f,
                    new Color(.34f, .32f, .3f), null).transform;
                puff.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                blast.smoke.Add(puff);
            }
            blasts.Add(blast);
        }

        private void AnimateBlasts(float dt)
        {
            for (int i = blasts.Count - 1; i >= 0; i--)
            {
                var blast = blasts[i];
                blast.age += dt;
                float flash = blast.age / .2f;
                if (blast.flash != null)
                {
                    if (flash >= 1) Destroy(blast.flash.gameObject);
                    else
                    {
                        blast.flash.localScale = Vector3.one * Mathf.Lerp(.4f, config.dynamite.power * 1.1f, Mathf.Sqrt(flash));
                        blast.light.intensity = 6 * (1 - flash);
                    }
                }
                // Smoke swells, drifts up and shrinks away.
                float life = blast.age / 1.6f;
                for (int k = 0; k < blast.smoke.Count; k++)
                {
                    var puff = blast.smoke[k];
                    float angle = k * Mathf.PI / 3;
                    puff.position += (new Vector3(Mathf.Cos(angle), 1.4f, Mathf.Sin(angle)) * 1.1f) * dt;
                    puff.localScale = Vector3.one * Mathf.Max(.02f, Mathf.Sin(Mathf.Min(1, life) * Mathf.PI) * 1.6f + .3f * (1 - life));
                }
                if (life < 1) continue;
                foreach (var puff in blast.smoke) Destroy(puff.gameObject);
                blasts.RemoveAt(i);
            }
        }

        public void BuyDynamite()
        {
            if (!Progress.BuyDynamite(config)) return;
            SaveNow();
            sound.Play("buy", .8f, 1.1f, 0);
            if (Progress.dynamite == 1) hud.Notify(TouchMode ? "Динамит в рюкзаке: кнопка «Динамит»" : "Динамит в рюкзаке: клавиша G — бросить");
        }
    }
}
