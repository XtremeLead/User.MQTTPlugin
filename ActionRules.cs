using System;
using System.Collections.Generic;
using System.Linq;

namespace User.MQTTPlugin
{
    /// <summary>
    /// The outcome of checking the action rows from the settings page.
    /// </summary>
    internal sealed class ActionsValidation
    {
        public ActionsValidation(List<MqttActionSetting> actions, IList<int> savedRows, int skippedCount, IList<string> duplicateNames)
        {
            Actions = actions;
            SavedRows = savedRows;
            SkippedCount = skippedCount;
            DuplicateNames = duplicateNames;
        }

        /// <summary>The actions, in row order.</summary>
        public List<MqttActionSetting> Actions { get; }

        /// <summary>Indexes of the rows that became an action.</summary>
        public IList<int> SavedRows { get; }

        /// <summary>Rows skipped because a field was empty.</summary>
        public int SkippedCount { get; }

        /// <summary>Names used by more than one row, ignoring case (D6). Saving is refused when there are any.</summary>
        public IList<string> DuplicateNames { get; }

        public bool HasDuplicates => DuplicateNames.Count > 0;
    }

    internal static class ActionRules
    {
        /// <summary>
        /// Turns rows of (name, topic, message) into actions: incomplete rows are skipped, and a
        /// name that was already used, ignoring case, is reported as a duplicate.
        /// </summary>
        internal static ActionsValidation Validate(IList<ValueTuple<string, string, string>> rows)
        {
            var actions = new List<MqttActionSetting>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var savedRows = new List<int>();
            var duplicates = new List<string>();
            int skipped = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                string name = rows[i].Item1;
                string topic = rows[i].Item2;
                string message = rows[i].Item3;

                if (!SettingsViewLogic.IsCompleteAction(name, topic, message))
                {
                    skipped++;
                    continue;
                }
                if (!seen.Add(name))
                {
                    if (!duplicates.Contains(name, StringComparer.OrdinalIgnoreCase)) duplicates.Add(name);
                    continue;
                }
                actions.Add(new MqttActionSetting(name, topic, message));
                savedRows.Add(i);
            }

            return new ActionsValidation(actions, savedRows, skipped, duplicates);
        }
    }
}
