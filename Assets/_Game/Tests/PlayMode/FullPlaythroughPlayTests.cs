using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Content;
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
    public sealed class FullPlaythroughPlayTests
    {
        private const float BootSlackSeconds = 1f;
        private const float SceneTimeoutSeconds = 10f;
        private const float WalkTimeoutSeconds = 10f;
        private const float BattleTimeoutSeconds = 120f;
        private const float CutsceneTimeoutSeconds = 60f;
        private const float MapReadySeconds = 3f;
        private const float OfflineThinkSeconds = 0.1f;
        private const float ThinkSlackSeconds = 1f;
        private const int MaxDialogueConfirms = 128;
        private const int MaxApproaches = 4;
        private const int MaxRestoreShift = 2;
        private const int QuietFrames = 30;
        private const int IdleFramesBeforeWalkingOn = 2;
        private const string DamarisOfferDialogue = "damaris_offer";
        private const string DamarisCharacter = "damaris";
        private const string MenexenoAgoraDialogue = "menexeno_agora";
        private const string CustomerDialoguePrefix = "customer_";
        private const int AcceptOption = 0;
        private const int RefuseOption = 1;
        private const string TrialCalledFlag = "street_trial_called";
        private const string SearchingMenexenoFlag = "searching_menexeno";
        private const string TutorOpponent = "thrasybios";

        private static readonly DamarisChoice[] Branches = { DamarisChoice.Accepted, DamarisChoice.Refused };

        private static readonly string[] BattleLinesToShow =
        {
            BattleText.QuoteKey,
            BattleText.RefuteHitKey,
            BattleText.RefuteMissKey,
            BattleText.MaieuticsHitKey,
            BattleText.MaieuticsMissKey,
            BattleText.ConcedeKey,
            BattleText.ConcedeCounterKey,
            BattleText.DeflectedKey,
            BattleText.VictoryKey,
            BattleText.WithdrewKey
        };

        private readonly HashSet<string> battleLinesShown = new();
        private readonly HashSet<string> dialoguesPlayed = new();
        private readonly HashSet<string> cutscenesPlayed = new();
        private readonly Dictionary<string, BattleStatus> duelResults = new();
        private readonly Dictionary<string, int> dialogueAnswers = new();
        private readonly HashSet<AbilityId> abilitiesUsed = new();
        private int abilityLines;
        private readonly List<DuelPhaseCue> phaseCues = new();
        private IsolatedDataDirectory data;
        private IReadOnlyList<string> refutableIds = Array.Empty<string>();
        private float longestOpponentTurnSeconds;
        private int opponentLines;

        private enum DuelPlan
        {
            Win,
            Explore,
            Withdraw,
            Lose
        }

        [UnitySetUp]
        public IEnumerator EnterAnIsolatedDataDirectory()
        {
            data = IsolatedDataDirectory.Enter();
            SceneArrival.Clear();
            battleLinesShown.Clear();
            dialoguesPlayed.Clear();
            cutscenesPlayed.Clear();
            duelResults.Clear();
            dialogueAnswers.Clear();
            abilitiesUsed.Clear();
            abilityLines = 0;
            phaseCues.Clear();
            longestOpponentTurnSeconds = 0f;
            opponentLines = 0;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator LeaveTheIsolatedDataDirectory()
        {
            SceneArrival.Clear();
            data?.Dispose();
            data = null;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EverythingThatExists_IsPlayableFromTheMenuToTheAcademy([ValueSource(nameof(Branches))] DamarisChoice branch)
        {
            yield return SceneManager.LoadSceneAsync(PauseMenuController.DefaultMainMenuScene, LoadSceneMode.Single);

            var menu = default(MainMenuController);
            yield return OpenMainMenu(loaded => menu = loaded);
            Assert.IsFalse(menu.Services.HasContinue, "a fresh install has nothing to continue");
            Choose(menu.MainMenu, SystemMenus.NewGame);
            PickSlot(menu.Slots, SaveSlots.DefaultIndex);
            yield return WaitForScene(LocationScenes.StartScene);

            var workshop = default(MapScene);
            yield return EnterMap(loaded => workshop = loaded);
            Assert.AreEqual(LocationScenes.Kerameikos, workshop.Sandbox.LocationId);
            yield return SettleTheStory(workshop);
            yield return WalkIntoEveryTrigger(workshop);
            yield return TalkToEveryone(workshop);
            var workshopFlags = workshop.Dialogue.Story.Flags.Keys.ToList();
            Assert.IsNotEmpty(workshopFlags, "the workshop's story recorded nothing");
            var road = workshop.Sandbox.Exits.First(link => link.Location == LocationScenes.Agora);
            yield return WalkThroughExit(workshop, road);

            var agora = default(MapScene);
            yield return EnterMap(loaded => agora = loaded);
            Assert.AreEqual(road.Arrival, agora.Player.Cell, "the agora puts Kleia at the road's arrival");
            Assert.IsTrue(agora.Services.TryLoadActiveSave(out var arrival), "arriving autosaves while offline");
            Assert.AreEqual(LocationScenes.Agora, arrival.Location);
            CollectionAssert.IsSubsetOf(workshopFlags, arrival.Flags.Keys, "the workshop's story survives the trip");
            refutableIds = agora.Services.Catalog.RefutableIds.ToList();
            yield return SettleTheStory(agora);
            yield return TalkToEveryone(agora);
            yield return TakeEveryLesson(agora);
            Assert.IsFalse(ActionUnlocks.IsUnlocked(ActionId.Maieutics, agora.Dialogue.Story), "only Menexeno teaches maieutics");

            var home = agora.Sandbox.Exits.First(link => link.Location == LocationScenes.Kerameikos);
            yield return WalkThroughExit(agora, home);
            yield return EnterMap(loaded => workshop = loaded);
            Assert.AreEqual(home.Arrival, workshop.Player.Cell, "the workshop puts Kleia at the road's arrival");
            yield return SettleTheStory(workshop);
            yield return WalkIntoEveryTrigger(workshop);
            Assert.IsTrue(ActionUnlocks.IsUnlocked(ActionId.Maieutics, workshop.Dialogue.Story), "Menexeno's door scene teaches maieutics while offline");
            CollectionAssert.IsSubsetOf(FiredCutscenes(workshop), cutscenesPlayed, "every cutscene whose trigger fired plays offline");
            yield return WalkThroughExit(workshop, road);

            yield return EnterMap(loaded => agora = loaded);
            Assert.AreEqual(road.Arrival, agora.Player.Cell);
            yield return SettleTheStory(agora);
            yield return DuelEveryone(agora);
            dialogueAnswers[DamarisOfferDialogue] = branch == DamarisChoice.Accepted ? AcceptOption : RefuseOption;
            Assert.IsTrue(agora.Services.TryLoadActiveSave(out var beforeHome));
            home = agora.Sandbox.Exits.First(link => link.Location == LocationScenes.Kerameikos);
            yield return WalkThroughExit(agora, home);
            yield return EnterMap(loaded => workshop = loaded);
            Assert.IsTrue(workshop.Services.TryLoadActiveSave(out var cameHome));
            Assert.AreEqual(StoryDays.DayOf(beforeHome) + 1, StoryDays.DayOf(cameHome), "coming home ends the day while offline");
            AssertTheWorkshopFeelsThePressure(workshop);
            yield return SettleTheStory(workshop);
            yield return WalkIntoEveryTrigger(workshop);
            CollectionAssert.IsSubsetOf(workshop.Dialogue.Triggers.Where(trigger => trigger.IsCutscene).Select(trigger => trigger.Cutscene).Distinct(), cutscenesPlayed, "every cutscene of the workshop plays offline");
            yield return TalkToEveryone(workshop);
            AssertDamarisWasAnswered(workshop, branch);
            yield return WalkThroughExit(workshop, road);

            yield return EnterMap(loaded => agora = loaded);
            yield return SettleTheStory(agora);
            Assert.AreEqual(branch == DamarisChoice.Refused, Menexeno(agora).Walker != null, "Menexeno stays away from the agora only after Kleia takes Damaris's money");
            yield return TalkToEveryone(agora);
            yield return DuelWithTheAbilityOf(agora, branch);

            yield return FaceTheStreetTrial(agora);

            var archiveDoor = agora.Sandbox.Exits.Single(link => link.Location == LocationScenes.Archive);
            Assert.IsTrue(agora.Sandbox.IsExitOpen(archiveDoor), "searching for Menexeno opens the archive door");
            yield return WalkThroughExit(agora, archiveDoor);
            var archive = default(MapScene);
            yield return EnterMap(loaded => archive = loaded);
            Assert.AreEqual(LocationScenes.Archive, archive.Sandbox.LocationId);
            yield return SettleTheStory(archive);
            yield return TalkToEveryone(archive);
            yield return WalkIntoEveryTrigger(archive);
            Assert.IsTrue(StoryCondition.Has("knows_menexeno_vote").IsMet(archive.Dialogue.Story), "the juror list reveals Menexeno's vote while offline");
            Assert.AreEqual(Act.Two, archive.Dialogue.Story.ActFlag, "the bronze seal sets ActFlag = 2 while offline");
            Assert.IsTrue(archive.Services.TryLoadActiveSave(out var sealedSave));
            Assert.AreEqual(Act.Two, sealedSave.ActFlag, "the second act is saved while offline");
            CollectionAssert.IsSubsetOf(archive.Dialogue.Triggers.Select(trigger => trigger.Cutscene), cutscenesPlayed, "the archive scene plays offline");

            yield return WalkThroughExit(archive, archive.Sandbox.Exits.Single());
            yield return EnterMap(loaded => agora = loaded);
            yield return SettleTheStory(agora);
            var academyRoad = agora.Sandbox.Exits.Single(link => link.Location == LocationScenes.Academy);
            Assert.IsTrue(agora.Sandbox.IsExitOpen(academyRoad), "the Academy road has come out of the fog");
            yield return WalkThroughExit(agora, academyRoad);
            var academy = default(MapScene);
            yield return EnterMap(loaded => academy = loaded);
            Assert.AreEqual(LocationScenes.Academy, academy.Sandbox.LocationId);
            Assert.AreEqual(academyRoad.Arrival, academy.Player.Cell);
            yield return SettleTheStory(academy);
            Assert.IsTrue(StoryCondition.Has("reached_academy").IsMet(academy.Dialogue.Story));
            yield return WalkThroughExit(academy, academy.Sandbox.Exits.Single());
            yield return EnterMap(loaded => agora = loaded);
            yield return SettleTheStory(agora);

            AssertEveryBattleLineWasShown();
            Assert.Greater(opponentLines, 0);
            Assert.Less(longestOpponentTurnSeconds, OfflineThinkSeconds + ThinkSlackSeconds, "an opponent turn took longer than its think time");

            yield return SaveFromThePauseMenuAndQuit(agora);
            yield return WaitForScene(PauseMenuController.DefaultMainMenuScene);

            yield return OpenMainMenu(loaded => menu = loaded);
            Assert.IsTrue(menu.Services.HasContinue, "the offline save is there to continue");
            Assert.IsTrue(menu.Services.TryLoadActiveSave(out var quitSave));
            Choose(menu.MainMenu, SystemMenus.Continue);
            PickSlot(menu.Slots, SaveSlots.DefaultIndex);
            yield return WaitForScene(LocationScenes.SceneFor(quitSave.Location));

            yield return EnterMap(loaded => agora = loaded);
            AssertRestored(agora, quitSave);

            home = agora.Sandbox.Exits.First(link => link.Location == LocationScenes.Kerameikos);
            yield return WalkThroughExit(agora, home);
            yield return EnterMap(loaded => workshop = loaded);
            Assert.AreEqual(home.Arrival, workshop.Player.Cell);

            Assert.IsTrue(workshop.Services.TryLoadActiveSave(out var newest));
            Assert.Greater(newest.Timestamp, quitSave.Timestamp, "arriving home saves over the continued game");
            CollectionAssert.IsSupersetOf(newest.Duels.Keys, duelResults.Where(result => result.Value == BattleStatus.PlayerWon).Select(result => result.Key), "a duel won offline went missing from the local database");
            TestContext.WriteLine($"{branch}: {dialoguesPlayed.Count} conversations, {cutscenesPlayed.Count} cutscenes, {duelResults.Count} duels, {opponentLines} opponent lines, longest opponent turn {longestOpponentTurnSeconds:0.000}s");
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerable<string> FiredCutscenes(MapScene map) =>
            map.Dialogue.Triggers.Where(trigger => trigger.IsCutscene && StoryCondition.Has(trigger.Flag).IsMet(map.Dialogue.Story)).Select(trigger => trigger.Cutscene).Distinct();

        private static void AssertTheWorkshopFeelsThePressure(MapScene map)
        {
            var story = map.Dialogue.Story;
            Assert.AreEqual(StoryPressure.Stages.Count, StoryPressure.Level(story), "every duel of the agora pushed the pressure to its last stage");
            foreach (var customer in map.Dialogue.Talkers.Where(link => link.Dialogue.StartsWith(CustomerDialoguePrefix, StringComparison.Ordinal)))
            {
                Assert.IsNull(customer.Actor.Walker, $"{customer.Dialogue} still comes to the workshop");
            }

            Assert.IsNotEmpty(map.Sandbox.BlockPresences, "the workshop changes its props with the story");
            foreach (var block in map.Sandbox.BlockPresences)
            {
                Assert.AreEqual(block.IsMet(story), map.Sandbox.IsBlockShown(block.Cell), $"the block at {block.Cell} does not follow the pressure");
                Assert.AreEqual(!block.IsMet(story), map.Sandbox.Grid[block.Cell].IsPassable, $"the grid at {block.Cell} does not follow the pressure");
            }

            Assert.IsNotNull(map.Cutscenes.Cast[DamarisCharacter].Walker, "Damaris waits for Kleia in the street");
        }

        private static void AssertDamarisWasAnswered(MapScene map, DamarisChoice branch)
        {
            Assert.IsTrue(map.Services.TryLoadActiveSave(out var saved), "the answer autosaves while offline");
            Assert.AreEqual(branch, saved.DamarisBranch, "the tribunal branch is saved while offline");
            CollectionAssert.AreEqual(new[] { DamarisOffer.GrantedAbility(branch).Value }, AbilityUnlocks.OwnedIn(saved));
            Assert.AreEqual(branch == DamarisChoice.Accepted ? DamarisOffer.AcceptedDrachmas : 0, saved.Inventory.TryGetValue(DamarisOffer.DrachmasItem, out var drachmas) ? drachmas : 0);
            Assert.AreEqual(branch == DamarisChoice.Accepted ? DamarisOffer.MenexenoAbsentDays : 0, StoryDays.AbsenceLeft(saved, DamarisOffer.MenexenoAbsentDaysFlag));
            Assert.AreEqual(branch == DamarisChoice.Refused, StoryCondition.Has(DamarisOffer.MenexenoRespectFlag).IsMet(saved));
            Assert.IsNull(map.Cutscenes.Cast[DamarisCharacter].Walker, "Damaris walks away after the answer");
        }

        private static ActorView Menexeno(MapScene map) =>
            map.Dialogue.Talkers.Single(link => link.Dialogue == MenexenoAgoraDialogue).Actor;

        private static bool IsAbilityLine(DialogueLine line) =>
            string.IsNullOrEmpty(line.SpeakerName) && line.Text != null && (line.Text.StartsWith("INVERSÃO", StringComparison.Ordinal) || line.Text.StartsWith("ESCUTA", StringComparison.Ordinal));

        private IEnumerator DuelWithTheAbilityOf(MapScene map, DamarisChoice branch)
        {
            var ability = DamarisOffer.GrantedAbility(branch).Value;
            var battle = map.Battle;
            var rival = battle.Opponents.First(entry => !battle.IsDefeated(entry) && entry.IsPlaced && !entry.Opponent.IsPhased);
            abilitiesUsed.Clear();
            abilityLines = 0;

            yield return Challenge(map, rival);
            Assert.IsTrue(battle.Session.State.HasAbility(ability), $"Kleia brings {ability} into the duel against {rival.Opponent.Id}");
            yield return PlayDuel(map, DuelPlan.Win);

            CollectionAssert.AreEqual(new[] { ability }, abilitiesUsed, $"{ability} never answered the arguments of {rival.Opponent.Id} offline");
            Assert.Greater(abilityLines, 0, $"the duel never announced {ability}");
        }

        private IEnumerator OpenMainMenu(Action<MainMenuController> loaded)
        {
            var started = Time.realtimeSinceStartup;
            var controller = Object.FindAnyObjectByType<MainMenuController>();
            Assert.IsNotNull(controller, "the main menu scene must contain its controller");
            yield return WaitFor(() => controller.IsReady, SceneTimeoutSeconds);

            Assert.IsTrue(controller.IsReady, "the main menu never opened");
            Assert.LessOrEqual(Time.realtimeSinceStartup - started, BootSlackSeconds, "the main menu took too long to open");
            loaded(controller);
        }

        private IEnumerator EnterMap(Action<MapScene> loaded)
        {
            var started = Time.realtimeSinceStartup;
            yield return null;
            var map = new MapScene(
                Object.FindAnyObjectByType<GridSandboxController>(),
                Object.FindAnyObjectByType<GameServices>(),
                Object.FindAnyObjectByType<ExplorationDialogueDirector>(),
                Object.FindAnyObjectByType<SandboxBattleDirector>(),
                Object.FindAnyObjectByType<PauseMenuController>(),
                Object.FindAnyObjectByType<StoryCueDirector>());
            Assert.IsNotNull(map.Sandbox);
            Assert.IsNotNull(map.Services, $"{map.Sandbox.LocationId} must contain GameServices");
            Assert.IsNotNull(map.Dialogue, $"{map.Sandbox.LocationId} must contain the dialogue director");
            Assert.IsNotNull(map.Pause, $"{map.Sandbox.LocationId} must contain the pause menu");
            if (map.Battle)
            {
                map.Battle.ThinkSeconds = OfflineThinkSeconds;
            }

            yield return WaitFor(() => map.Services.IsBooted && map.Sandbox.ProgressLoaded && map.Dialogue.IsStoryLoaded, SceneTimeoutSeconds);
            Assert.IsTrue(
                map.Services.IsBooted && map.Sandbox.ProgressLoaded && map.Dialogue.IsStoryLoaded,
                $"{map.Sandbox.LocationId} never finished loading: booted {map.Services.IsBooted} ({map.Services.BootTask?.Status}: {map.Services.BootTask?.Exception}), progress {map.Sandbox.ProgressLoaded}, story {map.Dialogue.IsStoryLoaded}");
            Assert.LessOrEqual(Time.realtimeSinceStartup - started, MapReadySeconds, $"{map.Sandbox.LocationId} took too long to load");
            loaded(map);
        }

        private IEnumerator WalkIntoEveryTrigger(MapScene map)
        {
            foreach (var flag in map.Dialogue.Triggers.Select(trigger => trigger.Flag).Distinct().ToList())
            {
                var due = map.Dialogue.Triggers.Where(candidate => candidate.Flag == flag && candidate.IsDue(map.Dialogue.Story)).ToList();
                if (due.Count == 0)
                {
                    continue;
                }

                var trigger = due.FirstOrDefault(candidate => map.Sandbox.TryMovePlayerTo(candidate.Cell));
                Assert.IsNotNull(trigger, $"no trigger for {flag} can be reached on foot");
                var scene = trigger.IsCutscene ? trigger.Cutscene : trigger.Dialogue;
                bool Plays() => trigger.IsCutscene ? map.IsPlayingCutscene && map.Cutscenes.ActiveCutscene == trigger.Cutscene : map.Dialogue.ActiveDialogueId == trigger.Dialogue;
                yield return WalkUntil(map, trigger.Cell, Plays, $"the walk toward {trigger.Cell}");
                Assert.IsTrue(Plays(), $"walking toward {trigger.Cell} never played {scene}");
                if (trigger.IsCutscene)
                {
                    yield return PlayCutscene(map);
                }
                else
                {
                    yield return PlayConversation(map);
                }

                Assert.IsTrue(StoryCondition.Has(flag).IsMet(map.Dialogue.Story), $"{scene} did not record {flag}");
            }
        }

        private IEnumerator WalkUntil(MapScene map, Coord destination, Func<bool> arrived, string walk)
        {
            var player = map.Player;
            var idleFrames = 0;
            var deadline = Time.realtimeSinceStartup + WalkTimeoutSeconds;
            while (!arrived() && Time.realtimeSinceStartup < deadline && map.Sandbox)
            {
                if (map.Dialogue.IsTalking)
                {
                    AssertIsStoryCue(map, map.Dialogue.ActiveDialogueId, walk);
                    yield return PlayConversation(map);
                    idleFrames = 0;
                    continue;
                }

                var idle = !player.IsMoving && !map.Dialogue.HasPendingTrigger && !map.IsPlayingCutscene && map.Sandbox.StateMachine.Mode == GameMode.Exploration;
                idleFrames = idle ? idleFrames + 1 : 0;
                if (idleFrames >= IdleFramesBeforeWalkingOn && player.Cell != destination)
                {
                    Assert.IsTrue(map.Sandbox.TryMovePlayerTo(destination), $"{walk} has no path left from {player.Cell}");
                    idleFrames = 0;
                }

                yield return null;
            }
        }

        private IEnumerator SettleTheStory(MapScene map)
        {
            var quietFrames = 0;
            while (quietFrames < QuietFrames)
            {
                if (map.Dialogue.IsTalking)
                {
                    AssertIsStoryCue(map, map.Dialogue.ActiveDialogueId, $"the quiet in {map.Sandbox.LocationId}");
                    yield return PlayConversation(map);
                    quietFrames = 0;
                    continue;
                }

                quietFrames++;
                yield return null;
            }
        }

        private static void AssertIsStoryCue(MapScene map, string dialogueId, string interrupted)
        {
            var isCue = map.Cues && map.Cues.Cues.Any(cue => cue.Dialogue == dialogueId);
            Assert.IsTrue(isCue, $"{dialogueId} started during {interrupted}, but it is not one of the story cues of {map.Sandbox.LocationId}");
        }

        private IEnumerator PlayCutscene(MapScene map)
        {
            var cutscene = map.Cutscenes.ActiveCutscene;
            var deadline = Time.realtimeSinceStartup + CutsceneTimeoutSeconds;
            while (map.IsPlayingCutscene && Time.realtimeSinceStartup < deadline)
            {
                if (map.Dialogue.IsTalking)
                {
                    yield return PlayConversation(map);
                    continue;
                }

                yield return null;
            }

            Assert.IsFalse(map.IsPlayingCutscene, $"{cutscene} never finished");
            Assert.AreEqual(GameMode.Exploration, map.Sandbox.StateMachine.Mode, $"{cutscene} did not hand the map back to exploration");
            cutscenesPlayed.Add(cutscene);
        }

        private IEnumerator TalkToEveryone(MapScene map)
        {
            var present = map.Dialogue.Talkers.Where(link => link.Actor && link.Actor.Walker != null).ToList();
            Assert.IsNotEmpty(present, $"{map.Sandbox.LocationId} has nobody to talk to");
            yield return SettleTheStory(map);
            foreach (var talker in present)
            {
                var npc = talker.Actor.Walker.Cell;
                map.Sandbox.Player.Walker.Teleport(FreeNeighbour(map, npc), Facing.NorthEast);
                yield return null;

                map.Sandbox.Cursor.TryPoint(npc);
                map.Sandbox.ConfirmCursor();
                yield return WaitFor(() => map.Dialogue.IsTalking, WalkTimeoutSeconds);
                Assert.IsTrue(map.Dialogue.IsTalking, $"{talker.Dialogue} never started while offline");
                Assert.AreEqual(talker.Dialogue, map.Dialogue.ActiveDialogueId);
                yield return PlayConversation(map);
            }
        }

        private IEnumerator PlayConversation(MapScene map)
        {
            var dialogue = map.Dialogue.ActiveDialogueId;
            dialoguesPlayed.Add(dialogue);
            for (var confirms = 0; map.Dialogue.IsTalking && confirms < MaxDialogueConfirms; confirms++)
            {
                var line = map.Dialogue.CurrentLine;
                AssertShowsText(line.Text, map.Dialogue.DialogueBox.Body.text);
                if (!line.IsNarration)
                {
                    AssertShowsText(map.Dialogue.DialogueBox.Line.SpeakerName, map.Dialogue.DialogueBox.SpeakerLabel.text);
                }

                if (map.Dialogue.IsAwaitingChoice && dialogueAnswers.TryGetValue(dialogue, out var answer))
                {
                    for (var step = 0; step < answer; step++)
                    {
                        map.Dialogue.DialogueBox.HandleNavigate(Vector2.down);
                    }
                }

                map.Dialogue.Advance();
                yield return null;
            }

            Assert.IsFalse(map.Dialogue.IsTalking, "a conversation never closed");
            yield return null;
        }

        private IEnumerator WalkThroughExit(MapScene map, MapExitLink exit)
        {
            var player = map.Sandbox.Player.Walker;
            var beside = Neighbours(exit.Cell)
                .First(cell => map.Sandbox.Grid.TryGet(cell, out var tile) && tile.IsPassable && (!tile.IsOccupied || tile.OccupantId == player.ActorId)
                    && map.Sandbox.Exits.All(link => link.Cell != cell));
            player.Teleport(beside, Facing.SouthWest);
            yield return null;

            var scene = LocationScenes.SceneFor(exit.Location);
            Assert.IsTrue(map.Sandbox.TryMovePlayerTo(exit.Cell), $"no path onto the exit to {exit.Location}");
            yield return WalkUntil(map, exit.Cell, () => SceneManager.GetActiveScene().name == scene, $"the walk onto the exit to {exit.Location}");
            yield return WaitForScene(scene);
        }

        private IEnumerator TakeEveryLesson(MapScene map)
        {
            var battle = map.Battle;
            Assert.IsNotNull(battle, "the agora hosts the duels");
            var teachers = battle.Opponents.Where(entry => entry.Opponent.Tutorial.IsPending(map.Dialogue.Story)).ToList();
            Assert.IsNotEmpty(teachers, "a new game starts with a lesson in the agora");
            foreach (var teacher in teachers)
            {
                yield return Challenge(map, teacher);
                Assert.IsNotNull(battle.Tutorial, $"the first duel with {teacher.Opponent.Id} is the lesson");
                yield return PlayDuel(map, DuelPlan.Win);
                Assert.AreEqual(BattleStatus.PlayerWon, duelResults[teacher.Opponent.Id], $"naming the right fallacy must win the lesson with {teacher.Opponent.Id} offline");
                Assert.IsFalse(teacher.Opponent.Tutorial.IsPending(map.Dialogue.Story), $"winning the lesson with {teacher.Opponent.Id} finishes it");
            }
        }

        private IEnumerator DuelEveryone(MapScene map)
        {
            var battle = map.Battle;
            Assert.IsNotNull(battle, "the agora hosts the duels");
            var rivals = battle.Opponents.Where(entry => entry.IsPlaced && !battle.IsDefeated(entry)).ToList();
            var winner = rivals
                .Where(entry => entry.Opponent.Profile == Profile.Sophist)
                .OrderBy(entry => entry.Opponent.MaxCoherence)
                .First();
            var quitter = rivals.Last(entry => entry != winner);
            var explorers = rivals.Where(entry => entry != winner && entry != quitter).ToList();

            yield return Challenge(map, winner);
            yield return PlayDuel(map, DuelPlan.Win);
            Assert.AreEqual(BattleStatus.PlayerWon, duelResults[winner.Opponent.Id], $"naming the right fallacy must beat {winner.Opponent.Id} offline");
            DuelChallenge.StandBeside(map.Sandbox, winner);
            DuelChallenge.Confirm(map.Sandbox, winner.Actor.Walker.Cell);
            yield return Frames(QuietFrames);
            Assert.AreEqual(BattleFlowPhase.Exploring, battle.Phase, $"{winner.Opponent.Id} took up a challenge after losing");

            yield return Challenge(map, quitter);
            yield return PlayDuel(map, DuelPlan.Withdraw);
            Assert.AreEqual(BattleStatus.PlayerWithdrew, duelResults[quitter.Opponent.Id]);
            yield return Challenge(map, quitter);
            yield return PlayDuel(map, DuelPlan.Withdraw);

            foreach (var entry in explorers)
            {
                yield return Challenge(map, entry);
                yield return PlayDuel(map, DuelPlan.Explore);
            }

            Assert.AreEqual(battle.Opponents.Count(entry => entry.IsPlaced), duelResults.Count, "every opponent on the map must be fought");
        }

        private IEnumerator FaceTheStreetTrial(MapScene map)
        {
            var battle = map.Battle;
            var trial = battle.Opponents.Single(entry => entry.Opponent.IsPhased);
            var menexeno = map.Dialogue.Talkers.Single(talker => talker.Dialogue == "menexeno_agora").Actor;
            var menexenoWasThere = menexeno.Walker != null;
            yield return SettleTheStory(map);
            Assert.IsTrue(StoryCondition.Has(TrialCalledFlag).IsMet(map.Dialogue.Story), "Thrasybios calls Kleia to the stoa steps while offline");
            Assert.IsTrue(trial.IsPlaced, "the trial waits on the stoa steps");
            Assert.IsFalse(battle.Opponents.Single(entry => entry.Opponent.Id == TutorOpponent).IsPlaced, "Thrasybios left the market");

            battle.PhaseStarted += phaseCues.Add;
            try
            {
                yield return Challenge(map, trial);
                yield return PlayDuel(map, DuelPlan.Lose);
                Assert.AreEqual(BattleStatus.PlayerLost, duelResults[trial.Opponent.Id], "refuting the personal attack loses the trial");
                Assert.IsTrue(trial.IsPlaced, "defeat is not game over");
                Assert.AreEqual(menexenoWasThere, menexeno.Walker != null, "losing the trial changes nothing for Menexeno");

                phaseCues.Clear();
                yield return Challenge(map, trial);
                yield return PlayDuel(map, DuelPlan.Win);
            }
            finally
            {
                battle.PhaseStarted -= phaseCues.Add;
            }

            Assert.AreEqual(BattleStatus.PlayerWon, duelResults[trial.Opponent.Id], "the three counters win the trial offline");
            CollectionAssert.AreEqual(trial.Opponent.Phases.Phases.Select(phase => phase.Music), phaseCues.Select(cue => cue.MusicId), "every phase raised its music cue");
            Assert.IsFalse(trial.IsPlaced, "Thrasybios leaves the steps");
            Assert.IsNull(menexeno.Walker, "Menexeno disappears after the trial");
            Assert.IsTrue(StoryCondition.Has(SearchingMenexenoFlag).IsMet(map.Dialogue.Story), "Kleia goes looking for him");
        }

        private static IEnumerator Challenge(MapScene map, MapOpponent entry)
        {
            if (DuelChallenge.TryFindFreeCellBeside(map.Sandbox, entry.Actor.Walker.Cell, out _))
            {
                yield return DuelChallenge.Engage(map.Sandbox, map.Battle, entry, SceneTimeoutSeconds);
                yield break;
            }

            TestContext.WriteLine($"the duels moved everyone around {entry.Opponent.Id}, so the duel starts from the nearest free cell");
            map.Player.Teleport(NearestFreeCell(map, entry.Actor.Walker.Cell), Facing.NorthEast);
            map.Battle.StartBattle(entry);
            yield return WaitFor(() => map.Battle.Phase != BattleFlowPhase.Exploring, SceneTimeoutSeconds);
            Assert.AreSame(entry, map.Battle.Engaged, $"the duel against {entry.Opponent.Id} never started");
        }

        private IEnumerator PlayDuel(MapScene map, DuelPlan plan)
        {
            var director = map.Battle;
            var opponent = director.Engaged;
            var lesson = director.Tutorial != null;
            var phased = director.PhasedDuel != null;
            var approaches = 0;
            var playerEntry = -1;
            var thinking = false;
            var thinkingSince = 0f;
            var awaitingOpponentLine = false;
            var deadline = Time.realtimeSinceStartup + BattleTimeoutSeconds;
            while (director.Phase != BattleFlowPhase.Exploring && Time.realtimeSinceStartup < deadline)
            {
                if (director.Phase == BattleFlowPhase.OpponentThinking && !thinking)
                {
                    thinking = true;
                    thinkingSince = Time.realtimeSinceStartup;
                }
                else if (director.Phase != BattleFlowPhase.OpponentThinking && thinking)
                {
                    thinking = false;
                    longestOpponentTurnSeconds = Math.Max(longestOpponentTurnSeconds, Time.realtimeSinceStartup - thinkingSince);
                    awaitingOpponentLine = true;
                }

                if (director.Session != null)
                {
                    abilitiesUsed.UnionWith(director.Session.State.Log.Where(entry => entry.TriggeredAbility.HasValue).Select(entry => entry.TriggeredAbility.Value));
                }

                switch (director.Phase)
                {
                    case BattleFlowPhase.Dialogue:
                        abilityLines += IsAbilityLine(director.Hud.Dialogue.Line) ? 1 : 0;
                        ReadBattleLine(director, opponent, awaitingOpponentLine, playerEntry);
                        awaitingOpponentLine = false;
                        playerEntry = -1;
                        director.AdvanceDialogue();
                        break;
                    case BattleFlowPhase.PlayerChoosing:
                        playerEntry = director.Session.State.Log.Count;
                        director.SubmitChoice(ChooseOffline(director.Session, plan, ref approaches));
                        break;
                    case BattleFlowPhase.PlayerTargeting:
                        if (!director.TryChooseDestination(director.Session.CirculateDestination()))
                        {
                            director.CancelTargeting();
                            playerEntry = director.Session.State.Log.Count;
                            director.SubmitChoice(new BattleMenuChoice(ActionId.Concede));
                        }

                        break;
                }

                yield return null;
            }

            Assert.AreEqual(BattleFlowPhase.Exploring, director.Phase, $"the duel against {opponent.Opponent.Id} did not finish in time");
            duelResults[opponent.Opponent.Id] = director.LastResult;
            var closedByTheLessonScene = lesson && director.LastResult == BattleStatus.PlayerWon;
            var closedByTheTrialScene = phased && director.LastResult is BattleStatus.PlayerWon or BattleStatus.PlayerLost;
            if (!closedByTheLessonScene && !closedByTheTrialScene)
            {
                battleLinesShown.Add(BattleText.StatusKey(director.LastResult));
            }

            yield return SettleTheStory(map);
            Assert.AreEqual(BattleFlowPhase.Exploring, director.Phase, "standing beside the last opponent must not start another duel");
        }

        private void ReadBattleLine(SandboxBattleDirector director, MapOpponent opponent, bool opponentJustPlayed, int playerEntry)
        {
            var box = director.Hud.Dialogue;
            var line = box.Line;
            AssertShowsText(line.Text, box.Body.text);
            if (opponentJustPlayed && line.SpeakerName == opponent.Opponent.DisplayName)
            {
                opponentLines++;
                Assert.IsNotNull(line.Portrait, $"{opponent.Opponent.Id} argued without a portrait");
            }

            var state = director.Session.State;
            if (playerEntry < 0 || playerEntry >= state.Log.Count)
            {
                return;
            }

            var entry = state.Log[playerEntry];
            var key = entry.Rejection == RejectionReason.None ? OutcomeLineKey(entry) : null;
            if (key != null)
            {
                Assert.AreEqual(string.Empty, line.SpeakerName, $"{key} is narrated");
                battleLinesShown.Add(key);
            }
        }

        private BattleMenuChoice ChooseOffline(BattleSession session, DuelPlan plan, ref int approaches)
        {
            bool Available(ActionId action) => session.Menu.Entries.Any(entry => entry.Action == action && entry.IsAvailable);
            bool Missing(string key) => !battleLinesShown.Contains(key);

            if (plan == DuelPlan.Withdraw)
            {
                return new BattleMenuChoice(ActionId.Withdraw);
            }

            if (session.State.Rules.Phases != null && session.State.CurrentArgument.HasValue)
            {
                return ChoosePhaseAnswer(session.State, plan);
            }

            if (Missing(BattleText.QuoteKey) && Available(ActionId.Quote))
            {
                return new BattleMenuChoice(ActionId.Quote);
            }

            var argument = session.State.CurrentArgument;
            if (plan == DuelPlan.Explore && argument.HasValue)
            {
                var presented = argument.Value;
                if (Missing(BattleText.RefuteMissKey) && presented.IsRefutable && Available(ActionId.Refute))
                {
                    return new BattleMenuChoice(ActionId.Refute, refutableIds.First(id => id != presented.FallacyId));
                }

                if (Missing(BattleText.MaieuticsMissKey) && presented.Premises.Count > 1 && Available(ActionId.Maieutics))
                {
                    return new BattleMenuChoice(ActionId.Maieutics, premiseIndex: (presented.WeakPremise + 1) % presented.Premises.Count);
                }

                if (Missing(BattleText.MaieuticsHitKey) && Available(ActionId.Maieutics))
                {
                    return new BattleMenuChoice(ActionId.Maieutics, premiseIndex: presented.WeakPremise);
                }

                if ((Missing(BattleText.MaieuticsHitKey) || Missing(BattleText.MaieuticsMissKey)) && approaches < MaxApproaches && Available(ActionId.Circulate))
                {
                    approaches++;
                    return new BattleMenuChoice(ActionId.Circulate);
                }

                if (Missing(BattleText.ConcedeKey) && Available(ActionId.Concede))
                {
                    return new BattleMenuChoice(ActionId.Concede);
                }
            }

            if (argument.HasValue && argument.Value.IsRefutable && Available(ActionId.Refute))
            {
                return new BattleMenuChoice(ActionId.Refute, argument.Value.FallacyId);
            }

            if (argument.HasValue && Available(ActionId.Maieutics))
            {
                return new BattleMenuChoice(ActionId.Maieutics, premiseIndex: argument.Value.WeakPremise);
            }

            return Available(ActionId.Circulate) ? new BattleMenuChoice(ActionId.Circulate) : new BattleMenuChoice(ActionId.Concede);
        }

        private static BattleMenuChoice ChoosePhaseAnswer(BattleState state, DuelPlan plan)
        {
            var argument = state.CurrentArgument.Value;
            if (plan == DuelPlan.Lose)
            {
                return new BattleMenuChoice(ActionId.Refute, argument.FallacyId);
            }

            return state.Rules.Phases.PhaseFor(state.Opponent.Coherence).Counter switch
            {
                ActionId.Refute => new BattleMenuChoice(ActionId.Refute, argument.FallacyId),
                ActionId.Maieutics => new BattleMenuChoice(ActionId.Maieutics, premiseIndex: argument.WeakPremise),
                _ => new BattleMenuChoice(ActionId.Concede)
            };
        }

        private IEnumerator SaveFromThePauseMenuAndQuit(MapScene map)
        {
            var pause = map.Pause;
            var services = map.Services;
            Assert.AreSame(pause, pause.Input.Panel, "exploration hands the input back to the pause menu");
            pause.Input.Panel.HandleCancel();
            Assert.IsTrue(pause.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);

            Choose(pause.PauseMenu, SystemMenus.Compendium);
            Assert.AreSame(pause.Compendium, pause.Input.Panel);
            Assert.IsNotNull(pause.Compendium.SelectedId, "the fallacies refuted offline show up in the compendium");
            pause.Compendium.HandleCancel();

            Choose(pause.PauseMenu, SystemMenus.Options);
            var volume = services.Settings.VolumeStep;
            var louder = volume < GameSettings.VolumeSteps;
            pause.Options.HandleNavigate(louder ? Vector2.right : Vector2.left);
            Assert.AreNotEqual(volume, services.Settings.VolumeStep, "the options still apply offline");
            pause.Options.HandleNavigate(louder ? Vector2.left : Vector2.right);
            pause.Options.HandleCancel();

            Assert.IsTrue(services.TryLoadActiveSave(out var beforeSaving));
            Choose(pause.PauseMenu, SystemMenus.Save);
            PickSlot(pause.Slots, SaveSlots.DefaultIndex);
            Assert.IsTrue(services.TryLoadActiveSave(out var saved));
            Assert.Greater(saved.Timestamp, beforeSaving.Timestamp, "SALVAR must write the slot while offline");
            Assert.AreEqual(map.Player.Cell, saved.Position);
            Assert.AreSame(pause.PauseMenu, pause.Input.Panel);

            Choose(pause.PauseMenu, SystemMenus.MainMenu);
            yield return null;
        }

        private void AssertRestored(MapScene map, SaveData save)
        {
            var standing = map.Player.Cell;
            var savedCellHolder = map.Sandbox.Grid[save.Position].OccupantId;
            if (savedCellHolder == map.Player.ActorId)
            {
                Assert.AreEqual(save.Position, standing, "CONTINUAR must put Kleia back where she was");
            }
            else
            {
                Assert.LessOrEqual(PositionalModifiers.Distance(save.Position, standing), MaxRestoreShift, "CONTINUAR must put Kleia next to the saved cell another actor holds");
            }

            foreach (var won in duelResults.Where(result => result.Value == BattleStatus.PlayerWon).Select(result => result.Key))
            {
                var entry = map.Battle.Opponents.Single(candidate => candidate.Opponent.Id == won);
                Assert.IsTrue(map.Battle.IsDefeated(entry), $"CONTINUAR forgot the duel won against {won}");
            }
        }

        private void AssertEveryBattleLineWasShown()
        {
            var missing = BattleLinesToShow.Where(key => !battleLinesShown.Contains(key)).ToList();
            Assert.IsEmpty(missing, $"these battle lines never showed up offline: {string.Join(", ", missing)}");
        }

        private static string OutcomeLineKey(BattleLogEntry entry) => entry.Deflected ? BattleText.DeflectedKey : entry.Action switch
        {
            ActionId.Refute => entry.Hit ? BattleText.RefuteHitKey : BattleText.RefuteMissKey,
            ActionId.Maieutics => entry.Hit ? BattleText.MaieuticsHitKey : BattleText.MaieuticsMissKey,
            ActionId.Concede => entry.Hit ? BattleText.ConcedeCounterKey : BattleText.ConcedeKey,
            ActionId.Quote => BattleText.QuoteKey,
            _ => null
        };

        private static void Choose(MenuListView menu, string id)
        {
            var row = -1;
            for (var index = 0; index < menu.Model.Entries.Count; index++)
            {
                row = menu.Model.Entries[index].Id == id ? index : row;
            }

            Assert.GreaterOrEqual(row, 0, $"{id} is not on the menu");
            Assert.IsTrue(menu.Model.Entries[row].Enabled, $"{id} is disabled");
            menu.HandlePoint(row);
            menu.HandleConfirm();
        }

        private static void PickSlot(SaveSlotsView slots, int index)
        {
            Assert.IsTrue(slots.gameObject.activeSelf, "the slot list never opened");
            slots.HandlePoint(index);
            slots.HandleConfirm();
            if (slots.Model.Step != SaveSlotsStep.ConfirmingOverwrite)
            {
                return;
            }

            slots.HandlePoint(SaveSlotsModel.ConfirmYes);
            slots.HandleConfirm();
        }

        private static void AssertShowsText(string line, string shown)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(line), "a line is empty");
            Assert.IsFalse(line.StartsWith(UiTextSO.MissingPrefix, StringComparison.Ordinal), $"missing text key {line}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(shown), "the box shows nothing");
        }

        private static Coord FreeNeighbour(MapScene map, Coord npc)
        {
            foreach (var cell in Neighbours(npc))
            {
                if (map.Sandbox.Grid.TryGet(cell, out var tile) && tile.IsPassable && (!tile.IsOccupied || tile.OccupantId == map.Player.ActorId)
                    && map.Sandbox.Exits.All(link => link.Cell != cell) && map.Dialogue.Triggers.All(trigger => trigger.Cell != cell))
                {
                    return cell;
                }
            }

            Assert.Fail($"nobody can stand beside {npc}");
            return npc;
        }

        private static Coord NearestFreeCell(MapScene map, Coord center) =>
            FirstCellAround(map, center, map.Sandbox.Grid.Width + map.Sandbox.Grid.Height, _ => true, $"no free cell anywhere near {center}");

        private static Coord FirstCellAround(MapScene map, Coord center, int farthest, Func<Coord, bool> accepts, string failure)
        {
            foreach (var cell in CellsAround(map, center, farthest).Where(accepts))
            {
                return cell;
            }

            Assert.Fail(failure);
            return center;
        }

        private static IEnumerable<Coord> CellsAround(MapScene map, Coord center, int farthest)
        {
            for (var distance = 1; distance <= farthest; distance++)
            {
                for (var y = 0; y < map.Sandbox.Grid.Height; y++)
                {
                    for (var x = 0; x < map.Sandbox.Grid.Width; x++)
                    {
                        var cell = new Coord(x, y);
                        if (PositionalModifiers.Distance(cell, center) == distance && IsFreeFor(map, cell))
                        {
                            yield return cell;
                        }
                    }
                }
            }
        }

        private static bool IsFreeFor(MapScene map, Coord cell) =>
            map.Sandbox.Grid.TryGet(cell, out var tile) && tile.IsPassable && (!tile.IsOccupied || tile.OccupantId == map.Player.ActorId)
            && map.Sandbox.Exits.All(link => link.Cell != cell);

        private static IEnumerable<Coord> Neighbours(Coord cell) => new[]
        {
            new Coord(cell.X + 1, cell.Y),
            new Coord(cell.X - 1, cell.Y),
            new Coord(cell.X, cell.Y + 1),
            new Coord(cell.X, cell.Y - 1)
        };

        private static IEnumerator WaitForScene(string scene)
        {
            yield return WaitFor(() => SceneManager.GetActiveScene().name == scene, SceneTimeoutSeconds);
            Assert.AreEqual(scene, SceneManager.GetActiveScene().name, $"{scene} never loaded");
        }

        private static IEnumerator Frames(int count)
        {
            for (var frame = 0; frame < count; frame++)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        private sealed class MapScene
        {
            public MapScene(GridSandboxController sandbox, GameServices services, ExplorationDialogueDirector dialogue, SandboxBattleDirector battle, PauseMenuController pause, StoryCueDirector cues)
            {
                Sandbox = sandbox;
                Services = services;
                Dialogue = dialogue;
                Battle = battle;
                Pause = pause;
                Cues = cues;
            }

            public GridSandboxController Sandbox { get; }

            public GameServices Services { get; }

            public ExplorationDialogueDirector Dialogue { get; }

            public SandboxBattleDirector Battle { get; }

            public PauseMenuController Pause { get; }

            public StoryCueDirector Cues { get; }

            public CutsceneDirector Cutscenes => Dialogue.Cutscenes;

            public bool IsPlayingCutscene => Cutscenes && Cutscenes.IsPlaying;

            public GridWalker Player => Sandbox.Player.Walker;
        }
    }
}
