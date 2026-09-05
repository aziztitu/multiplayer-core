using System;

namespace NetCodeGenerator.Serialization
{
    [AttributeUsage(AttributeTargets.Struct)]
    public class GenerateNetworkSerializationAttribute : Attribute
    {
    }
}
