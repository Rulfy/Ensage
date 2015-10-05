using System;
using System.Collections.Generic;
using System.Linq;
using Ensage;

namespace VisibleByEnemy
{
    class Program
    {
        private static readonly Dictionary<Unit, ParticleEffect> Effects = new Dictionary<Unit, ParticleEffect>();
        public static void Main(string[] args)
        {
            Game.OnUpdate += Game_OnUpdate;
        }

        private static void Game_OnUpdate(System.EventArgs args)
        {
            var player = ObjectMgr.LocalPlayer;
            if (player == null || player.Team == Team.Observer)
                return;
            // check allied heroes
            var heroes = ObjectMgr.GetEntities<Hero>().Where(x => x.Team == player.Team);
            foreach (var hero in heroes)
            {
                HandleEffect(hero);
            }
            // check wards
            var wards =
                ObjectMgr.GetEntities<Unit>()
                    .Where(
                        x =>
                            x.Team == player.Team &&
                             (x.ClassID == ClassID.CDOTA_NPC_Observer_Ward ||
                             x.ClassID == ClassID.CDOTA_NPC_Observer_Ward_TrueSight));
            foreach (var ward in wards)
            {
                HandleEffect(ward);
            }
        }

        static void HandleEffect(Unit unit)
        {
            if (unit.IsVisibleToEnemies && unit.IsAlive)
            {
                ParticleEffect effect;
                if (!Effects.TryGetValue(unit, out effect))
                {
                    effect = unit.AddParticleEffect("particles/items_fx/aura_shivas.vpcf");
                    Effects.Add(unit, effect);
                }
            }
            else
            {
                ParticleEffect effect;
                if (Effects.TryGetValue(unit, out effect))
                {
                    effect.Dispose();
                    Effects.Remove(unit);
                }
            }
        }
    }
}
