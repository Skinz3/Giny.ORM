using Giny.Core;
using Giny.Core.DesignPattern;
using Giny.ORM.Interfaces;
using Giny.ORM.IO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Giny.ORM.Cyclic
{
    public class CyclicSaveTask : Singleton<CyclicSaveTask>
    {
        private ConcurrentDictionary<Type, SynchronizedCollection<ITable>> _newElements = new ConcurrentDictionary<Type, SynchronizedCollection<ITable>>();
        private ConcurrentDictionary<Type, SynchronizedCollection<ITable>> _updateElements = new ConcurrentDictionary<Type, SynchronizedCollection<ITable>>();
        private ConcurrentDictionary<Type, SynchronizedCollection<ITable>> _removeElements = new ConcurrentDictionary<Type, SynchronizedCollection<ITable>>();

        public void AddElement(ITable element)
        {
            var type = element.GetType();

            if (_newElements.ContainsKey(type))
            {
                if (!_newElements[type].Contains(element))
                    _newElements[type].Add(element);
            }
            else
            {
                _newElements.TryAdd(type, new SynchronizedCollection<ITable> { element });
            }
        }

        public void UpdateElement(ITable element)
        {
            var type = element.GetType();

            lock (_updateElements)
            {
                if (_newElements.ContainsKey(type) && _newElements[type].Contains(element))
                    return;

                if (_updateElements.ContainsKey(type))
                {
                    if (!_updateElements[type].Contains(element))
                        _updateElements[type].Add(element);
                }
                else
                {
                    _updateElements.TryAdd(type, new SynchronizedCollection<ITable> { element });
                }
            }
        }

        public void RemoveElement(ITable element)
        {
            if (element == null)
                return;

            var type = element.GetType();

            if (_newElements.ContainsKey(type) && _newElements[type].Contains(element))
            {
                _newElements[type].Remove(element);
                return;
            }

            if (_updateElements.ContainsKey(type) && _updateElements[type].Contains(element))
                _updateElements[type].Remove(element);

            if (_removeElements.ContainsKey(type))
            {
                if (!_removeElements[type].Contains(element))
                    _removeElements[type].Add(element);
            }
            else
            {
                _removeElements.TryAdd(type, new SynchronizedCollection<ITable> { element });
            }
        }


        public void Save()
        {
            var types = _removeElements.Keys.ToList();
            foreach (var type in types)
            {
                SynchronizedCollection<ITable> elements;
                elements = _removeElements[type];

                if (elements.Count > 0)
                {
                    try
                    {
                        TableManager.Instance.GetWriter(type).Use(elements.ToArray(), DatabaseAction.Remove);
                        _removeElements[type] = new SynchronizedCollection<ITable>(_removeElements[type].Skip(elements.Count));
                    }
                    catch (Exception e)
                    {
                        Logger.Write(e.Message, MessageState.ERROR);
                    }
                }
                

            }

            types = _newElements.Keys.ToList();
            foreach (var type in types)
            {
                SynchronizedCollection<ITable> elements;

                elements = _newElements[type];

                if (elements.Count > 0)
                {
                    try
                    {
                        TableManager.Instance.GetWriter(type).Use(elements.ToArray(), DatabaseAction.Add);
                        _newElements[type] = new SynchronizedCollection<ITable>(_newElements[type].Skip(elements.Count));
                    }
                    catch (Exception e)
                    {
                        Logger.Write(e.Message, MessageState.ERROR);
                    }
                }

               

            }

            types = _updateElements.Keys.ToList();
            foreach (var type in types)
            {
                SynchronizedCollection<ITable> elements;

                elements = _updateElements[type];

                if (elements.Count > 0)
                {
                    try
                    {
                        TableManager.Instance.GetWriter(type).Use(elements.ToArray(), DatabaseAction.Update);
                        _updateElements[type] = new SynchronizedCollection<ITable>(_updateElements[type].Skip(elements.Count));
                    }
                    catch (Exception e)
                    {
                        Logger.Write(e.Message, MessageState.ERROR);
                    }
                }
               
            }
        }
    }
}
