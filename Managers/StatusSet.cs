using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;

namespace RezPls.Managers
{
    public class StatusSet
    {
        private readonly DalamudPluginInterface               _pi;
        private readonly RezPlsConfig                         _config;
        private readonly SortedList<ushort, (Status, string)> _enabledStatusSet;
        private readonly SortedList<ushort, (Status, string)> _disabledStatusSet;

        public IList<(Status, string)> EnabledStatusSet
            => _enabledStatusSet.Values;

        public IList<(Status, string)> DisabledStatusSet
            => _disabledStatusSet.Values;

        public bool IsEnabled(ushort statusId)
            => _enabledStatusSet.ContainsKey(statusId);

        public StatusSet(DalamudPluginInterface pi, RezPlsConfig config)
        {
            _pi     = pi;
            _config = config;
            var sheet = pi.Data.GetExcelSheet<Status>();
            _enabledStatusSet = new SortedList<ushort, (Status, string)>(sheet.Where(s => s.CanDispel && s.Name.RawData.Length > 0)
                .ToDictionary(s => (ushort) s.RowId, s => (s, s.Name.ToString().ToLowerInvariant())));
            _disabledStatusSet = new SortedList<ushort, (Status, string)>(_enabledStatusSet.Count);

            var bad = false;
            foreach (var statusId in _config.UnmonitoredStatuses)
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
                _config.UnmonitoredStatuses = _disabledStatusSet.Select(kvp => kvp.Key).ToHashSet();
                _pi.SavePluginConfig(_config);
            }
        }

        public void Swap(ushort statusId)
        {
            if (_enabledStatusSet.TryGetValue(statusId, out var status))
            {
                for (var i = 0; i < _enabledStatusSet.Count; ++i)
                {
                    var element = _enabledStatusSet.ElementAt(i);
                    if (element.Value.Item2 == status.Item2)
                    {
                        _disabledStatusSet.Add(element.Key, element.Value);
                        _enabledStatusSet.Remove(element.Key);
                        _config.UnmonitoredStatuses.Add(element.Key);
                        --i;
                    }
                }

                _pi.SavePluginConfig(_config);
            }
            else if (_disabledStatusSet.TryGetValue(statusId, out status))
            {
                for (var i = 0; i < _disabledStatusSet.Count; ++i)
                {
                    var element = _disabledStatusSet.ElementAt(i);
                    if (element.Value.Item2 == status.Item2)
                    {
                        _enabledStatusSet.Add(element.Key, element.Value);
                        _disabledStatusSet.Remove(element.Key);
                        _config.UnmonitoredStatuses.Remove(element.Key);
                        --i;
                    }
                }

                _pi.SavePluginConfig(_config);
            }
            else
            {
                PluginLog.Warning($"Trying to swap Status {statusId}, but it is not a valid status.");
            }
        }

        public void ClearEnabledList()
        {
            var previousCount = _config.UnmonitoredStatuses.Count;
            foreach (var s in _enabledStatusSet)
            {
                _disabledStatusSet.Add(s.Key, s.Value);
                _config.UnmonitoredStatuses.Add(s.Key);
            }

            _enabledStatusSet.Clear();
            if (previousCount != _config.UnmonitoredStatuses.Count)
                _pi.SavePluginConfig(_config);
        }

        public void ClearDisabledList()
        {
            var previousCount = _config.UnmonitoredStatuses.Count;
            foreach (var s in _disabledStatusSet)
                _enabledStatusSet.Add(s.Key, s.Value);
            _disabledStatusSet.Clear();
            _config.UnmonitoredStatuses.Clear();
            if (previousCount != 0)
                _pi.SavePluginConfig(_config);
        }
    }
}
