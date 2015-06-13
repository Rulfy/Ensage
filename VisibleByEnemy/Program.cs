using System.Collections.Generic;
using Ensage;
using SharpDX;

namespace VisibleByEnemy
{
    class Program
    {
        private static Dictionary<Entity,ParticleEffect> _effects = new Dictionary<Entity, ParticleEffect>();
        public static void Main(string[] args)
        {
            Entity.OnIntegerPropertyChange += Entity_OnIntegerPropertyChange;
        }

        static void Entity_OnIntegerPropertyChange(Entity sender, EntityIntegerPropertyChangeEventArgs args)
        {
            var player = EntityList.Player;
            if (player == null || sender == null)
                return;

            var hero = sender as Hero;
            if (hero != null && hero.Team == player.Team && args.Property == "m_iTaggedAsVisibleByTeam")
            {
                if (hero.IsVisibleToEnemies)
                {
                    ParticleEffect effect;
                    if (!_effects.TryGetValue(hero, out effect))
                    {
                        effect = hero.AddParticleEffect("aura_shivas");
                        effect.SetVector(0, new Vector3(0, 0, 0));
                    }
                }
                else
                {
                    ParticleEffect effect;
                    if (_effects.TryGetValue(hero, out effect))
                    {
                        effect.Dispose();
                        _effects.Remove(sender);
                    }
                }
            }
        }
    }
}
