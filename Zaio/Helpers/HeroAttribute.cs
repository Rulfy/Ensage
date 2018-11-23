using System;
using Ensage;
using Attribute = System.Attribute;

namespace Zaio.Helpers
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class HeroAttribute : Attribute
    {
        public HeroAttribute(HeroId id)
        {
            Id = id;
        }

        public HeroId Id { get; }
    }
}