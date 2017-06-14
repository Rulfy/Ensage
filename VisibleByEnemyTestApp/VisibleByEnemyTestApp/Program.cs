using System;

namespace VisibleByEnemyTestApp
{
    using Ensage;

    class Program
    {
        static void Main(string[] args)
        {
            Entity.OnInt32PropertyChange += Entity_OnInt32PropertyChange;
            Console.WriteLine("VisibleByEnemyTestApp loaded");
        }

        private static void Entity_OnInt32PropertyChange(Entity sender, Int32PropertyChangeEventArgs args)
        {
            var hero = sender as Hero;
            if (args.PropertyName == "m_iTaggedAsVisibleByTeam" && hero != null)
            {
                var me = ObjectManager.LocalHero;

                var output = $"{sender.Name}: {args.OldValue} => {args.NewValue} | IsVisibleToEnemies: {!hero.IsVisibleToEnemies} | OwnTeam: {hero.Team == me?.Team} | Me: {hero == me}";
                Console.WriteLine(output);
            }
        }
    }
}
