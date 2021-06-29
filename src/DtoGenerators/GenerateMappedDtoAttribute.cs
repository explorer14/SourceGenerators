using System;

namespace DtoGenerators
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public class GenerateMappedDtoAttribute : Attribute
    {
        public GenerateMappedDtoAttribute()
        {
        }
    }
}