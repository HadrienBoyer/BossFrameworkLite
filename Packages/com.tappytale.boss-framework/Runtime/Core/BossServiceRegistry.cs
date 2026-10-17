using System;
using System.Collections.Generic;

namespace TappyTale.BossFight.Core
{
    public sealed class BossServiceRegistry
    {
        private readonly Dictionary<Type, object> _services = new();

        public void Register<TService>(TService service) where TService : class
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            Type serviceType = typeof(TService);
            if (_services.ContainsKey(serviceType))
            {
                throw new InvalidOperationException($"A service of type {serviceType.FullName} is already registered.");
            }

            _services.Add(serviceType, service);
        }

        public bool TryResolve<TService>(out TService service) where TService : class
        {
            if (_services.TryGetValue(typeof(TService), out object registeredService))
            {
                service = registeredService as TService;
                return service != null;
            }

            service = null;
            return false;
        }

        public TService Resolve<TService>() where TService : class
        {
            if (TryResolve(out TService service))
            {
                return service;
            }

            throw new KeyNotFoundException($"No service of type {typeof(TService).FullName} is registered.");
        }

        public void Clear() => _services.Clear();
    }
}
