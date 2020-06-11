﻿using System.Collections.Generic;

namespace Microsoft.Xna.Framework.Graphics
{
    public class EffectParameterCollection : IEnumerable<EffectParameter>
    {
        internal static readonly EffectParameterCollection Empty = new EffectParameterCollection(new EffectParameter[0]);

        private readonly EffectParameter[] _parameters;
        private readonly Dictionary<string, EffectParameter> _byName;

        internal EffectParameterCollection(EffectParameter[] parameters)
        {
            _parameters = parameters;
            _byName = new Dictionary<string, EffectParameter>(_parameters.Length, System.StringComparer.Ordinal);
            foreach (var p in parameters)
                _byName[p.Name] = p;
        }

        internal EffectParameterCollection Clone()
        {
            if (_parameters.Length == 0)
                return Empty;

            var parameters = new EffectParameter[_parameters.Length];
            for (var i = 0; i < _parameters.Length; i++)
                parameters[i] = new EffectParameter(_parameters[i]);

            return new EffectParameterCollection(parameters);
        }

        public int Count
        {
            get { return _parameters.Length; }
        }
		
		public EffectParameter this[int index]
		{
			get { return _parameters[index]; }
		}
		
		public EffectParameter this[string name]
        {
            get 
            {
                EffectParameter p;
                if (!_byName.TryGetValue(name, out p))
                    return null;
                return p;
			}
        }

        public IEnumerator<EffectParameter> GetEnumerator()
        {
            return ((IEnumerable<EffectParameter>)_parameters).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _parameters.GetEnumerator();
        }
    }
}
