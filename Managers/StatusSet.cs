using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;

namespace RezPls.Managers
{
    public class StatusSet
    {
        private readonly DalamudPluginInterface     _pi;
        private readonly RezPlsConfig               _config;
        private readonly SortedList<ushort, Status> _enabledStatusSet;
        private readonly SortedList<ushort, Status> _disabledStatusSet;

        public IList<Status> ListStatusSet
            => Sets.Item1.Values;

        public IList<Status> RestStatusSet
            => Sets.Item2.Values;

        public bool IsEnabled(ushort statusId)
            => _enabledStatusSet.ContainsKey(statusId);

        private (SortedList<ushort, Status>, SortedList<ushort, Status>) Sets
            => _config.InvertStatusSet ? (_enabledStatusSet, _disabledStatusSet) : (_disabledStatusSet, _enabledStatusSet);

        public StatusSet(DalamudPluginInterface pi, RezPlsConfig config)
        {
            _pi     = pi;
            _config = config;
            var sheet = pi.Data.GetExcelSheet<Status>();
            _enabledStatusSet = new SortedList<ushort, Status>(sheet.Where(s => s.CanDispel && s.Name.RawData.Length > 0)
                .ToDictionary(s => (ushort) s.RowId, s => s));
            _disabledStatusSet = new SortedList<ushort, Status>(_enabledStatusSet.Count);

            var bad = false;
            foreach (var statusId in _config.ChosenStatuses)
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

            if (bad)
            {
                _config.ChosenStatuses = _disabledStatusSet.Select(kvp => kvp.Key).ToHashSet();
                _pi.SavePluginConfig(_config);
            }

            if (_config.InvertStatusSet)
            {
                var tmp = _enabledStatusSet;
                _enabledStatusSet  = _disabledStatusSet;
                _disabledStatusSet = tmp;
            }
        }

        public void Swap(ushort statusId)
        {
            var (manual, rest) = Sets;
            if (manual.TryGetValue(statusId, out var status))
            {
                rest.Add(statusId, status);
                manual.Remove(statusId);
                _config.ChosenStatuses.Remove(statusId);
                _pi.SavePluginConfig(_config);
            }
            else if (rest.TryGetValue(statusId, out status))
            {
                manual.Add(statusId, status);
                rest.Remove(statusId);
                _config.ChosenStatuses.Add(statusId);
                _pi.SavePluginConfig(_config);
            }
            else
            {
                PluginLog.Warning($"Trying to swap Status {statusId}, but it is not a valid status.");
            }
        }

        public void ClearList()
        {
            var (rest, list) = Sets;
            foreach (var s in list)
                rest.Add(s.Key, s.Value);
            list.Clear();
            _config.ChosenStatuses.Clear();
            _pi.SavePluginConfig(_config);
        }
    }
}
