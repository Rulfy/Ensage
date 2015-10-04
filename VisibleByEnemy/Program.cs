using System;
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
            Game.OnUpdate += Game_OnUpdate;
        }

        private static void Game_OnUpdate(System.EventArgs args)
        {
            var player = ObjectMgr.LocalPlayer;
            if (player == null)
                return;

            var heroes = ObjectMgr.GetEntities<Hero>().Where(x => x.Team == player.Team);
            foreach (var hero in heroes)
            {
                HandleEffect(hero);
            }
        }

        static void HandleEffect(Hero hero)
        {
            if (hero.IsVisibleToEnemies && hero.IsAlive)
            {
                ParticleEffect effect;
                if (!Effects.TryGetValue(hero, out effect))
                {
                    effect = hero.AddParticleEffect("particles/items_fx/aura_shivas.vpcf");
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
