using System;
using Ensage;
using SharpDX;

namespace VisibleByEnemy
{
    class Program
    {
        private static ParticleEffect _effect;
        public static void Main(string[] args)
        {
            Entity.OnIntegerPropertyChange += Entity_OnIntegerPropertyChange;
        }

        static void Entity_OnIntegerPropertyChange(Entity sender, EntityIntegerPropertyChangeEventArgs args)
        {
            var me = EntityList.Hero;
            if (me != null && sender.Equals(me) && args.Property == "m_iTaggedAsVisibleByTeam")
            {
                if (me.IsVisibleToEnemies)
                {
                    if (_effect == null)
                    {
                        _effect = me.AddParticleEffect("aura_shivas");
                        _effect.SetVector(0, new Vector3(0, 0, 0));
                    }
                }
                else
                {
                    if (_effect != null)
                    {
                        _effect.Dispose();
                        _effect = null;
                    }
                }
            }
        }
    }
}
