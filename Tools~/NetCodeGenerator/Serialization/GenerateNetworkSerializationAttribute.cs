using System;
using System.Collections.Generic;
using System.Text;

namespace NetCodeGenerator.Serialization
{
    [AttributeUsage(AttributeTargets.Struct)]
    public class GenerateNetworkSerializationAttribute : Attribute
    {
    }
}
