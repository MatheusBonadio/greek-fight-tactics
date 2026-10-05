using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Grid;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class PressureAndDamarisPlayTests
    {
        private const float TimeoutSeconds = 10f;
        private const float BattleTimeoutSeconds = 90f;
        private const float CutsceneTimeoutSeconds = 60f;
        private const int MaxConfirms = 128;
        private const float CameraSettleSeconds = 1.5f;
        private const string WheelTile = "prop_potters_wheel";
        private const string AmphoraTile = "prop_amphora_stack";
        private const string DamarisCharacter = "damaris";
        private const string DamarisCutscene = "damaris_seeks_kleia";
        private const string MenexenoDialogue = "menexeno_agora";
        private const string MenexenoGoneCue = "agora_menexeno_away";
        private const string DuelOpponent = "simo";
        private const string CalmWorkshopCapturePath = "artifacts/reference/kerameikos_calm.png";
        private const string PressedWorkshopCapturePath = "artifacts/reference/kerameikos_pressure.png";

        private static readonly string[] SettledStory =
        {
            "left_workshop", "tutorial_first_steps", "tutorial_fog", "tutorial_talk", "aristeu_errand", "talked_to_neighbor",
            "saw_thrasybios_demo", "thrasybios_tutorial_done", "headed_home", "met_menexeno", "menexeno_condition", "menexeno_avoids_past",
            "menexeno_door_scene", ActionUnlocks.MaieuticsFlag, "heard_asebeia", "saw_rumor_spread", "worried_for_home", "saw_empty_workshop", "saw_wheel_sold"
        };

        private static readonly string[] ActOneOpponents = { "thrasybios", "oron", "phaidra", "kritolaos", "simo", "batilo" };

        private static readonly Coord HomeArrival = new(16, 2);
        private static readonly Coord CaptureCenter = new(8, 4);

        private IsolatedDataDirectory data;

        [UnitySetUp]
        public IEnumerator IsolateData()
        {
            data = IsolatedDataDirectory.Enter();
            SceneArrival.Clear();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestoreData()
        {
            SceneArrival.Clear();
            data?.Dispose();
            data = null;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Workshop_LosesItsCustomersThenThePottersWheelAsDuelsConclude()
        {
            var map = default(MapScene);
            yield return Open(LocationScenes.KerameikosScene, WithDuels(1), loaded => map = loaded, false);
            var wheel = BlockCell(map, WheelTile);
            var unsold = BlockCell(map, AmphoraTile);
            Assert.IsTrue(Customers(map).All(customer => customer.Walker != null), "the customers still come after the first duel");
            Assert.IsTrue(map.Sandbox.IsBlockShown(wheel), "the wheel stays while Aristeu can still sell amphorae");
            Assert.IsFalse(map.Sandbox.Grid[wheel].IsPassable);
            Assert.IsFalse(map.Sandbox.IsBlockShown(unsold), "no amphorae pile up yet");
            Assert.IsTrue(map.Sandbox.Grid[unsold].IsPassable, "the empty cell of the future pile can be walked on");

            yield return Open(LocationScenes.KerameikosScene, WithDuels(StoryPressure.DesertedDuels), loaded => map = loaded, false);
            Assert.IsNotEmpty(Customers(map));
            Assert.IsTrue(Customers(map).All(customer => customer.Walker == null && !customer.gameObject.activeSelf), "the customers vanish once the rumor has spread");
            Assert.IsTrue(map.Sandbox.IsBlockShown(unsold), "unsold amphorae pile up at the door");
            Assert.IsFalse(map.Sandbox.Grid[unsold].IsPassable);
            Assert.IsTrue(map.Sandbox.IsBlockShown(wheel), "the wheel is the last thing to go");

            yield return Open(LocationScenes.KerameikosScene, WithDuels(StoryPressure.ToolsDuels), loaded => map = loaded, false);
            Assert.IsFalse(map.Sandbox.IsBlockShown(wheel), "Aristeu sold the wheel");
            Assert.IsTrue(map.Sandbox.Grid[wheel].IsPassable, "the grid, not the tilemap, lets Kleia stand where the wheel was");
            Assert.IsTrue(map.Sandbox.TryMovePlayerTo(wheel), "Kleia walks into the empty corner");
            yield return DuelChallenge.WaitFor(() => !map.Sandbox.Player.Walker.IsMoving, TimeoutSeconds);
            Assert.AreEqual(wheel, map.Sandbox.Player.Walker.Cell);
        }

        [UnityTest]
        public IEnumerator Damaris_WaitsForKleiaAtHomeAndHerAnswerIsSaved([Values(0, 1)] int option)
        {
            var expected = option == 0 ? DamarisChoice.Accepted : DamarisChoice.Refused;
            var map = default(MapScene);
            yield return Open(LocationScenes.KerameikosScene, WithDuels(StoryPressure.ToolsDuels), loaded => map = loaded, true, true);
            var damaris = map.Cutscenes.Cast[DamarisCharacter];
            Assert.IsNotNull(damaris.Walker, "Damaris waits in the street once Aristeu has sold the wheel");
            Assert.AreEqual(HomeArrival, map.Sandbox.Player.Walker.Cell);
            Assert.IsTrue(map.Services.TryLoadActiveSave(out var arrival));
            Assert.AreEqual(StoryDays.FirstDay + 1, StoryDays.DayOf(arrival), "coming home ends the day");

            var trigger = map.Dialogues.Triggers.First(link => link.Cutscene == DamarisCutscene && link.IsDue(map.Dialogues.Story));
            Assert.IsTrue(map.Sandbox.TryMovePlayerTo(trigger.Cell));
            yield return DuelChallenge.WaitFor(() => map.Cutscenes.IsPlaying, TimeoutSeconds);
            Assert.AreEqual(DamarisCutscene, map.Cutscenes.ActiveCutscene, "walking up to Damaris plays her offer");
            Assert.AreEqual(1, PositionalModifiers.Distance(map.Sandbox.Player.Walker.Cell, damaris.Walker.Cell));

            var lines = new List<DialogueLine>();
            var dialogues = new List<string>();
            yield return PlayCutscene(map, option, lines, dialogues);
            CollectionAssert.AreEqual(new[] { "damaris_offer", "damaris_technique" }, dialogues);
            Assert.IsTrue(lines.Any(line => line.SpeakerName == "Damaris" && line.Portrait), "Damaris speaks with her portrait");
            Assert.IsNull(damaris.Walker, "Damaris leaves after the answer");
            Assert.AreEqual(GameMode.Exploration, map.Sandbox.StateMachine.Mode);

            Assert.IsTrue(map.Services.TryLoadActiveSave(out var saved), "the end of the scene autosaves");
            Assert.AreEqual(expected, saved.DamarisBranch, "the branch the tribunal reads is in the save");
            CollectionAssert.AreEqual(new[] { DamarisOffer.GrantedAbility(expected).Value }, AbilityUnlocks.OwnedIn(saved));
            if (expected == DamarisChoice.Accepted)
            {
                Assert.AreEqual(DamarisOffer.AcceptedDrachmas, saved.Inventory[DamarisOffer.DrachmasItem]);
                Assert.AreEqual(DamarisOffer.MenexenoAbsentDays, StoryDays.AbsenceLeft(saved, DamarisOffer.MenexenoAbsentDaysFlag));
                Assert.IsTrue(lines.Any(line => line.Text.Contains("INVERSÃO")));
            }
            else
            {
                Assert.IsTrue(StoryCondition.Has(DamarisOffer.MenexenoRespectFlag).IsMet(saved));
                Assert.IsFalse(saved.Inventory.ContainsKey(DamarisOffer.DrachmasItem));
                Assert.IsTrue(lines.Any(line => line.Text.Contains("ESCUTA")));
            }

            yield return Open(LocationScenes.KerameikosScene, null, loaded => map = loaded, false);
            Assert.IsNull(map.Cutscenes.Cast[DamarisCharacter].Walker, "Damaris does not come back once Kleia has answered");
            Assert.IsTrue(map.Services.TryLoadActiveSave(out var reloaded));
            Assert.AreEqual(expected, reloaded.DamarisBranch, "the answer survives the save");
        }

        [UnityTest]
        public IEnumerator Abilities_AnswerTheFirstArgumentOfADuel([Values(AbilityId.Inversion, AbilityId.Listening)] AbilityId ability)
        {
            var story = WithDuels(1);
            story.Flags[DamarisOffer.AbilityFlag(ability)] = DamarisOffer.EnabledValue;
            var map = default(MapScene);
            yield return Open(LocationScenes.AgoraScene, story, loaded => map = loaded, false);
            var battle = map.Battle;
            battle.ThinkSeconds = 0f;
            battle.AutosaveEnabled = false;
            var opponent = battle.Opponents.Single(entry => entry.Opponent.Id == DuelOpponent);
            yield return DuelChallenge.Engage(map.Sandbox, battle, opponent, TimeoutSeconds);
            Assert.IsTrue(battle.Session.State.HasAbility(ability), "the ability comes from the save");
            KeepMaieuticsInReachWhereverTheOpponentWanders(battle, map.Sandbox);

            var shown = new List<string>();
            var firstArgument = default(BattleLogEntry);
            var goldPremise = false;
            var deadline = Time.realtimeSinceStartup + BattleTimeoutSeconds;
            while (battle.Phase != BattleFlowPhase.Exploring && Time.realtimeSinceStartup < deadline)
            {
                switch (battle.Phase)
                {
                    case BattleFlowPhase.Dialogue:
                        shown.Add(battle.Hud.Dialogue.Line.Text);
                        battle.AdvanceDialogue();
                        break;
                    case BattleFlowPhase.PlayerChoosing:
                        firstArgument ??= battle.Session.State.Log.FirstOrDefault(entry => entry.Action == ActionId.Argue && entry.Rejection == RejectionReason.None);
                        if (firstArgument != null && ability == AbilityId.Listening)
                        {
                            goldPremise = WeakPremiseGlows(battle);
                        }

                        battle.SubmitChoice(new BattleMenuChoice(firstArgument != null ? ActionId.Withdraw : ActionId.Concede));
                        break;
                }

                yield return null;
            }

            Assert.AreEqual(BattleFlowPhase.Exploring, battle.Phase, "the duel did not finish in time");
            Assert.AreEqual(BattleStatus.PlayerWithdrew, battle.LastResult);
            Assert.IsNotNull(firstArgument, "the opponent never argued");
            Assert.AreEqual(ability, firstArgument.TriggeredAbility, "the ability answers the first argument");
            var announced = ability == AbilityId.Inversion ? "INVERSÃO" : "ESCUTA";
            Assert.IsTrue(shown.Any(text => text.StartsWith(announced, StringComparison.Ordinal)), $"the duel never announced {announced}: {string.Join(" | ", shown)}");
            if (ability == AbilityId.Inversion)
            {
                Assert.Greater(firstArgument.PlayerAudienceDelta, 0, "the attack became applause for Kleia");
            }
            else
            {
                Assert.IsTrue(goldPremise, "the weak premise glows gold without the assisted mode");
            }
        }

        [UnityTest]
        public IEnumerator Menexeno_StaysAwayFromTheAgoraForThreeHomecomingsAfterKleiaAcceptsDamaris()
        {
            var story = WithDuels(StoryPressure.ToolsDuels);
            DamarisOffer.Apply(story, DamarisChoice.Accepted);
            var map = default(MapScene);
            yield return Open(LocationScenes.AgoraScene, story, loaded => map = loaded, true);
            yield return DuelChallenge.WaitFor(() => map.Dialogues.IsTalking, TimeoutSeconds);
            Assert.AreEqual(MenexenoGoneCue, map.Dialogues.ActiveDialogueId, "Kleia notices the empty place by the stoa");
            yield return PlayToTheEnd(map.Dialogues, new List<DialogueLine>());

            for (var day = 1; day <= DamarisOffer.MenexenoAbsentDays; day++)
            {
                Assert.IsNull(Menexeno(map).Walker, $"Menexeno is away before homecoming {day}");
                Assert.IsFalse(Menexeno(map).gameObject.activeSelf);

                SceneArrival.Set(new PendingArrival(LocationScenes.Kerameikos, HomeArrival, Facing.SouthWest, LocationScenes.Agora));
                yield return Open(LocationScenes.KerameikosScene, null, loaded => map = loaded, false);
                Assert.IsTrue(map.Services.TryLoadActiveSave(out var home));
                Assert.AreEqual(DamarisOffer.MenexenoAbsentDays - day, StoryDays.AbsenceLeft(home, DamarisOffer.MenexenoAbsentDaysFlag), $"homecoming {day} passes one day");

                SceneArrival.Set(new PendingArrival(LocationScenes.Agora, new Coord(0, 1), Facing.NorthEast, LocationScenes.Kerameikos));
                yield return Open(LocationScenes.AgoraScene, null, loaded => map = loaded, false);
            }

            Assert.IsNotNull(Menexeno(map).Walker, "Menexeno is back in the agora after three days");
            var talker = map.Dialogues.Talkers.Single(link => link.Dialogue == MenexenoDialogue);
            map.Sandbox.Player.Walker.Teleport(FreeCellBeside(map.Sandbox, talker.Actor.Walker.Cell), Facing.NorthEast);
            yield return null;
            map.Sandbox.Cursor.TryPoint(talker.Actor.Walker.Cell);
            map.Sandbox.ConfirmCursor();
            yield return DuelChallenge.WaitFor(() => map.Dialogues.IsTalking, TimeoutSeconds);
            var said = new List<DialogueLine>();
            yield return PlayToTheEnd(map.Dialogues, said);
            Assert.IsTrue(said.Any(line => line.Text.Contains("Três dias")), "he says where he has been, without saying why");
        }

        [UnityTest]
        [Explicit("renders the workshop before and after the pressure under artifacts/reference for a visual review")]
        public IEnumerator Capture_WorkshopBeforeAndAfterThePressure()
        {
            var map = default(MapScene);
            foreach (var (concluded, capturePath) in new[] { (1, CalmWorkshopCapturePath), (StoryPressure.ToolsDuels, PressedWorkshopCapturePath) })
            {
                yield return Open(LocationScenes.KerameikosScene, WithDuels(concluded), loaded => map = loaded, false);
                map.Sandbox.Player.Walker.Teleport(CaptureCenter, Facing.SouthWest);
                new FogOfWar(map.Sandbox.Grid).Reveal(CaptureCenter, map.Sandbox.Width + map.Sandbox.Height);
                yield return new WaitForSeconds(CameraSettleSeconds);
                yield return UiCapture.Screen(capturePath);
            }
        }

        private static SaveData WithDuels(int concluded)
        {
            var story = new SaveData { Location = GridSandboxController.DefaultLocationId };
            foreach (var flag in SettledStory)
            {
                story.Flags[flag] = StoryEffect.DefaultValue;
            }

            foreach (var opponent in ActOneOpponents.Take(concluded))
            {
                story.Flags[DuelStoryFlags.FlagFor(opponent)] = DuelStoryFlags.Won;
                story.Duels[opponent] = new DuelRecord(true, DuelVictory.Refute);
            }

            return story;
        }

        private static IEnumerator Open(string scene, SaveData story, Action<MapScene> loaded, bool withCues, bool arriveHome = false)
        {
            if (story != null)
            {
                yield return SceneManager.LoadSceneAsync(LocationScenes.KerameikosScene, LoadSceneMode.Single);
                var services = Object.FindAnyObjectByType<GameServices>();
                Object.FindAnyObjectByType<StoryCueDirector>().enabled = false;
                yield return DuelChallenge.WaitFor(() => services.IsBooted, TimeoutSeconds);
                story.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                services.Autosave(story);
            }

            if (arriveHome)
            {
                SceneArrival.Set(new PendingArrival(LocationScenes.Kerameikos, HomeArrival, Facing.SouthWest, LocationScenes.Agora));
            }

            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            var map = Find();
            if (map.Cues)
            {
                map.Cues.enabled = withCues;
            }

            yield return DuelChallenge.WaitFor(() => map.Services.IsBooted && map.Sandbox.ProgressLoaded && map.Dialogues.IsStoryLoaded, TimeoutSeconds);
            Assert.IsTrue(map.Dialogues.IsStoryLoaded, $"{scene} never loaded the story");
            loaded(map);
        }

        private static MapScene Find() => new(
            Object.FindAnyObjectByType<GridSandboxController>(),
            Object.FindAnyObjectByType<ExplorationDialogueDirector>(),
            Object.FindAnyObjectByType<StoryCueDirector>(),
            Object.FindAnyObjectByType<GameServices>(),
            Object.FindAnyObjectByType<CutsceneDirector>(),
            Object.FindAnyObjectByType<SandboxBattleDirector>());

        private static IEnumerable<ActorView> Customers(MapScene map) =>
            map.Dialogues.Talkers.Where(link => link.Dialogue.StartsWith("customer_", StringComparison.Ordinal)).Select(link => link.Actor);

        private static ActorView Menexeno(MapScene map) => map.Dialogues.Talkers.Single(link => link.Dialogue == MenexenoDialogue).Actor;

        private static Coord BlockCell(MapScene map, string tile)
        {
            var link = map.Sandbox.BlockPresences.Single(presence => map.Sandbox.BlockTiles[presence.Cell.Y * map.Sandbox.Width + presence.Cell.X].name == tile);
            return link.Cell;
        }

        private static void KeepMaieuticsInReachWhereverTheOpponentWanders(SandboxBattleDirector battle, GridSandboxController sandbox)
        {
            battle.Session.State.Rules.MaieuticsRange = sandbox.Width + sandbox.Height;
            battle.Session.Menu.Refresh();
        }

        private static bool WeakPremiseGlows(SandboxBattleDirector battle)
        {
            var menu = battle.Hud.Menu;
            Assert.IsFalse(menu.AssistedMode, "the gold highlight must come from the ability");
            Assert.IsTrue(battle.Session.State.IsWeakPremiseRevealed, "listening revealed the first argument");
            var entries = battle.Session.Menu.Entries.Select((entry, index) => (entry, index)).ToList();
            var maieutics = entries.Single(pair => pair.entry.Action == ActionId.Maieutics);
            if (battle.Session.State.CurrentArgument.Value.IsOnlyRefutable)
            {
                Assert.AreEqual(RejectionReason.PremisesHold, maieutics.entry.Reason, "listening heard that the premises hold");
                var refute = entries.Single(pair => pair.entry.Action == ActionId.Refute);
                menu.HandlePoint(BattleMenuLayer.Actions, refute.index);
                return menu.ActionRows[refute.index].color == menu.HintColor;
            }

            Assert.IsTrue(maieutics.entry.IsAvailable, "maieutics is on the menu");
            menu.HandlePoint(BattleMenuLayer.Actions, maieutics.index);
            menu.HandleConfirm();
            Assert.AreEqual(BattleMenuLayer.Premises, battle.Controller.Layer);
            var weak = battle.Session.State.CurrentArgument.Value.WeakPremise;
            var glows = menu.SubmenuRows[weak - menu.SubmenuWindowStart].color == menu.HintColor;
            menu.HandleCancel();
            Assert.AreEqual(BattleMenuLayer.Actions, battle.Controller.Layer);
            return glows;
        }

        private static IEnumerator PlayCutscene(MapScene map, int option, List<DialogueLine> lines, List<string> dialogues)
        {
            var deadline = Time.realtimeSinceStartup + CutsceneTimeoutSeconds;
            while (map.Cutscenes.IsPlaying && Time.realtimeSinceStartup < deadline)
            {
                if (map.Dialogues.IsTalking)
                {
                    dialogues.Add(map.Dialogues.ActiveDialogueId);
                    yield return PlayToTheEnd(map.Dialogues, lines, option);
                    continue;
                }

                yield return null;
            }

            Assert.IsFalse(map.Cutscenes.IsPlaying, "the cutscene never finished");
        }

        private static IEnumerator PlayToTheEnd(ExplorationDialogueDirector director, List<DialogueLine> shown, int option = 0)
        {
            ScriptLine last = null;
            for (var confirms = 0; director.IsTalking && confirms < MaxConfirms; confirms++)
            {
                if (!ReferenceEquals(director.CurrentLine, last))
                {
                    last = director.CurrentLine;
                    shown.Add(director.DialogueBox.Line);
                }

                if (director.IsAwaitingChoice)
                {
                    for (var step = 0; step < option; step++)
                    {
                        director.DialogueBox.HandleNavigate(Vector2.down);
                    }
                }

                director.Advance();
                yield return null;
            }

            Assert.IsFalse(director.IsTalking, "the dialogue never finished");
        }

        private static Coord FreeCellBeside(GridSandboxController map, Coord cell)
        {
            var player = map.Player.Walker;
            return new[] { new Coord(cell.X + 1, cell.Y), new Coord(cell.X, cell.Y + 1), new Coord(cell.X - 1, cell.Y), new Coord(cell.X, cell.Y - 1) }
                .First(candidate => map.Grid.TryGet(candidate, out var tile) && tile.IsPassable && (!tile.IsOccupied || tile.OccupantId == player.ActorId)
                    && PathFinder.AreBesideOnFoot(map.Grid, candidate, cell) && map.Exits.All(link => link.Cell != candidate));
        }

        private readonly struct MapScene
        {
            public readonly GridSandboxController Sandbox;
            public readonly ExplorationDialogueDirector Dialogues;
            public readonly StoryCueDirector Cues;
            public readonly GameServices Services;
            public readonly CutsceneDirector Cutscenes;
            public readonly SandboxBattleDirector Battle;

            public MapScene(GridSandboxController sandbox, ExplorationDialogueDirector dialogues, StoryCueDirector cues, GameServices services, CutsceneDirector cutscenes, SandboxBattleDirector battle)
            {
                Sandbox = sandbox;
                Dialogues = dialogues;
                Cues = cues;
                Services = services;
                Cutscenes = cutscenes;
                Battle = battle;
            }
        }
    }
}
