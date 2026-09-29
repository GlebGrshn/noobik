using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nubik
{
    /// <summary>
    /// Life in the yard: a pond with lily pads and a frog, a vegetable bed with a scarecrow, a doghouse with a sleeping
    /// dog, a tire swing, a wheelbarrow of dug earth, a birdhouse, mushrooms, garden lamps, butterflies and birds.
    /// Still props join the merged scenery; the living ones move every frame.
    /// </summary>
    public sealed partial class Yard
    {
        /// <summary>Sound cues for the game: clip, place, volume.</summary>
        public Action<string, Vector3, float> Sound;
        public Transform Dog { get; private set; }
        public static readonly Vector3 PondCenter = new Vector3(-12, 0, -2);
        public const float PondRadius = 2.3f;

        private Transform living, dogChest, dogTail, swing, frog;
        private readonly List<Transform> butterflies = new List<Transform>(), birds = new List<Transform>();
        private readonly List<Vector3> butterflyHomes = new List<Vector3>();
        private readonly Vector3[] pads = { new Vector3(-12.9f, 0, -2.6f), new Vector3(-11.2f, 0, -1.2f), new Vector3(-11.6f, 0, -3.1f), new Vector3(-12.6f, 0, -0.9f) };
        private float time, frogNext = 6, frogHop = -1, tweetNext = 4;
        private int frogPad, frogFrom;

        private void DressGarden(Shapes s, Transform root)
        {
            var wood = new Color(.5f, .34f, .2f);
            var soil = new Color(.33f, .22f, .14f);
            var leaf = new Color(.32f, .58f, .26f);

            // Pond: a stone rim around still water, lily pads with a flower or two, reeds at the far side.
            s.Box("Pond bed stone", PondCenter + Vector3.down * .09f, new Vector3(PondRadius * 2, .02f, PondRadius * 2) * .98f, new Color(.25f, .3f, .28f), root);
            s.Ball("Pond water", PondCenter + Vector3.down * .02f, new Vector3(PondRadius * 2 - .3f, .04f, PondRadius * 2 - .6f), Quaternion.identity,
                s.Water(new Color(.18f, .36f, .42f)), root);
            for (int i = 0; i < 18; i++)
            {
                float a = i * Mathf.PI * 2 / 18;
                var at = PondCenter + new Vector3(Mathf.Cos(a) * (PondRadius - .05f), .04f, Mathf.Sin(a) * (PondRadius - .3f));
                s.Ball("Pond stone", at, new Vector3(.42f + i % 3 * .08f, .2f, .34f), new Color(.5f, .52f, .48f) * (.9f + i % 4 * .05f), root);
            }
            foreach (var pad in pads)
            {
                s.Ball("Lily pad", pad + Vector3.up * .01f, new Vector3(.42f, .015f, .42f), new Color(.24f, .52f, .26f), root);
            }
            s.Ball("Lily flower", pads[1] + new Vector3(.08f, .05f, .05f), new Vector3(.12f, .07f, .12f), new Color(1f, .72f, .82f), root);
            s.Ball("Lily flower", pads[3] + new Vector3(-.05f, .05f, .07f), new Vector3(.11f, .06f, .11f), new Color(.98f, .95f, .9f), root);
            var reedGreen = new Color(.4f, .56f, .28f);
            for (int i = 0; i < 16; i++)
            {
                float a = 2.4f + i * .12f;
                var at = PondCenter + new Vector3(Mathf.Cos(a) * (PondRadius - .45f), 0, Mathf.Sin(a) * (PondRadius - .6f));
                float h = .8f + i % 4 * .18f;
                s.Box("Reed", at + Vector3.up * h / 2, new Vector3(.03f, h, .03f), Quaternion.Euler(i % 3 * 4 - 4, i * 40, 0), reedGreen, root);
                if (i % 3 == 0) s.Box("Reed head", at + Vector3.up * (h + .08f), new Vector3(.05f, .16f, .05f), new Color(.42f, .28f, .16f), root);
            }

            // Vegetable bed with a scarecrow keeping watch.
            var bed = new Vector3(12, 0, -3);
            foreach (int side in new[] { -1, 1 })
            {
                s.Box("Bed frame wood", bed + new Vector3(0, .1f, side * 1.1f), new Vector3(3.8f, .2f, .12f), wood, root);
                s.Box("Bed frame wood", bed + new Vector3(side * 1.85f, .1f, 0), new Vector3(.12f, .2f, 2.3f), wood, root);
            }
            s.Box("Garden soil", bed + Vector3.up * .08f, new Vector3(3.6f, .12f, 2.1f), soil, root);
            for (int row = 0; row < 3; row++)
                for (int k = 0; k < 5; k++)
                {
                    var at = bed + new Vector3(-1.4f + k * .7f, .18f, -.7f + row * .7f);
                    if (row == 1) { s.Ball("Cabbage", at + Vector3.up * .06f, new Vector3(.34f, .26f, .34f), new Color(.52f, .74f, .38f), root); continue; }
                    for (int leafIndex = 0; leafIndex < 3; leafIndex++)
                        s.Box("Carrot top grass tuft", at + new Vector3(0, .1f, 0), new Vector3(.03f, .22f, .01f), Quaternion.Euler(18, leafIndex * 120, 0), leaf, root);
                    s.Ball("Carrot", at + Vector3.up * .01f, new Vector3(.07f, .05f, .07f), new Color(.95f, .5f, .15f), root);
                }
            var scarecrow = bed + new Vector3(1.2f, 0, 1.3f);
            s.Box("Scarecrow post wood", scarecrow + Vector3.up * .8f, new Vector3(.08f, 1.6f, .08f), wood, root);
            s.Box("Scarecrow arms wood", scarecrow + Vector3.up * 1.25f, new Vector3(1.2f, .07f, .07f), wood, root);
            s.Box("Scarecrow shirt cloth", scarecrow + Vector3.up * 1.12f, new Vector3(.5f, .5f, .22f), new Color(.72f, .28f, .22f), root);
            foreach (int side in new[] { -1, 1 })
                s.Box("Scarecrow sleeve cloth", scarecrow + new Vector3(side * .42f, 1.24f, 0), new Vector3(.36f, .14f, .16f), new Color(.72f, .28f, .22f), root);
            s.Ball("Scarecrow head cloth", scarecrow + Vector3.up * 1.55f, new Vector3(.3f, .32f, .3f), new Color(.9f, .8f, .55f), root);
            s.Ball("Scarecrow hat edge", scarecrow + Vector3.up * 1.7f, new Vector3(.62f, .06f, .62f), new Color(.72f, .6f, .28f), root);
            s.Ball("Scarecrow hat top", scarecrow + Vector3.up * 1.8f, new Vector3(.3f, .2f, .3f), new Color(.72f, .6f, .28f), root);
            for (int i = 0; i < 5; i++)
                s.Box("Straw grass tuft", scarecrow + new Vector3(i % 2 == 0 ? -.72f : .72f, 1.22f - i * .03f, 0), new Vector3(.02f, .14f, .02f), Quaternion.Euler(0, 0, 50 + i * 10), new Color(.95f, .82f, .42f), root);

            // A wheelbarrow of fresh earth by the dig and a spare shovel leaning on it.
            var barrow = new Vector3(8.8f, 0, -8.8f);
            var turn = Quaternion.Euler(0, 35, 0);
            s.Box("Wheelbarrow tray painted", barrow + turn * new Vector3(0, .55f, 0), new Vector3(.75f, .3f, 1.05f), turn, new Color(.24f, .48f, .38f), root);
            s.Ball("Wheelbarrow dirt", barrow + turn * new Vector3(0, .72f, -.05f), new Vector3(.62f, .26f, .82f), new Color(.46f, .3f, .18f), root);
            s.Ball("Wheelbarrow wheel tire", barrow + turn * new Vector3(0, .2f, .65f), new Vector3(.12f, .4f, .4f), turn, s.Mat(new Color(.1f, .1f, .1f)), root);
            foreach (int side in new[] { -1, 1 })
            {
                s.Box("Wheelbarrow handle wood", barrow + turn * new Vector3(side * .28f, .5f, -.85f), new Vector3(.05f, .05f, .8f), turn * Quaternion.Euler(-12, 0, 0), wood, root);
                s.Box("Wheelbarrow leg metal", barrow + turn * new Vector3(side * .28f, .2f, -.45f), new Vector3(.04f, .4f, .04f), turn, new Color(.3f, .3f, .32f), root);
            }
            s.Box("Spare shovel shaft", barrow + turn * new Vector3(.5f, .6f, .1f), new Vector3(.035f, 1.2f, .035f), turn * Quaternion.Euler(0, 0, -18), wood, root);
            s.Box("Spare shovel blade", barrow + turn * new Vector3(.68f, .08f, .1f), new Vector3(.2f, .26f, .02f), turn * Quaternion.Euler(0, 0, -18), new Color(.6f, .62f, .64f), root);

            // Birdhouse on a post; mushrooms around the old tree; lamps along the path to the house.
            var post = new Vector3(-10.4f, 0, -12.6f);
            s.Box("Birdhouse post wood", post + Vector3.up * 1.1f, new Vector3(.08f, 2.2f, .08f), wood, root);
            s.Box("Birdhouse wood", post + Vector3.up * 2.35f, new Vector3(.36f, .38f, .32f), new Color(.72f, .56f, .36f), root);
            s.Box("Birdhouse roof", post + new Vector3(.1f, 2.6f, 0), new Vector3(.28f, .04f, .42f), Quaternion.Euler(0, 0, -35), new Color(.62f, .24f, .18f), root);
            s.Box("Birdhouse roof", post + new Vector3(-.1f, 2.6f, 0), new Vector3(.28f, .04f, .42f), Quaternion.Euler(0, 0, 35), new Color(.62f, .24f, .18f), root);
            s.Ball("Birdhouse hole", post + new Vector3(0, 2.4f, -.165f), new Vector3(.1f, .1f, .02f), new Color(.12f, .08f, .05f), root);
            var tree = new Vector3(14.5f, 0, -12);
            for (int i = 0; i < 7; i++)
            {
                float a = i * .9f;
                var at = tree + new Vector3(Mathf.Cos(a) * 1.3f, 0, Mathf.Sin(a) * 1.3f);
                float h = .08f + i % 3 * .03f;
                s.Box("Mushroom stem", at + Vector3.up * h / 2, new Vector3(.04f, h, .04f), new Color(.95f, .92f, .84f), root);
                s.Ball("Mushroom cap", at + Vector3.up * h, new Vector3(.14f, .07f, .14f), i % 2 == 0 ? new Color(.85f, .2f, .15f) : new Color(.75f, .55f, .32f), root);
            }
            foreach (float x in new[] { -1.9f, 1.9f })
                foreach (float z in new[] { 9.2f, 12.2f })
                {
                    var lamp = new Vector3(x, 0, z);
                    s.Box("Lamp post metal", lamp + Vector3.up * .45f, new Vector3(.06f, .9f, .06f), new Color(.2f, .22f, .22f), root);
                    s.Box("Lamp glass", lamp + Vector3.up * .98f, new Vector3(.16f, .18f, .16f), new Color(1f, .86f, .55f), root, false, .5f);
                    s.Box("Lamp cap metal", lamp + Vector3.up * 1.1f, new Vector3(.22f, .05f, .22f), new Color(.2f, .22f, .22f), root);
                }

            // Doghouse; its dog is alive and built separately.
            var house = new Vector3(13.2f, 0, 4.2f);
            s.Box("Doghouse planks", house + Vector3.up * .45f, new Vector3(1.1f, .9f, 1f), new Color(.66f, .42f, .26f), root, true);
            s.Box("Doghouse roof", house + new Vector3(0, 1.05f, -.3f), new Vector3(1.3f, .06f, .72f), Quaternion.Euler(-38, 0, 0), new Color(.5f, .22f, .16f), root);
            s.Box("Doghouse roof", house + new Vector3(0, 1.05f, .3f), new Vector3(1.3f, .06f, .72f), Quaternion.Euler(38, 0, 0), new Color(.5f, .22f, .16f), root);
            s.Box("Doghouse door", house + new Vector3(-.56f, .32f, 0), new Vector3(.03f, .5f, .4f), new Color(.14f, .09f, .06f), root);
            s.Ball("Dog bowl metal", house + new Vector3(-1.1f, .05f, .7f), new Vector3(.26f, .08f, .26f), new Color(.6f, .64f, .7f), root);

            // A branch on the old tree for the tire swing.
            s.Box("Swing branch trunk", new Vector3(-13.2f, 3.05f, 10), new Vector3(1.8f, .16f, .16f), Quaternion.Euler(0, 0, 6), new Color(.42f, .28f, .18f), root);
        }

        private void BuildLiving(Shapes s)
        {
            living = new GameObject("Yard life").transform;
            // The dog sleeps in front of its house: the chest rises and falls, the tail twitches now and then.
            Dog = new GameObject("Dog").transform;
            Dog.SetParent(living, false);
            Dog.position = new Vector3(12.1f, 0, 4.1f);
            Dog.rotation = Quaternion.Euler(0, -90, 0);
            var fur = new Color(.78f, .56f, .32f);
            dogChest = new GameObject("Dog chest").transform;
            dogChest.SetParent(Dog, false);
            s.Ball("Dog body", new Vector3(0, .2f, 0), new Vector3(.42f, .32f, .7f), fur, dogChest);
            s.Ball("Dog head", new Vector3(.02f, .18f, .44f), new Vector3(.3f, .24f, .3f), fur, Dog);
            s.Ball("Dog muzzle", new Vector3(.02f, .14f, .6f), new Vector3(.16f, .12f, .16f), fur * 1.1f, Dog);
            s.Ball("Dog nose", new Vector3(.02f, .16f, .68f), new Vector3(.05f, .04f, .04f), new Color(.08f, .06f, .05f), Dog);
            foreach (int side in new[] { -1, 1 })
            {
                s.Ball("Dog ear", new Vector3(side * .13f, .22f, .4f), new Vector3(.08f, .16f, .12f), fur * .7f, Dog);
                s.Box("Dog closed eye", new Vector3(side * .07f, .22f, .56f), new Vector3(.05f, .01f, .01f), new Color(.1f, .07f, .05f), Dog);
                s.Ball("Dog paw", new Vector3(side * .12f, .05f, .55f), new Vector3(.1f, .07f, .16f), fur * 1.1f, Dog);
            }
            dogTail = new GameObject("Dog tail pivot").transform;
            dogTail.SetParent(Dog, false);
            dogTail.localPosition = new Vector3(0, .24f, -.34f);
            s.Box("Dog tail", new Vector3(0, 0, -.14f), new Vector3(.05f, .05f, .3f), fur * .8f, dogTail);

            // Tire swing on the branch.
            swing = new GameObject("Tire swing").transform;
            swing.SetParent(living, false);
            swing.position = new Vector3(-12.6f, 2.98f, 10);
            s.Box("Swing rope", new Vector3(0, -1.05f, 0), new Vector3(.03f, 2.1f, .03f), new Color(.72f, .62f, .42f), swing);
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2 / 10;
                s.Box("Swing tire", new Vector3(Mathf.Cos(a) * .3f, -2.25f + Mathf.Sin(a) * .3f, 0), new Vector3(.2f, .12f, .18f), Quaternion.Euler(0, 0, a * Mathf.Rad2Deg + 90),
                    new Color(.1f, .1f, .1f), swing);
            }

            // A frog that hops from pad to pad.
            frog = new GameObject("Frog").transform;
            frog.SetParent(living, false);
            frog.position = pads[0] + Vector3.up * .05f;
            var green = new Color(.36f, .62f, .22f);
            s.Ball("Frog body", new Vector3(0, .05f, 0), new Vector3(.16f, .1f, .2f), green, frog);
            foreach (int side in new[] { -1, 1 })
            {
                s.Ball("Frog eye", new Vector3(side * .05f, .1f, .06f), new Vector3(.05f, .05f, .05f), new Color(.9f, .85f, .3f), frog);
                s.Ball("Frog leg", new Vector3(side * .08f, .02f, -.05f), new Vector3(.06f, .04f, .1f), green * .85f, frog);
            }

            // Butterflies over the flower beds.
            var wings = new[] { new Color(1f, .8f, .25f), new Color(.95f, .95f, 1f), new Color(.55f, .75f, 1f), new Color(1f, .55f, .35f) };
            var homes = new[] { new Vector3(-8, 0, 13.2f), new Vector3(-2, 0, 13.3f), new Vector3(5, 0, 13.2f), new Vector3(-6, 0, -14.8f), new Vector3(7, 0, -14.8f), new Vector3(12, 0, -3) };
            for (int i = 0; i < homes.Length; i++)
            {
                var fly = new GameObject("Butterfly").transform;
                fly.SetParent(living, false);
                foreach (int side in new[] { -1, 1 })
                {
                    var wing = new GameObject("Wing").transform;
                    wing.SetParent(fly, false);
                    s.Box("Butterfly wing", new Vector3(side * .045f, 0, 0), new Vector3(.08f, .005f, .07f), wings[i % wings.Length], wing, false, .15f);
                }
                butterflies.Add(fly);
                butterflyHomes.Add(homes[i]);
            }

            // Birds circling high over the yard.
            for (int i = 0; i < 5; i++)
            {
                var bird = new GameObject("Bird").transform;
                bird.SetParent(living, false);
                foreach (int side in new[] { -1, 1 })
                {
                    var wing = new GameObject("Wing").transform;
                    wing.SetParent(bird, false);
                    s.Box("Bird wing", new Vector3(side * .35f, 0, 0), new Vector3(.7f, .03f, .18f), new Color(.18f, .18f, .2f), wing);
                }
                s.Box("Bird body", Vector3.zero, new Vector3(.12f, .1f, .4f), new Color(.18f, .18f, .2f), bird);
                birds.Add(bird);
            }
            foreach (var renderer in living.GetComponentsInChildren<Renderer>())
                if (renderer.bounds.size.magnitude < .5f) renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void AnimateLiving(float dt)
        {
            if (living == null) return;
            time += dt;
            float breath = Mathf.Sin(time * 1.4f);
            dogChest.localScale = new Vector3(1 + breath * .04f, 1 + breath * .07f, 1);
            // The tail wags in short bursts.
            float wag = Mathf.Repeat(time, 7) < 1.2f ? Mathf.Sin(time * 16) * 30 : Mathf.Sin(time * .7f) * 6;
            dogTail.localRotation = Quaternion.Euler(-20, wag, 0);
            swing.localRotation = Quaternion.Euler(Mathf.Sin(time * 1.1f) * 7, 0, Mathf.Sin(time * .8f) * 4);

            // The frog waits, then hops to another pad with a splash and a croak.
            if (frogHop < 0 && time > frogNext)
            {
                frogFrom = frogPad;
                frogPad = (frogPad + 1 + (int)(time * 7) % (pads.Length - 1)) % pads.Length;
                frogHop = 0;
                Sound?.Invoke("croak", frog.position, .7f);
            }
            if (frogHop >= 0)
            {
                frogHop += dt / .55f;
                float k = Mathf.Clamp01(frogHop);
                frog.position = Vector3.Lerp(pads[frogFrom], pads[frogPad], k) + Vector3.up * (.05f + Mathf.Sin(k * Mathf.PI) * .4f);
                var heading = pads[frogPad] - pads[frogFrom];
                if (heading.sqrMagnitude > .01f) frog.rotation = Quaternion.LookRotation(new Vector3(heading.x, 0, heading.z));
                if (k >= 1) { frogHop = -1; frogNext = time + 7 + (time * 13) % 9; Sound?.Invoke("splash", frog.position, .35f); }
            }

            for (int i = 0; i < butterflies.Count; i++)
            {
                var fly = butterflies[i];
                if (!fly.gameObject.activeSelf) continue;
                float t = time * (.5f + i * .07f) + i * 2.1f;
                var home = butterflyHomes[i];
                var at = home + new Vector3(Mathf.Sin(t) * 1.4f + Mathf.Sin(t * 2.3f) * .4f, .5f + Mathf.Sin(t * 1.7f) * .25f, Mathf.Sin(t * .8f + 1) * .9f);
                var move = at - fly.position;
                fly.position = at;
                if (move.sqrMagnitude > 1e-6f) fly.rotation = Quaternion.LookRotation(new Vector3(move.x, 0, move.z));
                float flap = Mathf.Sin(time * 22 + i) * 60;
                fly.GetChild(0).localRotation = Quaternion.Euler(0, 0, flap);
                fly.GetChild(1).localRotation = Quaternion.Euler(0, 0, -flap);
            }
            for (int i = 0; i < birds.Count; i++)
            {
                var bird = birds[i];
                if (!bird.gameObject.activeSelf) continue;
                float radius = 18 + i * 3, speed = .12f + i * .015f, a = time * speed + i * 1.3f;
                var at = new Vector3(Mathf.Cos(a) * radius, 24 + i * 2.5f + Mathf.Sin(time * .5f + i) * 1.5f, Mathf.Sin(a) * radius);
                bird.position = at;
                bird.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)));
                float flap = Mathf.Sin(time * 5 + i * 1.7f) * (Mathf.Repeat(time + i, 6) < 3 ? 28 : 6);
                bird.GetChild(0).localRotation = Quaternion.Euler(0, 0, flap);
                bird.GetChild(1).localRotation = Quaternion.Euler(0, 0, -flap);
            }
            // Now and then a bird calls from one of the trees.
            if (time > tweetNext)
            {
                tweetNext = time + 5 + (time * 17) % 7;
                var trees = new[] { new Vector3(-14, 4, 10), new Vector3(14.5f, 4, -12), new Vector3(-14.5f, 4, -12.5f), new Vector3(15, 4, 9) };
                Sound?.Invoke("tweet", trees[(int)(time * 3) % trees.Length], .6f);
            }
        }

        /// <summary>Graphics level: the lowest hides the small animated life.</summary>
        public void SetDetail(int level)
        {
            bool show = level > GameSettings.Low;
            foreach (var fly in butterflies) fly.gameObject.SetActive(show);
            foreach (var bird in birds) bird.gameObject.SetActive(show);
        }
    }
}
