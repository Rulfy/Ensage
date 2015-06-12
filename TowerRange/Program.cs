using System;
using System.Collections.Generic;
using System.Linq;
using Ensage;
using SharpDX;

namespace TowerRange
{
    internal class Program
    {
        private const bool OwnTowers = true;
        private const bool EnemyTowers = true;
        private static readonly List<ParticleEffect> Effects = new List<ParticleEffect>(); // keep references

        private static void Main(string[] args)
        {
            Entity.OnIntegerPropertyChange += Entity_OnIntegerPropertyChange;
            CheckTowers();
        }

        private static void Entity_OnIntegerPropertyChange(Entity sender, EntityIntegerPropertyChangeEventArgs args)
        {
            if (args.Property != "m_nGameState")
                return;
            if (!Game.IsInGame)
                Effects.Clear();
            else
                CheckTowers();
        }

        private static void CheckTowers()
        {
            if (!Game.IsInGame)
                return;

            Effects.Clear();

            var player = EntityList.GetLocalPlayer();
            if (player == null)
                return;
            var towers =
                EntityList.GetEntities<Building>()
                    .Where(x => x.IsAlive && x.ClassId == ClassId.CDOTA_BaseNPC_Tower)
                    .ToList();
            if (!towers.Any())
                return;

            if (player.Team == Team.Observer)
            {
                foreach (var effect in towers.Select(tower => tower.AddParticleEffect("range_display")))
                {
                    effect.SetVector(0, new Vector3(850, 0, 0));
                    Effects.Add(effect);
                }
            }
            else
            {
                if (EnemyTowers)
                {
                    foreach (var effect in towers.Where(x => x.Team != player.Team).Select(tower => tower.AddParticleEffect("range_display")))
                    {
                        effect.SetVector(0, new Vector3(850, 0, 0));
                        Effects.Add(effect);
                    }
                }
                if (OwnTowers)
                {
                    foreach (var effect in towers.Where(x => x.Team == player.Team).Select(tower => tower.AddParticleEffect("range_display")))
                    {
                        effect.SetVector(0, new Vector3(850, 0, 0));
                        Effects.Add(effect);
                    }
                }
            }
        }
    }
}
