#region License
// /*
// Microsoft Public License (Ms-PL)
// MonoGame - Copyright © 2009 The MonoGame Team
// 
// All rights reserved.
// 
// This license governs use of the accompanying software. If you use the software, you accept this license. If you do not
// accept the license, do not use the software.
// 
// 1. Definitions
// The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same meaning here as under 
// U.S. copyright law.
// 
// A "contribution" is the original software, or any additions or changes to the software.
// A "contributor" is any person that distributes its contribution under this license.
// "Licensed patents" are a contributor's patent claims that read directly on its contribution.
// 
// 2. Grant of Rights
// (A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, 
// each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution, prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.
// (B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, 
// each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to make, have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or derivative works of the contribution in the software.
// 
// 3. Conditions and Limitations
// (A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.
// (B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software, 
// your patent license from such contributor to the software ends automatically.
// (C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and attribution 
// notices that are present in the software.
// (D) If you distribute any portion of the software in source code form, you may do so only under this license by including 
// a complete copy of this license with your distribution. If you distribute any portion of the software in compiled or object 
// code form, you may only do so under a license that complies with this license.
// (E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express warranties, guarantees
// or conditions. You may have additional consumer rights under your local laws which this license cannot change. To the extent
// permitted under your local laws, the contributors exclude the implied warranties of merchantability, fitness for a particular
// purpose and non-infringement.
// */
#endregion License

#region Using clause
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework.GamerServices;
#if SWITCH
#else
using Sce.PlayStation4.Network.ToolkitNp;
#endif

#endregion Using clause

namespace Microsoft.Xna.Framework.Net
{
    /// <summary>
    /// NetworkSessionProperies can contain up to eight interger values.
    /// </summary>
	public class NetworkSessionProperties
    {
        internal const int MaxProperties = 8;

        private readonly int?[] _array;
        private bool _dirty;

        public NetworkSessionProperties()
		{
            _array = new int?[MaxProperties];
		}

        private static string[] _attributeNames;
        public static void Config(string[] attributeNames)
        {
            _attributeNames = attributeNames;
        }

        internal bool Dirty
        {
            get
            {
                return _dirty;
            }
        }

        internal void MarkClean()
        {
            _dirty = false;
        }

        public int? this[int index]
        {
            get
            {
                return _array[index];
            }
            set
            {
                if (_array[index] != value)
                {
                    _array[index] = value;
                    _dirty = true;
                }
            }
        }

#if SWITCH

        internal static NetworkSessionProperties FromApplicationDataString(string applicationData)
        {
            Console.WriteLine("NetworkSessionProperties.FromApplicationDataString(); '{0}'", applicationData);

            var properties = new NetworkSessionProperties();

            var lines = applicationData.Split('\n');
            foreach (var line in lines)
            {
                var words = line.Split('=');
                if (words.Length < 1)
                    throw new Exception("Error parsing ApplicationData");

                var key = words[0];
                if (string.IsNullOrEmpty(key))
                    continue;

                int index = Array.IndexOf<string>(_attributeNames, key, 0, _attributeNames.Length);
                if (index == -1)
                    continue;
                    //throw new Exception("Error parsing ApplicationData");

                if (words.Length < 2 || string.IsNullOrEmpty(words[1]) || string.IsNullOrWhiteSpace(words[1]))
                {
                    properties._array[index] = null;
                }
                else
                {
                    var val = words[1];
                    int intval = int.Parse(val);

                    // dont use public indexer, we aren't dirty
                    properties._array[index] = intval;
                }
            }

            return properties;
        }

        internal string ToApplicationDataString()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < MaxProperties; i++)
            {
                var key = _attributeNames[i];
                var val = _array[i];

                sb.Append(key);
                sb.Append('=');
                sb.Append(val);
                sb.Append('\n');
            }

            return sb.ToString();
        }

#else
        public static AttributeConfig AttributeConfig
        {
            get
            {
                var config = new AttributeConfig();

                // Needed for internal use, is not searchable.
                config.AddInternalInt("dummy");
                
                for (var i = 0; i < MaxProperties; i++)
                {
                    config.AddSearchableInt(AttributeNames[i]);
                }

                return config;
            }
        }

        internal void Set(CreateSessionRequest req)
        {
            for (var i = 0; i < MaxProperties; i++)
            {
                if (this[i] != null)
                    req.SetAttribute(AttributeNames[i], (uint)(this[i].Value));
            }
        }

        internal void Set(SearchSessionsRequest req)
        {
            for (var i = 0; i < MaxProperties; i++)
            {
                if (this[i] != null)
                    req.SetAttribute(AttributeNames[i], (uint)this[i].Value, SearchOperator.Equal);
            }
        }

        internal static NetworkSessionProperties Get(SessionInformation sessionInfo)
        {
            var properties = new NetworkSessionProperties();
            for (var i = 0; i < MaxProperties; i++)
            {
                bool exists;
                var val = sessionInfo.GetIntAttribute(AttributeNames[i], out exists);
                if (!exists)
                {
                    Console.WriteLine("Attributes[ {0} ] = null", AttributeNames[i]);
                }
                else
                {
                    Console.WriteLine("Attributes[ {0} ] = {1}", AttributeNames[i], val);

                    properties[i] = (int)val;
                }                
            }

            return properties;
        }
#endif

        public bool Equals(NetworkSessionProperties other)
        {
            for (var i = 0; i < MaxProperties; i++)
            {
                var a = this[i];
                var b = other[i];
                if (!a.Equals(b))
                    return false;                
            }

            return true;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            for (var i = 0; i < MaxProperties; i++)
            {
                var item = this[i];
                sb.AppendFormat("Property[{0}]; {1} : {2}\n", i, _attributeNames[i], (item.HasValue ? item.Value.ToString() : "[null]"));
            }

            return sb.ToString();
        }
    }
}
