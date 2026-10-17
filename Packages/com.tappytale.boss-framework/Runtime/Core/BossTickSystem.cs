using System;
using System.Collections.Generic;

namespace TappyTale.BossFight.Core
{
    public sealed class BossTickSystem
    {
        private readonly List<IBossTickable> _tickables = new();
        private bool _isDirty;

        public void Register(IBossTickable tickable)
        {
            if (tickable == null)
            {
                throw new ArgumentNullException(nameof(tickable));
            }

            if (_tickables.Contains(tickable))
            {
                return;
            }

            _tickables.Add(tickable);
            _isDirty = true;
        }

        public void Unregister(IBossTickable tickable)
        {
            if (tickable == null)
            {
                return;
            }

            _tickables.Remove(tickable);
        }

        public void Tick(float deltaTime)
        {
            if (_isDirty)
            {
                _tickables.Sort(static (left, right) => left.TickOrder.CompareTo(right.TickOrder));
                _isDirty = false;
            }

            for (int index = 0; index < _tickables.Count; index++)
            {
                _tickables[index].Tick(deltaTime);
            }
        }

        public void Clear()
        {
            _tickables.Clear();
            _isDirty = false;
        }
    }
}
