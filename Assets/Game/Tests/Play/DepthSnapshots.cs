using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Nubik.PlayTests
{
    /// <summary>
    /// Smoke tests of the real scene: depths, ore, the house, falls and the jetpack. Camera frames go to
    /// TestResults/Snapshots. Run with graphics (no -nographics) to get pictures:
    /// Unity -batchmode -runTests -testPlatform PlayMode -projectPath .
    /// The editor save is kept aside and restored.
    /// </summary>
    public partial class DepthSnapshots
    {
        private const string Key = "nubik.progress.v1";
        private string saved, savedBackup;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            saved = PlayerPrefs.GetString(Key, null);
            savedBackup = PlayerPrefs.GetString(Key + ".backup", null);
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(Key + ".backup");
            yield return SceneManager.LoadSceneAsync("Mine");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestoreSave()
        {
            // Remove the test game before restoring preferences so its quit/focus callbacks cannot overwrite them.
            var scene = SceneManager.GetSceneByName("Mine");
            SceneManager.CreateScene("Test cleanup");
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Restore(Key, saved);
            Restore(Key + ".backup", savedBackup);
            PlayerPrefs.Save();
        }

        private static void Restore(string key, string value)
        {
            if (string.IsNullOrEmpty(value)) PlayerPrefs.DeleteKey(key);
            else PlayerPrefs.SetString(key, value);
        }

        [UnityTest]
        public IEnumerator EveryZoneRendersAndTheDoorIsReached()
        {
            var game = Object.FindAnyObjectByType<MineGame>();
            Assert.NotNull(game);
            yield return Frames(30);
            Shot("ui_welcome", true);
            game.SetView(0, 36);
            yield return Frames(3);
            Shot("00_surface");
            foreach (int depth in new[] { 12, 45, 90 })
            {
                game.DebugDig(depth.ToString());
                yield return Frames(8);
                Assert.AreEqual(depth, game.Depth, 1);
                game.SetView(200, 5);
                yield return Frames(3);
                Shot(depth.ToString("000") + "_wall");
            }
            game.DebugDig(game.config.depth.ToString());
            yield return Frames(8);
            Assert.IsFalse(game.Progress.finished, "Reaching the door is not the campaign ending.");
            Assert.IsFalse(game.Progress.OpenDoor(), "Five keys are required.");
            game.SetView(-14, 8);
            yield return Frames(3);
            Shot("120_door");
            // No way up but climbing, the jetpack or the rescuers.
            Assert.IsTrue(game.CanRescue);
            game.CallRescue();
            yield return Frames(30);
            Assert.AreEqual(0, game.Depth);
            Assert.IsTrue(Yard.InsideHouse(Body(game).transform.position));
            Shot("house_inside");
            game.DebugPlace(Yard.SurfaceSpawn, Yard.SurfaceYaw, 10);
            yield return Frames(30);
            Shot("99_surface_again");
        }

        [UnityTest]
        public IEnumerator RevealedOreStaysVisibleUntilHit()
        {
            var game = Object.FindAnyObjectByType<MineGame>();
            game.DebugDig("12");
            yield return Frames(3);
            var field = Field<LootField>(game, "loot");
            int rendered = 0;
            foreach (var item in field.Items)
            {
                if (item.Kind != LootKind.Find || item.View == null) continue;
                Assert.IsFalse(item.Taken, "Revealing ore must not collect it.");
                var renderers = item.View.GetComponentsInChildren<Renderer>();
                Assert.AreEqual(3, renderers.Length);
                foreach (var renderer in renderers)
                    Assert.Less(Vector3.Distance(renderer.bounds.center, item.Position), 0.25f,
                        "Nugget geometry must stay beside its hitbox (unsigned offset regression).");
                rendered++;
            }
            Assert.Greater(rendered, 0);
            Assert.AreEqual(0, game.Progress.OrePieces);

            // Aim at the starter nugget from above and exercise the actual digging ray.
            var first = field.Items.Find(item => item.Special == 0);
            game.DebugPlace(first.Position + new Vector3(0, 0.6f, 0), 0, 85);
            Physics.SyncTransforms();
            Shot("ore_before_pickup");
            Invoke(game, "Swing");
            Assert.IsTrue(first.Taken);
            Assert.AreEqual(1, game.Progress.OreCount(first.Ore));
            Invoke(game, "Swing");
            Assert.AreEqual(1, game.Progress.OrePieces, "An ore hit must pay only once.");
        }

        [UnityTest]
        public IEnumerator OreSellsOnlyAtTheBuyer()
        {
            var game = Object.FindAnyObjectByType<MineGame>();
            PlayByTouch(game);
            game.DebugDig("12");
            yield return Frames(3);
            game.Progress.ores = new int[game.config.ores.Length];
            game.Progress.ores[1] = 4;
            int coins = game.Progress.coins, value = game.Progress.BagValue(game.config);
            game.DebugPlace(Yard.SurfaceSpawn, Yard.SurfaceYaw, 5);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(value, game.Progress.BagValue(game.config), "Coming up does not sell anything.");
            Assert.AreEqual(Station.None, game.Station);
            game.DebugPlace(Yard.CounterPoint + Vector3.up * 0.15f, 0, 5);
            yield return Frames(5);
            Assert.AreEqual(Station.Counter, game.Station);
            Shot("house_counter");
            game.OpenHouse();
            yield return Frames(3);
            Shot("ui_house_sell", true);
            game.SellOre();
            Assert.AreEqual(coins + value, game.Progress.coins);
            Assert.AreEqual(0, game.Progress.OrePieces);
            game.SellOre();
            Assert.AreEqual(coins + value, game.Progress.coins, "A second sale pays nothing.");
            game.GetComponent<MineHud>().ShowHouse(MineHud.OrderPage);
            yield return Frames(3);
            Shot("ui_house_orders", true);
            game.GetComponent<MineHud>().ShowHouse(MineHud.UpgradePage);
            yield return Frames(3);
            Shot("ui_house_upgrades", true);
            game.GetComponent<MineHud>().ShowHouse(MineHud.ItemPage);
            yield return Frames(3);
            Shot("ui_house_items", true);
            game.GetComponent<MineHud>().ShowHouse(MineHud.JournalPage);
            yield return Frames(3);
            Shot("ui_house_journal", true);
        }

        [UnityTest]
        public IEnumerator ShortFallHurtsLongFallFaintsAndDropsOre()
        {
            var game = Object.FindAnyObjectByType<MineGame>();
            PlayByTouch(game);
            game.DebugDig("4");
            yield return Frames(3);
            var shaft = new Vector3(0.8f, 0.2f, -1.2f);
            game.DebugPlace(shaft, 0, 60);
            yield return new WaitForSeconds(1.5f);
            Assert.Less(game.Health, game.MaxHealth, "A four metre drop hurts.");
            Assert.Greater(game.Health, 0);

            game.DebugDig("24");
            yield return Frames(3);
            game.Progress.ores = new int[game.config.ores.Length];
            game.Progress.ores[0] = 3;
            game.DebugPlace(shaft, 0, 60);
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(0, game.Progress.OrePieces, "Fainting leaves the ore in the mine.");
            Assert.AreEqual(game.MaxHealth, game.Health, "Wakes up rested.");
            Assert.IsTrue(Yard.InsideHouse(Body(game).transform.position));
        }

        [UnityTest]
        public IEnumerator JetpackLiftsAndBurnsFuel()
        {
            var game = Object.FindAnyObjectByType<MineGame>();
            PlayByTouch(game);
            game.Progress.coins = game.Progress.NextPrice(Track.Jetpack, game.config);
            game.Buy(Track.Jetpack);
            Assert.IsTrue(game.HasJetpack);
            game.DebugDig("10");
            yield return new WaitForSeconds(0.5f);
            float start = Body(game).transform.position.y;
            var jump = game.GetComponent<MineHud>().Jump;
            jump.OnPointerDown(null);
            yield return new WaitForSeconds(1.2f);
            float top = Body(game).transform.position.y;
            jump.OnPointerUp(null);
            Assert.Greater(top - start, 2.5f, "The jetpack climbs out of the shaft.");
            Assert.Less(game.Fuel, game.FuelMax);
        }

        [UnityTest]
        public IEnumerator AbandonedSitesHaveWalkableFloorsAndPersistentDiscoveries()
        {
            var game = Object.FindAnyObjectByType<MineGame>();
            PlayByTouch(game);
            game.Progress.jetLevel = 1;
            game.Engage();
            for (int i = 0; i < MineSites.All.Length; i++)
            {
                var site = MineSites.All[i];
                game.DebugPlace(site.Origin + new Vector3(0, .3f, .4f), 0, 10);
                yield return new WaitForSeconds(.5f);
                Assert.IsTrue(game.Progress.HasSite(i));
                Assert.AreEqual(i, game.CurrentSite);
                Assert.AreEqual(site.Depth, game.Depth);
                Assert.IsTrue(Body(game).isGrounded, "The old walkway must support the player.");
                Assert.Greater(game.Health, 0);
                yield return new WaitForSeconds(3.5f);
                game.DebugPlace(site.Origin + new Vector3(0, .15f, -.8f), 0, 8);
                yield return Frames(5);
                Shot("site_" + site.Depth, true);
                var cache = Field<LootField>(game, "loot").Items.Find(item => item.Special == site.CacheId);
                Assert.NotNull(cache.View, "The cache should be visible inside the room.");
                Assert.IsFalse(cache.Taken);
            }
            game.SaveNow();
            Assert.AreEqual(7, ProgressStore.Load(game.config).sites);
            game.DebugGoto("counter");
            yield return Frames(5);
            game.OpenHouse();
            var hud = game.GetComponent<MineHud>();
            typeof(MineHud).GetMethod("SelectTab", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, new object[] { MineHud.JournalPage });
            yield return Frames(5);
            Shot("ui_exploration_journal", true);
        }

        [UnityTest]
        public IEnumerator PetrolOnlyRefillsAtBaseAndFinalToolBecomesADrill()
        {
            var game = Object.FindAnyObjectByType<MineGame>(); PlayByTouch(game); game.Engage();
            Assert.IsTrue(game.HasJetpack, "A basic petrol jetpack is available from the start.");
            game.DebugPlace(MineSites.All[0].Origin + new Vector3(0, .2f, .4f), 0, 0);
            game.Progress.fuel = 7;
            yield return new WaitForSeconds(.7f);
            Assert.AreEqual(7, game.Fuel, .01f, "Standing underground must not refill petrol.");
            game.Progress.tool = game.config.tools.Length - 1;
            yield return Frames(4);
            Assert.IsTrue(game.UsingDrill); Assert.NotNull(GameObject.Find("Drill rotor")); Shot("campaign_drill", true);
            game.Progress.scanner = true; game.Scan(); yield return Frames(4);
            Assert.IsTrue(game.ScanHits.Exists(item => item.Kind == LootKind.Key), "The scanner must include nearby quest keys.");
            game.Progress.fuel = 0; yield return Frames(4);
            Assert.IsFalse(game.UsingDrill); Assert.AreEqual("Кристальная лопата", game.Tool.nameRu);
            game.DebugPlace(Yard.FuelPumpPoint + Vector3.back * 1.4f + Vector3.up * .1f, 0, 2);
            yield return new WaitForSeconds(1);
            Assert.Greater(game.Fuel, 5); Shot("campaign_refuel", true);
            game.SaveNow(); Assert.AreEqual(game.Fuel, ProgressStore.Load(game.config).fuel, .05f);
        }

        [UnityTest]
        public IEnumerator FiveKeysOpenTheDoorAndTheHarpoonDefeatsCthulhu()
        {
            var game = Object.FindAnyObjectByType<MineGame>(); PlayByTouch(game); game.Engage();
            var field = Field<LootField>(game, "loot");
            for (int i = 0; i < 5; i++) Invoke(game, "Collect", field.Items.Find(x => x.Key == i));
            game.Progress.healthLevel = 4; game.Progress.health = game.MaxHealth;
            game.DebugPlace(new Vector3(0, game.config.FloorY + .15f, -.8f), 0, 0);
            yield return Frames(6);
            Assert.IsFalse(game.Progress.finished); Assert.AreEqual("Открыть дверь", game.Interaction);
            Shot("campaign_five_seals", true);
            game.Interact(); yield return Frames(5); Assert.IsTrue(game.InBoss);
            Assert.IsFalse(game.Progress.hasWeapon);
            game.Interact(); yield return Frames(5); Assert.IsTrue(game.Progress.hasWeapon);
            game.Progress.Hurt(9999, game.config); Invoke(game, "LoseBattle"); yield return Frames(3);
            Assert.AreEqual(BattlePhase.Lost, game.Battle.Phase);
            game.RetryBoss(); yield return Frames(3);
            Assert.AreEqual(game.MaxHealth, game.Health); Assert.AreEqual(5, game.Progress.KeyCount);
            game.SetView(0, -13); yield return new WaitForSeconds(3.6f); Shot("campaign_cthulhu", true);
            for (int shot = 0; shot < 240 && !game.Progress.finished; shot++)
            {
                // Standing still takes every hit; keep the test about aiming, not dodging.
                game.Progress.health = game.MaxHealth;
                Invoke(game, "Swing"); yield return new WaitForSeconds(.46f);
            }
            Assert.IsTrue(game.Progress.finished, "Real aim rays and the equipped harpoon must damage the boss.");
            Assert.AreEqual(BattlePhase.Won, game.Battle.Phase); Shot("campaign_victory", true);
            game.ReturnFromBoss(); yield return Frames(5);
            Assert.IsFalse(game.InBoss); Assert.IsTrue(Yard.InsideHouse(Body(game).transform.position));
            Assert.IsTrue(ProgressStore.Load(game.config).finished);
        }

        [UnityTest]
        public IEnumerator SecretsOrdersAdsAndRestart()
        {
            var game = Object.FindAnyObjectByType<MineGame>(); PlayByTouch(game); game.Engage();
            var field = Field<LootField>(game, "loot");
            // The garden gnome: stand beside it and hit it with the real dig ray.
            var gnome = field.Items.Find(x => x.Secret == 0);
            var stand = gnome.Position + new Vector3(-1.5f, -.2f, 0);
            var look = gnome.Position - (stand + Vector3.up * 1.55f);
            game.DebugPlace(stand, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, -Mathf.Asin(look.y / look.magnitude) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(.6f);
            Assert.NotNull(gnome.View, "Surface secrets are visible from the lawn.");
            Shot("secret_gnome", true);
            int coins = game.Progress.coins;
            Invoke(game, "Swing");
            Assert.IsTrue(gnome.Taken);
            Assert.AreEqual(coins + Secrets.All[0].Reward, game.Progress.coins);
            Assert.IsTrue(ProgressStore.Load(game.config).HasSecret(0), "A found secret is saved at once.");

            // A deep secret sits in its own pocket and shows up once the player is near.
            var statue = field.Items.Find(x => x.Secret == Secrets.Statue);
            var inward = -new Vector3(Mathf.Sign(statue.Position.x), 0, Mathf.Sign(statue.Position.z)) * .55f;
            game.DebugPlace(statue.Position + inward + Vector3.down * .3f, Mathf.Atan2(-inward.x, -inward.z) * Mathf.Rad2Deg, 50);
            yield return new WaitForSeconds(.6f);
            Assert.NotNull(statue.View, "The statue waits in its pocket.");
            Shot("secret_statue");

            // The buyer's order: bring the ore and the house opens on the order page.
            Assert.IsTrue(game.Progress.HasQuest);
            game.Progress.ores = new int[game.config.ores.Length];
            game.Progress.ores[game.Progress.questOre] = game.Progress.questAmount;
            game.DebugGoto("counter");
            yield return Frames(5);
            Assert.AreEqual(Station.Counter, game.Station);
            game.OpenHouse();
            yield return Frames(3);
            Shot("ui_house_order_ready", true);
            coins = game.Progress.coins;
            int reward = game.Progress.questReward;
            game.DeliverOrder();
            Assert.AreEqual(coins + reward, game.Progress.coins);
            Assert.AreEqual(0, game.Progress.OrePieces);
            Assert.AreEqual(1, game.Progress.questNumber);

            // A rewarded ad pauses the game, pays once and starts the cooldown.
            game.GetComponent<MineHud>().ShowHouse(MineHud.SellPage);
            yield return Frames(3);
            Shot("ui_house_sell_ad", true);
            Assert.IsTrue(game.AdAvailable);
            coins = game.Progress.coins;
            int adCoins = game.AdCoins;
            game.WatchAd();
            Assert.IsTrue(YandexBridge.Paused, "The game waits while the ad is on screen.");
            game.WatchAd();
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.IsFalse(YandexBridge.Paused);
            Assert.AreEqual(coins + adCoins, game.Progress.coins, "One ad, one reward.");
            Assert.IsFalse(game.AdAvailable, "The next ad waits for the cooldown.");
            Assert.AreEqual(1, ProgressStore.Load(game.config).adRewards);

            // Starting over asks first, then wipes everything but the sound setting.
            game.Progress.expeditions = 1;
            game.CloseHouse();
            game.OpenMenu();
            yield return Frames(2);
            var hud = game.GetComponent<MineHud>();
            typeof(MineHud).GetMethod("ConfirmRestart", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, new object[] { true });
            yield return Frames(3);
            Shot("ui_restart_confirm", true);
            game.Progress.muted = true;
            game.RestartGame();
            yield return Frames(10);
            var fresh = Object.FindAnyObjectByType<MineGame>();
            Assert.AreNotEqual(game, fresh);
            Assert.AreEqual(0, fresh.Progress.coins);
            Assert.AreEqual(0, fresh.Progress.secrets);
            Assert.AreEqual(0, fresh.Progress.questNumber);
            Assert.IsTrue(fresh.Progress.muted);
            Assert.IsFalse(PlayerPrefs.HasKey(Key + ".backup"), "The old game cannot come back from the backup.");
            Assert.AreEqual(0, ProgressStore.Load(fresh.config).coins);
            GameAudio.SetMuted(false);
        }

        [UnityTest]
        public IEnumerator CthulhuSleepsWindsUpSlamsWatchesAndSinks()
        {
            var game = Object.FindAnyObjectByType<MineGame>(); PlayByTouch(game); game.Engage();
            var field = Field<LootField>(game, "loot");
            for (int i = 0; i < 5; i++) Invoke(game, "Collect", field.Items.Find(x => x.Key == i));
            game.Progress.healthLevel = 4; game.Progress.health = game.MaxHealth;
            game.DebugPlace(new Vector3(0, game.config.FloorY + .15f, -.8f), 0, 0);
            yield return Frames(6);
            game.Interact(); yield return Frames(5);
            Assert.IsTrue(game.InBoss);
            var neck = GameObject.Find("Cthulhu neck").transform;
            var chest = GameObject.Find("Cthulhu chest").transform;
            var arms = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in chest) if (child.name == "Cthulhu shoulder") arms.Add(child);
            Assert.AreEqual(2, arms.Count);
            game.SetView(0, -13);
            yield return new WaitForSeconds(1);
            Assert.Less(Mathf.DeltaAngle(0, neck.localEulerAngles.x), -15, "Asleep, the head hangs.");
            Shot("cthulhu_asleep", true);

            // Taking the harpoon wakes it; then every attack is wound up and lands with a slam.
            game.DebugPlace(BossEncounter.WeaponPoint + new Vector3(0, -.7f, -.9f), 0, -13);
            yield return Frames(3);
            game.Interact(); yield return Frames(3);
            Assert.IsTrue(game.Progress.hasWeapon);
            game.DebugPlace(BossEncounter.Spawn, 0, -13);
            for (int attack = 0; attack < 5; attack++)
            {
                game.Progress.health = game.MaxHealth;
                float until = Time.time + 6;
                while (!(game.Battle.Phase == BattlePhase.Warning && game.Battle.Remaining < .4f) && Time.time < until) yield return null;
                Assert.AreEqual(BattlePhase.Warning, game.Battle.Phase);
                int pattern = game.Battle.Pattern;
                if (pattern == BossBattle.WaveLeft || pattern == BossBattle.WaveRight)
                    Assert.Greater(Quaternion.Angle(Quaternion.identity, arms[pattern - 1].localRotation), 60, "The arm on the wave's side is raised.");
                else if (pattern == BossBattle.Quake)
                    foreach (var arm in arms) Assert.Greater(Quaternion.Angle(Quaternion.identity, arm.localRotation), 60, "Both arms go up before the quake.");
                else Assert.Greater(Mathf.DeltaAngle(0, chest.localEulerAngles.x), 4, "It rears back before the tentacles fall.");
                Shot("cthulhu_windup_" + pattern, true);
                int landed = game.Battle.Landed;
                until = Time.time + 2;
                while (game.Battle.Landed == landed && Time.time < until) yield return null;
                yield return new WaitForSeconds(.1f);
                Shot("cthulhu_slam_" + pattern, true);
            }

            // The head follows the player around the arena.
            game.DebugPlace(BossEncounter.Origin + new Vector3(6, .1f, -4), -60, -10);
            yield return new WaitForSeconds(1.2f);
            Assert.Less(Mathf.DeltaAngle(0, neck.localEulerAngles.y), -12, "The head turns towards the player.");

            // Defeat: the victory is saved at once, the monster sinks, and only then the result card opens.
            game.DebugPlace(BossEncounter.Spawn, 0, -13);
            for (int shot = 0; shot < 200 && game.Battle.Phase != BattlePhase.Won; shot++)
            {
                game.Battle.Shoot(true);
                if (game.Battle.Phase != BattlePhase.Warning && game.Battle.Phase != BattlePhase.Recovery && game.Battle.Phase != BattlePhase.Won) break;
            }
            Assert.AreEqual(BattlePhase.Won, game.Battle.Phase);
            yield return Frames(3);
            Assert.IsTrue(ProgressStore.Load(game.config).finished);
            var hud = game.GetComponent<MineHud>();
            Assert.IsFalse(hud.PanelOpen, "The result waits for the death animation.");
            yield return new WaitForSeconds(1.4f);
            Shot("cthulhu_dying", true);
            Assert.IsTrue(GameObject.Find("Cthulhu") != null, "Still sinking.");
            yield return new WaitForSeconds(2.4f);
            Assert.IsNull(GameObject.Find("Cthulhu"), "Gone under the floor.");
            Assert.IsTrue(hud.PanelOpen);
            Shot("cthulhu_gone", true);
        }

        [UnityTest]
        public IEnumerator DynamiteBlowsAHoleAndHurtsWhoeverStaysClose()
        {
            var game = Object.FindAnyObjectByType<MineGame>(); PlayByTouch(game); game.Engage();
            game.DebugDig("45");
            yield return Frames(5);
            game.Progress.coins = 1000;
            game.BuyDynamite(); game.BuyDynamite();
            Assert.AreEqual(2, game.Progress.dynamite);
            var terrain = Field<VoxelTerrain>(game, "terrain");
            var floor = new Vector3(0.8f, -45 + .2f, -1.2f);

            // Thrown at the shaft wall from the bottom; the player is far away when it goes off.
            game.DebugPlace(floor, 90, 12);
            yield return Frames(3);
            game.ThrowDynamite();
            Assert.AreEqual(1, game.Progress.dynamite);
            Assert.AreEqual(1, game.ChargesInFlight);
            game.DebugPlace(Yard.SurfaceSpawn, Yard.SurfaceYaw, 5);
            float health = game.Health;
            yield return new WaitForSeconds(game.config.dynamiteFuse + .15f);
            Assert.AreEqual(0, game.ChargesInFlight);
            Assert.IsTrue(game.LastBlast.HasValue);
            var blast = game.LastBlast.Value;
            Assert.IsTrue(terrain.IsAir(blast + Vector3.right * 1.4f) || terrain.IsAir(blast + Vector3.forward * 1.4f) || terrain.IsAir(blast + Vector3.back * 1.4f),
                "The blast opens the rock around it.");
            Assert.AreEqual(health, game.Health, .01f, "Far away is safe.");

            // Standing right next to it hurts.
            yield return new WaitForSeconds(1.8f);
            game.DebugPlace(floor, 90, 12);
            yield return new WaitForSeconds(.5f);
            health = game.Health;
            game.ThrowDynamite();
            yield return new WaitForSeconds(game.config.dynamiteFuse + .06f);
            Shot("dynamite_blast", true);
            yield return new WaitForSeconds(.1f);
            Assert.Less(game.Health, health, "A blast a metre away hurts.");
            Assert.AreEqual(0, game.Progress.dynamite);
            game.ThrowDynamite();
            Assert.AreEqual(0, game.ChargesInFlight, "Nothing to throw.");
        }

        [UnityTest]
        public IEnumerator TheYardHasNoWayOutButTheHouseDoorIsOpen()
        {
            var game = Object.FindAnyObjectByType<MineGame>();
            yield return Frames(3);
            // Rays from the middle of the yard in every direction and at every height hit a wall or a fence.
            foreach (float height in new[] { .8f, 3f, 12f, 40f, 75f })
                for (int i = 0; i < 24; i++)
                {
                    float angle = i * 15 * Mathf.Deg2Rad;
                    var origin = new Vector3(0, height, 0);
                    var direction = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                    Assert.IsTrue(Physics.Raycast(origin, direction, out var hit, 40, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore),
                        "Open at " + height + " m towards " + i * 15 + " degrees");
                    Assert.Less(hit.distance, 26);
                }
            Assert.IsTrue(Physics.Raycast(new Vector3(0, 2, 0), Vector3.up, 90, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), "No way out upwards");
            // The doorway stays open: straight through it the first thing hit is the back wall of the house.
            Assert.IsTrue(Physics.Raycast(new Vector3(-3, 1, 14), Vector3.forward, out var door, 20, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore));
            Assert.Greater(door.point.z, 24);
            game.DebugPlace(new Vector3(-15.5f, .05f, 14), -30, 0);
            yield return Frames(4);
            Shot("yard_north_west_fence");
            game.DebugPlace(new Vector3(15.5f, .05f, 12), 20, 0);
            yield return Frames(4);
            Shot("yard_north_east_fence");
        }

        private static void PlayByTouch(MineGame game)
        {
            // The batch editor cannot lock the mouse; touch mode lets the player move.
            typeof(MineGame).GetField("<TouchMode>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, true);
        }

        private static CharacterController Body(MineGame game) => Field<CharacterController>(game, "body");

        private static T Field<T>(MineGame game, string name) =>
            (T)typeof(MineGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);

        private static void Invoke(MineGame game, string name, params object[] args) =>
            typeof(MineGame).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, args);

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private static void Shot(string name, bool includeUi = false)
        {
            var camera = Camera.main;
            if (camera == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var target = new RenderTexture(960, 540, 24);
            var canvas = GameObject.Find("Game UI").GetComponent<Canvas>();
            if (includeUi)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = .1f;
                Canvas.ForceUpdateCanvases();
            }
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;
            if (includeUi) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RenderTexture.active = target;
            var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory("TestResults/Snapshots");
            File.WriteAllBytes("TestResults/Snapshots/" + name + ".png", image.EncodeToPNG());
            Object.Destroy(image);
            target.Release();
        }
    }
}
