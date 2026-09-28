using System;
using System.IO;
using Game.Core;
using Game.Net;
using Newtonsoft.Json.Linq;
using Unity.Pipeline.Commands;
using UnityEngine;

namespace Game.DevCommands
{
    public static class SaveCommands
    {
        [CliCommand("save_dump", "Return the local save slot as JSON.", Tags = new[] { GftCommands.Tag })]
        public static string SaveDump(
            [CliArg("directory", "Data directory holding gft.db; defaults to <persistentDataPath>")] string directory = "",
            [CliArg("slot", "Save slot index, from 0 to 2")] int slot = SaveSlots.DefaultIndex)
        {
            if (!TryOpen(directory, slot, out var store, out var openError))
            {
                return DevJson.Error(openError);
            }

            if (!store.TryLoad(out var save, out var error))
            {
                return DevJson.Error(error);
            }

            return Respond(store, save, "save_dump");
        }

        [CliCommand("save_load", "Validate a save JSON file and install it into the local save slot. Returns JSON.", Tags = new[] { GftCommands.Tag })]
        public static string SaveLoad(
            [CliArg("file", "Path to a save JSON file", Required = true)] string file = "",
            [CliArg("directory", "Data directory holding gft.db; defaults to <persistentDataPath>")] string directory = "",
            [CliArg("slot", "Save slot index, from 0 to 2")] int slot = SaveSlots.DefaultIndex)
        {
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
            {
                return DevJson.Error($"file not found: {file}");
            }

            if (!SaveJson.TryDeserialize(File.ReadAllText(file), out var save, out var error))
            {
                return DevJson.Error($"invalid save: {error}");
            }

            if (!TryOpen(directory, slot, out var store, out var openError))
            {
                return DevJson.Error(openError);
            }

            store.Save(save);
            return Respond(store, save, "save_load");
        }

        [CliCommand("set_flag", "Set a narrative flag (act, damaris_branch, reputation, position or a custom snake_case flag) in the local save. Returns JSON.", Tags = new[] { GftCommands.Tag })]
        public static string SetFlag(
            [CliArg("name", "Flag name", Required = true)] string name = "",
            [CliArg("value", "Flag value", Required = true)] string value = "",
            [CliArg("directory", "Data directory holding gft.db; defaults to <persistentDataPath>")] string directory = "",
            [CliArg("slot", "Save slot index, from 0 to 2")] int slot = SaveSlots.DefaultIndex)
        {
            if (!TryOpen(directory, slot, out var store, out var openError))
            {
                return DevJson.Error(openError);
            }

            var save = store.Exists && store.TryLoad(out var existing, out _) ? existing : new SaveData();
            if (!SaveFlags.TrySet(save, name, value, out var error))
            {
                return DevJson.Error(error);
            }

            save.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            store.Save(save);
            return Respond(store, save, "set_flag");
        }

        private static bool TryOpen(string directory, int slot, out LocalSaveStore store, out string error)
        {
            store = null;
            error = null;
            try
            {
                var root = string.IsNullOrWhiteSpace(directory) ? Application.persistentDataPath : directory;
                store = new LocalSaveStore(new SaveDatabase(root), slot);
                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static string Respond(LocalSaveStore store, SaveData save, string action) =>
            new JObject
            {
                ["success"] = true,
                ["action"] = action,
                ["path"] = store.DatabasePath,
                ["slot"] = store.Slot,
                ["save"] = JObject.Parse(SaveJson.Serialize(save))
            }.ToString();
    }
}
