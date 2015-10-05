using System;
using System.Collections.Generic;
using System.Linq;
using Ensage;
using SharpDX;

namespace TowerRange
{
    internal class Program
    {
        // TODO: config stuff
        private const bool OwnTowers = true;
        private const bool EnemyTowers = true;
        // ReSharper disable once CollectionNeverQueried.Local
        private static readonly List<ParticleEffect> Effects = new List<ParticleEffect>(); // keep references

        private static void Main()
        {
            HandleTowers();
            Game.OnFireEvent += Game_OnFireEvent;
        }

        private static void Game_OnFireEvent(FireEventEventArgs args)
        {
            if (args.GameEvent.Name == "dota_game_state_change")
            {
                var state = (GameState) args.GameEvent.GetInt("new_state");
                if (state == GameState.Started || state == GameState.Prestart )
                    HandleTowers();
            }
        }

        private static void HandleTowers()
        {
            if (!Game.IsInGame)
                return;

            foreach (var e in Effects)
            {
                e.Dispose();
            }
            Effects.Clear();

            var player = ObjectMgr.LocalPlayer;
            if (player == null)
                return;
            var towers =
                ObjectMgr.GetEntities<Building>()
                    .Where(x => x.IsAlive && x.ClassID == ClassID.CDOTA_BaseNPC_Tower)
                    .ToList();
            if (!towers.Any())
                return;
            
            if (player.Team == Team.Observer)
            {
                foreach (var effect in towers.Select(tower => tower.AddParticleEffect(@"particles\ui_mouseactions\range_display.vpcf")))
                {
                    effect.SetControlPoint(1, new Vector3(850, 0, 0));
                    Effects.Add(effect);
                }
            }
            else
            {
                if (EnemyTowers)
                {
                    foreach (var effect in towers.Where(x => x.Team != player.Team).Select(tower => tower.AddParticleEffect(@"particles\ui_mouseactions\range_display.vpcf")))
                    {
                        effect.SetControlPoint(1, new Vector3(850, 0, 0));
                        Effects.Add(effect);
                    }
                }
                if (OwnTowers)
                {
                    foreach (var effect in towers.Where(x => x.Team == player.Team).Select(tower => tower.AddParticleEffect(@"particles\ui_mouseactions\range_display.vpcf")))
                    {
                        effect.SetControlPoint(1, new Vector3(850, 0, 0));
                        Effects.Add(effect);
                    }
                }
            }
        }
    }
}
