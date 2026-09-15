using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Net;
using Game.UI;
using Unity.Pipeline.Commands;
using UnityEngine;

namespace Game.DevCommands
{
    public static class GftCommands
    {
        public const string Tag = "gft";
        public const int DefaultSeed = 402;
        public const int MaxSimulatedBattles = 1000;

        private const string SandboxNotRunning = "GridSandbox is not running in Play Mode";

        [CliCommand("validate_argument", "Validate an argument JSON file (one object or an array) against the contract and the fallacy catalog. Returns JSON.", Tags = new[] { Tag })]
        public static string ValidateArgument(
            [CliArg("file", "Path to the JSON file, absolute or relative to the project root", Required = true)] string file = "")
        {
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
            {
                return DevJson.Error($"file not found: {file}");
            }

            if (!DevContent.TryLoad(out var content))
            {
                return DevJson.Error(DevContent.Unavailable);
            }

            return DevJson.Serialize(ArgumentValidationReport.Validate(File.ReadAllText(file), content.Validator));
        }

        [CliCommand("battle_start", "Create a duel against an opponent profile, let it open with a fallback argument, and return the battle state as JSON.", Tags = new[] { Tag })]
        public static string BattleStart(
            [CliArg("opponent", "Opponent profile: sophist or philosopher", Required = true)] string opponent = "sophist",
            [CliArg("fallacy", "Catalog id used for the opening argument")] string fallacy = "ad_hominem",
            [CliArg("act", "Act number: 1 or 2")] int act = 1,
            [CliArg("seed", "Random seed")] int seed = DefaultSeed)
        {
            if (!TryParseProfile(opponent, out var profile))
            {
                return DevJson.Error($"unknown opponent profile: {opponent}");
            }

            if (!TryParseAct(act, out var parsedAct))
            {
                return DevJson.Error($"act must be 1 or 2, got {act}");
            }

            if (!DevContent.TryLoad(out var content))
            {
                return DevJson.Error(DevContent.Unavailable);
            }

            var library = new FallbackArgumentLibrary(content.Library, content.Validator, seed);
            if (!library.TryNext(fallacy, out var argument))
            {
                return DevJson.Error($"no fallback argument for catalog entry: {fallacy}");
            }

            var state = BattleArena.CreateDuel(parsedAct, profile, seed, BattleArena.OpponentOpens);
            state.Apply(new BattleResolver().Resolve(Play.Argue(BattleArena.OpponentId, BattleArena.PlayerId, argument), state));
            return DevJson.Serialize(new BattleSnapshot(state));
        }

        [CliCommand("battle_simulate", "Run N simulated battles (random player against the opponent brain) and return win rates plus the first battle's resolver log as JSON.", Tags = new[] { Tag })]
        public static string BattleSimulate(
            [CliArg("n", "Number of battles, 1 to 1000", Required = true)] int n = 10,
            [CliArg("act", "Act number: 1 or 2")] int act = 1,
            [CliArg("opponent", "Opponent profile: sophist or philosopher")] string opponent = "sophist",
            [CliArg("seed", "Random seed")] int seed = DefaultSeed)
        {
            if (n < 1 || n > MaxSimulatedBattles)
            {
                return DevJson.Error($"n must be between 1 and {MaxSimulatedBattles}, got {n}");
            }

            if (!TryParseProfile(opponent, out var profile) || !TryParseAct(act, out var parsedAct))
            {
                return DevJson.Error($"invalid opponent '{opponent}' or act {act}");
            }

            if (!DevContent.TryLoad(out var content))
            {
                return DevJson.Error(DevContent.Unavailable);
            }

            var pool = parsedAct == Act.One ? content.RefutableIds : content.ParadoxIds;
            var library = new FallbackArgumentLibrary(content.Library, content.Validator, seed);
            var report = new SimulationReport { Battles = n, Act = parsedAct, OpponentProfile = profile, Seed = seed };
            var totalTurns = 0;

            for (var battle = 0; battle < n; battle++)
            {
                var state = BattleArena.CreateDuel(parsedAct, profile, seed + battle);
                var result = BattleSimulator.Run(
                    state,
                    new RandomPlayerPolicy(new System.Random(seed + battle), content.RefutableIds),
                    new OpponentBrain(new System.Random(seed * 31 + battle)),
                    new CatalogArgumentSource(library, pool, seed + battle));

                totalTurns += result.Turns;
                report.Wins += result.Status == BattleStatus.PlayerWon ? 1 : 0;
                report.Losses += result.Status == BattleStatus.PlayerLost ? 1 : 0;
                report.Withdrawals += result.Status == BattleStatus.PlayerWithdrew ? 1 : 0;
                report.TurnLimit += result.Status == BattleStatus.TurnLimitReached ? 1 : 0;
                if (battle == 0)
                {
                    report.FirstBattleLog = state.Log.Select(entry => new LogEntrySnapshot(entry)).ToList();
                }
            }

            report.AverageTurns = (double)totalTurns / n;
            return DevJson.Serialize(report);
        }

        [CliCommand("grid_spawn", "Spawn a test actor on a GridSandbox cell (Play Mode only). Returns JSON.", Tags = new[] { Tag })]
        public static string GridSpawn(
            [CliArg("x", "Grid x", Required = true)] int x = 0,
            [CliArg("y", "Grid y", Required = true)] int y = 0)
        {
            if (!TryFindSandbox(out var sandbox))
            {
                return DevJson.Error(SandboxNotRunning);
            }

            var spawned = sandbox.TrySpawnTestActor(new Coord(x, y));
            return DevJson.Serialize(new GridCommandResponse(spawned, "grid_spawn", spawned ? 1 : 0, sandbox.Actors.Count, x, y));
        }

        [CliCommand("grid_clear", "Remove every test actor spawned in the GridSandbox (Play Mode only). Returns JSON.", Tags = new[] { Tag })]
        public static string GridClear()
        {
            if (!TryFindSandbox(out var sandbox))
            {
                return DevJson.Error(SandboxNotRunning);
            }

            var removed = sandbox.ClearTestActors();
            return DevJson.Serialize(new GridCommandResponse(true, "grid_clear", removed, sandbox.Actors.Count, 0, 0));
        }

        private static bool TryFindSandbox(out GridSandboxController sandbox)
        {
            sandbox = Application.isPlaying ? UnityEngine.Object.FindAnyObjectByType<GridSandboxController>() : null;
            return sandbox != null && sandbox.Grid != null;
        }

        private static bool TryParseProfile(string value, out Profile profile) =>
            Enum.TryParse(value, true, out profile) && Enum.IsDefined(typeof(Profile), profile);

        private static bool TryParseAct(int value, out Act act)
        {
            act = (Act)value;
            return Enum.IsDefined(typeof(Act), act);
        }

        private sealed class CatalogArgumentSource : IArgumentSource
        {
            private readonly FallbackArgumentLibrary library;
            private readonly IReadOnlyList<string> entries;
            private readonly System.Random random;

            public CatalogArgumentSource(FallbackArgumentLibrary library, IReadOnlyList<string> entries, int seed)
            {
                this.library = library;
                this.entries = entries;
                random = new System.Random(seed);
            }

            public bool TryNext(Debater opponent, out Argument argument) =>
                library.TryNext(entries[random.Next(entries.Count)], out argument);
        }
    }
}
