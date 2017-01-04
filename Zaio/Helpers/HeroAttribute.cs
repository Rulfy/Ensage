using System;
using Ensage;
using Attribute = System.Attribute;

namespace Zaio.Helpers
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class HeroAttribute : Attribute
    {
        public HeroAttribute(ClassID id)
        {
            this.Id = id;
        }

        public ClassID Id { get; }
    }
}
