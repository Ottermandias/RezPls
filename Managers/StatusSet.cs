using System.Collections.Generic;
using System.Linq;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;

namespace RezPls.Managers
{
    public class StatusSet
    {
        private readonly SortedList<ushort, (Status, string)> _enabledStatusSet;
        private readonly SortedList<ushort, (Status, string)> _disabledStatusSet;

        public IList<(Status, string)> EnabledStatusSet
            => _enabledStatusSet.Values;

        public IList<(Status, string)> DisabledStatusSet
            => _disabledStatusSet.Values;

        public bool IsEnabled(ushort statusId)
            => _enabledStatusSet.ContainsKey(statusId);

        public StatusSet()
        {
            var sheet = RezPls.GameData.GetExcelSheet<Status>();
            _enabledStatusSet = new SortedList<ushort, (Status, string)>(sheet!.Where(s => s.CanDispel && s.Name.RawData.Length > 0)
                .ToDictionary(s => (ushort) s.RowId, s => (s, s.Name.ToString().ToLowerInvariant())));
            _disabledStatusSet = new SortedList<ushort, (Status, string)>(_enabledStatusSet.Count);

            var bad = false;
            foreach (var statusId in RezPls.Config.UnmonitoredStatuses)
            {
                if (_enabledStatusSet.TryGetValue(statusId, out var status))
                {
                    _disabledStatusSet[statusId] = status;
                    _enabledStatusSet.Remove(statusId);
                }
                else
                {
                    bad = true;
                }
            }

            if (!bad)
                return;

            RezPls.Config.UnmonitoredStatuses = _disabledStatusSet.Select(kvp => kvp.Key).ToHashSet();
            RezPls.Config.Save();
        }

        public void Swap(ushort statusId)
        {
            if (_enabledStatusSet.TryGetValue(statusId, out var status))
            {
                for (var i = 0; i < _enabledStatusSet.Count; ++i)
                {
                    var (key, value) = _enabledStatusSet.ElementAt(i);
                    if (value.Item2 != status.Item2)
                        continue;

                    _disabledStatusSet.Add(key, value);
                    _enabledStatusSet.Remove(key);
                    RezPls.Config.UnmonitoredStatuses.Add(key);
                    --i;
                }

                RezPls.Config.Save();
            }
            else if (_disabledStatusSet.TryGetValue(statusId, out status))
            {
                for (var i = 0; i < _disabledStatusSet.Count; ++i)
                {
                    var (key, value) = _disabledStatusSet.ElementAt(i);
                    if (value.Item2 != status.Item2)
                        continue;

                    _enabledStatusSet.Add(key, value);
                    _disabledStatusSet.Remove(key);
                    RezPls.Config.UnmonitoredStatuses.Remove(key);
                    --i;
                }

                RezPls.Config.Save();
            }
            else
            {
                PluginLog.Warning($"Trying to swap Status {statusId}, but it is not a valid status.");
            }
        }

        public void ClearEnabledList()
        {
            var previousCount = RezPls.Config.UnmonitoredStatuses.Count;
            foreach (var (key, value) in _enabledStatusSet)
            {
                _disabledStatusSet.Add(key, value);
                RezPls.Config.UnmonitoredStatuses.Add(key);
            }

            _enabledStatusSet.Clear();
            if (previousCount != RezPls.Config.UnmonitoredStatuses.Count)
                RezPls.Config.Save();
        }

        public void ClearDisabledList()
        {
            var previousCount = RezPls.Config.UnmonitoredStatuses.Count;
            foreach (var (key, value) in _disabledStatusSet)
                _enabledStatusSet.Add(key, value);
            _disabledStatusSet.Clear();
            RezPls.Config.UnmonitoredStatuses.Clear();
            if (previousCount != 0)
                RezPls.Config.Save();
        }
    }
}
