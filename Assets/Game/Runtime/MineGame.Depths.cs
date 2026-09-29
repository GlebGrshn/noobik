using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nubik
{
    /// <summary>
    /// The magma zone near the bottom: glowing cracks (shader), drifting embers, lava pools that burn and the meteorite
    /// that only dynamite can crack. Star metal inside it becomes ordinary loot once it is open.
    /// </summary>
    public sealed partial class MineGame
    {
        private sealed class Ember { public Transform obj; public Vector3 velocity; public float life, max; }

        private Transform meteor;
        private Collider meteorCollider;
        private Material lavaMaterial;
        private readonly List<Ember> embers = new List<Ember>();
        private float burnSoundAt, burnNoticeAt;
        private static readonly Color StarGlow = new Color(.35f, .85f, 1f);

        public bool InLava { get; private set; }
        public bool MeteorIntact => meteor != null;

        private void BuildDepths()
        {
            Shader.SetGlobalVector("_NubikMagma", new Vector4(config.zones[config.zones.Length - 1].startDepth - 2, config.zones[config.zones.Length - 1].startDepth + 4));
            lavaMaterial = new Material(prototypeMaterial) { name = "Lava" };
            lavaMaterial.SetColor("_BaseColor", new Color(.16f, .05f, .03f));
            lavaMaterial.SetColor("_Emission", new Color(1f, .38f, .08f) * 1.5f);
            lavaMaterial.SetFloat("_Lava", 1);
            lavaMaterial.SetFloat("_Surface", 0);
            var root = new GameObject("Lava pools").transform;
            foreach (var pool in Depths.Lava)
            {
                var surface = shapes.Box("Lava surface", new Vector3(pool.Center.x, pool.Surface - .2f, pool.Center.z),
                    new Vector3(pool.Half.x * 2 + .2f, .4f, pool.Half.z * 2 + .2f), lavaMaterial, root, true);
                surface.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var light = new GameObject("Lava light", typeof(Light)).GetComponent<Light>();
                light.transform.SetParent(surface.transform, false);
                light.transform.position = new Vector3(pool.Center.x, pool.Surface + .8f, pool.Center.z);
                light.type = LightType.Point; light.range = 7; light.intensity = 2.2f; light.color = new Color(1f, .45f, .15f); light.shadows = LightShadows.None;
                sound.Emitter(surface.transform, "lava", .7f, 11);
            }
            // Natural caves may already extend below the crater. Keep a visible basalt bed under the meteorite.
            shapes.Box("Meteor crater stone", new Vector3(Depths.CraterCenter.x, Depths.CraterCenter.y - Depths.CraterHalf.y - .2f, Depths.CraterCenter.z),
                new Vector3(3.6f, .4f, 3.6f), new Color(.16f, .12f, .13f), root, true);
            if (!Progress.meteor) BuildMeteor();
        }

        private void BuildMeteor()
        {
            meteor = new GameObject("Meteorite").transform;
            meteor.position = Depths.Meteor;
            var crust = new Color(.2f, .19f, .23f);
            var core = shapes.Ball("Meteor crust", Vector3.zero, Vector3.one * Depths.MeteorRadius * 2, crust, meteor, true);
            meteorCollider = core.GetComponent<Collider>();
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.05f, b = i * .7f;
                shapes.Ball("Meteor crust", new Vector3(Mathf.Cos(a) * .75f, Mathf.Sin(b) * .45f, Mathf.Sin(a) * .75f), Vector3.one * (.7f + i % 3 * .15f), crust * (.85f + i % 2 * .2f), meteor);
            }
            // Glowing seams betray the star metal inside.
            for (int i = 0; i < 7; i++)
                shapes.Box("Meteor seam", new Vector3(Mathf.Cos(i * .9f) * .95f, (i % 3 - 1) * .35f, Mathf.Sin(i * .9f) * .95f), new Vector3(.05f, .5f + i % 2 * .3f, .05f),
                    Quaternion.Euler(i * 23, i * 51, i * 37), StarGlow, meteor, false, 1);
            var light = new GameObject("Meteor glow", typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(meteor, false);
            light.transform.localPosition = new Vector3(-.8f, .9f, -.8f);
            light.type = LightType.Point; light.range = 8; light.intensity = 1.8f; light.color = StarGlow; light.shadows = LightShadows.None;
            sound.Emitter(meteor, "hum", .55f, 13);
        }

        /// <summary>Called by any blast: dynamite close enough cracks the meteorite open.</summary>
        private void BlastMeteor(Vector3 at)
        {
            if (meteor == null || Vector3.Distance(at, Depths.Meteor) > config.dynamite.power + Depths.MeteorRadius + .3f) return;
            Progress.meteor = true;
            Burst(Depths.Meteor, Vector3.up, new Color(.2f, .19f, .23f), 20);
            Burst(Depths.Meteor, Vector3.up, StarGlow, 12);
            Destroy(meteor.gameObject);
            meteor = null;
            meteorCollider = null;
            foreach (var item in loot.Items)
            {
                if (!item.Meteor || item.Taken) continue;
                item.Exposed = true;
                if (item.View == null) ShowItem(item);
            }
            sound.Play("chest", 1, .8f, 0);
            hud.Announce(UiGlyph.Kind.Star, StarGlow, "МЕТЕОРИТ РАСКОЛОТ", "Звёздный металл", "Внутри три куска небесного металла. Каждый занимает 2 места в рюкзаке.");
            SaveNow();
        }

        private bool LootVisible(LootItem item) => !item.Meteor || Progress.meteor;

        private void UpdateDepths(float dt)
        {
            var feet = body.transform.position;
            InLava = false;
            if (!InBoss && feet.y < -config.zones[config.zones.Length - 1].startDepth + 2)
                foreach (var pool in Depths.Lava)
                    if (pool.Burns(feet)) { InLava = true; break; }
            if (InLava && Active)
            {
                float damage = config.lavaDamage * dt;
                hud.Hurt(.25f);
                saveDirty = true;
                if (Time.time > burnSoundAt) { sound.Play("sizzle", .8f); burnSoundAt = Time.time + .45f; }
                if (Time.time > burnNoticeAt) { hud.Notify("Лава! Выбирайся — джетпак или ступеньки"); burnNoticeAt = Time.time + 3; }
                if (Progress.Hurt(damage, config)) WakeAtHome("ЛАВА", "Спасатели вытащили тебя из огня", true);
            }
            UpdateEmbers(dt);
        }

        /// <summary>Sparks drifting up around the eye in the magma zone.</summary>
        private void UpdateEmbers(float dt)
        {
            var eye = view.transform.position;
            bool hot = !InBoss && -eye.y > config.zones[config.zones.Length - 1].startDepth - 3;
            if (hot && embers.Count == 0)
                for (int i = 0; i < 22; i++)
                {
                    var obj = shapes.Box("Ember", eye, Vector3.one * .03f, new Color(1f, .5f, .15f), null, false, 1).transform;
                    obj.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    embers.Add(new Ember { obj = obj });
                }
            foreach (var ember in embers)
            {
                ember.obj.gameObject.SetActive(hot);
                if (!hot) continue;
                ember.life -= dt;
                if (ember.life <= 0)
                {
                    ember.max = ember.life = Random.Range(2f, 4f);
                    ember.obj.position = eye + new Vector3(Random.Range(-5f, 5f), Random.Range(-3f, 1f), Random.Range(-5f, 5f));
                    ember.velocity = new Vector3(Random.Range(-.3f, .3f), Random.Range(.5f, 1.2f), Random.Range(-.3f, .3f));
                }
                ember.velocity += new Vector3(Mathf.Sin(Time.time * 1.3f + ember.max * 7) * .4f, 0, Mathf.Cos(Time.time * 1.1f + ember.max * 5) * .4f) * dt;
                ember.obj.position += ember.velocity * dt;
                ember.obj.localScale = Vector3.one * .035f * Mathf.Sin(Mathf.Clamp01(ember.life / ember.max) * Mathf.PI);
            }
        }
    }
}
