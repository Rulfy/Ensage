// <copyright file="Program.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace VisibleByEnemyTestApp
{
    using System;

    using Ensage;

    public class Program
    {
        private static bool? lastState;

        public static void Main(string[] args)
        {
            Game.OnIngameUpdate += Game_OnIngameUpdate;
            Entity.OnInt32PropertyChange += Entity_OnInt32PropertyChange;
            Console.WriteLine("VisibleByEnemyTestApp loaded");
        }

        private static void Entity_OnInt32PropertyChange(Entity sender, Int32PropertyChangeEventArgs args)
        {
            var hero = sender as Hero;
            if (args.PropertyName == "m_iTaggedAsVisibleByTeam" && hero != null)
            {
                var me = ObjectManager.LocalHero;

                var output =
                    $"{sender.Name}: {args.OldValue} => {args.NewValue} | IsVisibleToEnemies: {!hero.IsVisibleToEnemies} | OwnTeam: {hero.Team == me?.Team} | Me: {hero == me}";
                Console.WriteLine(output);
            }
        }

        private static void Game_OnIngameUpdate(EventArgs args)
        {
            var state = ObjectManager.LocalHero?.IsVisibleToEnemies;
            if (state != lastState)
            {
                Game.PrintMessage($"IsVisibleToEnemies: {state}");
                lastState = state;
            }
        }
    }
}