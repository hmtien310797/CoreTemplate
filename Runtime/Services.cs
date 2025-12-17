using System;
using System.Collections.Generic;

namespace BaseCore
{
    public static class Services
    {
        private static readonly Dictionary<Type, object> _map = new();

        public static void Register<T>(T service) where T : class
            => _map[typeof(T)] = service;

        public static T Get<T>() where T : class
            => (T)_map[typeof(T)];
    }
}