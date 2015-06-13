using System.Collections.Generic;
using System.Linq;
using Ensage;

namespace VisibleByEnemy
{
    class Program
    {
        private static readonly Dictionary<Hero, ParticleEffect> Effects = new Dictionary<Hero, ParticleEffect>();
        public static void Main(string[] args)
        {
            Entity.OnIntegerPropertyChange += Entity_OnIntegerPropertyChange;

            // Initial Check
            var player = EntityList.Player;
            if (player == null)
                return;

            var heroes = EntityList.GetEntities<Hero>().Where(x => x.IsAlive && x.Team == player.Team);
            foreach (var hero in heroes)
            {
                HandleEffect(hero);
            }
        }

        static void Entity_OnIntegerPropertyChange(Entity sender, EntityIntegerPropertyChangeEventArgs args)
        {
            if (sender == null)
                return;

            var player = EntityList.Player;
            if (player == null)
                return;

            var hero = sender as Hero;
            if (hero != null && hero.Team == player.Team && args.Property == "m_iTaggedAsVisibleByTeam")
                HandleEffect(hero);
        }

        static void HandleEffect(Hero hero)
        {
            if (hero.IsVisibleToEnemies)
            {
                ParticleEffect effect;
                if (!Effects.TryGetValue(hero, out effect))
                {
                    effect = hero.AddParticleEffect("aura_shivas");
                    Effects.Add(hero, effect);
                }
            }
            else
            {
                ParticleEffect effect;
                if (Effects.TryGetValue(hero, out effect))
                {
                    effect.Dispose();
                    Effects.Remove(hero);
                }
            }
        }
    }
}
