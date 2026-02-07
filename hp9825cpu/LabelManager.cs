using System;
using System.Collections.Generic;
using System.Linq;

namespace HP9825CPU
{


    public class LabelManager
    {
        public LabelManager()
        {
        }

        private Dictionary<string, int> _List = new Dictionary<string, int>();
        private List<(string Label, Action<int> Recevier)> _Relocate = new List<(string Label, Action<int> Recevier)>();
        private List<Action> _Dependencies = new List<Action>();

        public void RegisterRelocationDependency(Action callback)
        {
            _Dependencies.Add(callback);
        }

        public bool SetKnownLocation(string label, int location)
        {
            if (_List.ContainsKey(label))
                return false;
            _List.Add(label, location);
            return true;
        }

        public void SetLabelUnknown(string label)
        {
            _List.Remove(label);
        }

        public int? GetLocation(string label)
        {
            int loc;
            if (!_List.TryGetValue(label, out loc))
                return null;
            return loc;
        }

        public void RegisterRelocation(string labelInQuestion, Action<int> receiver)
        {
            _Relocate.Add((labelInQuestion, receiver));
        }

        public void Relocate()
        {
            while (_Relocate.Count > 0)
            {
                DependencyCallback();
                int countNow = _Relocate.Count;
                int i = 0;
                while (i < _Relocate.Count)
                {
                    var x = _Relocate[i];
                    if (_List.TryGetValue(x.Label, out var loc))
                    {
                        x.Recevier(loc);
                        _Relocate.RemoveAt(i);
                    }
                    else
                        i++;
                }
                if (_Relocate.Count == countNow)
                {
                    throw new GenericException((int)AssemblerErrorCodes.RelocationRecursion, "Couldn't resolve some relocation. Posisble recursion? " + string.Join(',', _Relocate.Select(x => x.Label)));
                }
            }
            DependencyCallback();   // make sure we run at least once...
        }

        private void DependencyCallback()
        {
            foreach (var x in _Dependencies)
                x();
        }


        public IEnumerable<string> MissingLabels()
        {
            return _Relocate.Select(x => x.Label).Distinct();
        }

        private int? FirstOrg = null;
        private int? SecondaryOrg = null;
        public void SetOrg(int location)
        {
            if (!FirstOrg.HasValue)
            {
                FirstOrg = location;
            }
            SecondaryOrg = location;
        }
        public bool MovePLC(int increment)
        {
            if (IsEnded && increment != 0)
                return false;
            if (SecondaryOrg.HasValue)
            {
                SecondaryOrg = SecondaryOrg.Value + increment;
            }
            else
            {
                if (FirstOrg.HasValue)
                {
                    FirstOrg = FirstOrg.Value + increment;
                }
            }
            return true;
        }

        public int? GetPLC()
        {
            return SecondaryOrg ?? FirstOrg;
        }

        public void ResetOrg()
        {
            if (SecondaryOrg.HasValue)
            {
                SecondaryOrg = null;
            }
        }

        private bool IsEnded = false;

        public void DemandEnded(SourceLineRef from)
        {
            if (!IsEnded)
            {
                throw from.Error(AssemblerErrorCodes.MissingEnd, "The program didn't 'END'...");
            }
        }

        public void SetEnded()
        {
            IsEnded = true;
        }

        public void CreateCrossReference(ListingPrinter printer, bool sortAlphabetically = false)
        {
            if (_Relocate.Count >0)
            {
                foreach(var x in _Relocate.OrderBy(x=>x.Label))
                {
                    printer.PrintCorssReferenceLine(null, x.Label);
                }
            }
            if (_List.Count>0)
            {
                if (sortAlphabetically)
                {
                    foreach(var x in _List.OrderBy(x=>x.Key))
                    {
                        printer.PrintCorssReferenceLine(x.Value, x.Key);
                    }
                }
                else
                {
                    foreach(var x in _List.OrderBy(x=>x.Value))
                    {
                        printer.PrintCorssReferenceLine(x.Value, x.Key);
                    }
                }
            }
        }
    }

}