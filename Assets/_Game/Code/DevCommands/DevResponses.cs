using System.Collections.Generic;
using System.Linq;
using Game.Battle;
using Game.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Game.DevCommands
{
    internal static class DevJson
    {
        public static string Serialize(object value) => JsonConvert.SerializeObject(value, Formatting.Indented, new StringEnumConverter());

        public static string Error(string message) => Serialize(new ErrorResponse(message));
    }

    internal sealed class ErrorResponse
    {
        public ErrorResponse(string message)
        {
            Message = message;
        }

        [JsonProperty("success")]
        public bool Success => false;

        [JsonProperty("error")]
        public string Message { get; }
    }

    internal sealed class DebaterSnapshot
    {
        public DebaterSnapshot(Debater debater)
        {
            Id = debater.Id;
            Side = debater.Side;
            Profile = debater.Profile;
            Coherence = debater.Coherence;
            MaxCoherence = debater.MaxCoherence;
            X = debater.Position.X;
            Y = debater.Position.Y;
        }

        [JsonProperty("id")] public int Id { get; }
        [JsonProperty("side")] public Side Side { get; }
        [JsonProperty("profile")] public Profile Profile { get; }
        [JsonProperty("coherence")] public int Coherence { get; }
        [JsonProperty("max_coherence")] public int MaxCoherence { get; }
        [JsonProperty("x")] public int X { get; }
        [JsonProperty("y")] public int Y { get; }
    }

    internal sealed class ArgumentSnapshot
    {
        public ArgumentSnapshot(Argument argument)
        {
            Speech = argument.Speech;
            FallacyId = argument.FallacyId;
            Premises = argument.Premises.ToList();
            WeakPremise = argument.WeakPremise;
            Tone = argument.Tone;
        }

        [JsonProperty("speech")] public string Speech { get; }
        [JsonProperty("fallacy_id")] public string FallacyId { get; }
        [JsonProperty("premises")] public IReadOnlyList<string> Premises { get; }
        [JsonProperty("weak_premise")] public int WeakPremise { get; }
        [JsonProperty("tone")] public string Tone { get; }
    }

    internal sealed class LogEntrySnapshot
    {
        public LogEntrySnapshot(BattleLogEntry entry)
        {
            LogId = entry.LogId;
            Turn = entry.Turn;
            ActorId = entry.ActorId;
            Action = entry.Action;
            Rejection = entry.Rejection;
            Hit = entry.Hit;
            Counterattack = entry.Counterattack;
            PlayerCoherence = entry.PlayerCoherence;
            OpponentCoherence = entry.OpponentCoherence;
            PlayerAudience = entry.PlayerAudience;
            Status = entry.Status;
        }

        [JsonProperty("log_id")] public string LogId { get; }
        [JsonProperty("turn")] public int Turn { get; }
        [JsonProperty("actor_id")] public int ActorId { get; }
        [JsonProperty("action")] public ActionId Action { get; }
        [JsonProperty("rejection")] public RejectionReason Rejection { get; }
        [JsonProperty("hit")] public bool Hit { get; }
        [JsonProperty("counterattack")] public bool Counterattack { get; }
        [JsonProperty("player_coherence")] public int PlayerCoherence { get; }
        [JsonProperty("opponent_coherence")] public int OpponentCoherence { get; }
        [JsonProperty("player_audience")] public int PlayerAudience { get; }
        [JsonProperty("status")] public BattleStatus Status { get; }
    }

    internal sealed class BattleSnapshot
    {
        public BattleSnapshot(BattleState state)
        {
            Status = state.Status;
            Act = state.Act;
            Turn = state.Turn;
            PlayerAudience = state.PlayerAudience;
            CurrentActorId = state.Queue.Current;
            UpcomingActorIds = state.Queue.Preview(3);
            Debaters = state.Debaters.Select(debater => new DebaterSnapshot(debater)).ToList();
            CurrentArgument = state.CurrentArgument.HasValue ? new ArgumentSnapshot(state.CurrentArgument.Value) : null;
            Log = state.Log.Select(entry => new LogEntrySnapshot(entry)).ToList();
        }

        [JsonProperty("success")] public bool Success => true;
        [JsonProperty("status")] public BattleStatus Status { get; }
        [JsonProperty("act")] public Act Act { get; }
        [JsonProperty("turn")] public int Turn { get; }
        [JsonProperty("player_audience")] public int PlayerAudience { get; }
        [JsonProperty("current_actor_id")] public int CurrentActorId { get; }
        [JsonProperty("upcoming_actor_ids")] public IReadOnlyList<int> UpcomingActorIds { get; }
        [JsonProperty("debaters")] public IReadOnlyList<DebaterSnapshot> Debaters { get; }
        [JsonProperty("current_argument")] public ArgumentSnapshot CurrentArgument { get; }
        [JsonProperty("log")] public IReadOnlyList<LogEntrySnapshot> Log { get; }
    }

    internal sealed class SimulationReport
    {
        [JsonProperty("success")] public bool Success => true;
        [JsonProperty("battles")] public int Battles { get; set; }
        [JsonProperty("act")] public Act Act { get; set; }
        [JsonProperty("opponent_profile")] public Profile OpponentProfile { get; set; }
        [JsonProperty("seed")] public int Seed { get; set; }
        [JsonProperty("wins")] public int Wins { get; set; }
        [JsonProperty("losses")] public int Losses { get; set; }
        [JsonProperty("withdrawals")] public int Withdrawals { get; set; }
        [JsonProperty("turn_limit")] public int TurnLimit { get; set; }
        [JsonProperty("average_turns")] public double AverageTurns { get; set; }
        [JsonProperty("first_battle_log")] public IReadOnlyList<LogEntrySnapshot> FirstBattleLog { get; set; }
    }

    internal sealed class GridCommandResponse
    {
        public GridCommandResponse(bool success, string action, int affected, int actorCount, int x, int y)
        {
            Success = success;
            Action = action;
            Affected = affected;
            ActorCount = actorCount;
            X = x;
            Y = y;
        }

        [JsonProperty("success")] public bool Success { get; }
        [JsonProperty("action")] public string Action { get; }
        [JsonProperty("affected")] public int Affected { get; }
        [JsonProperty("actor_count")] public int ActorCount { get; }
        [JsonProperty("x")] public int X { get; }
        [JsonProperty("y")] public int Y { get; }
    }
}
