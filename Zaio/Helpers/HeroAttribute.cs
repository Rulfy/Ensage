using System;
using Ensage;
using Attribute = System.Attribute;

namespace Zaio.Helpers
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class HeroAttribute : Attribute
    {
        public HeroAttribute(ClassID id)
        {
            Id = id;
        }

        public ClassID Id { get; }
    }
}