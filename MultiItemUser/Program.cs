using System;
using System.Linq;
using Ensage;
using Ensage.Common;
using Ensage.Common.Extensions;

namespace MultiItemUser
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Player.OnExecuteOrder += Player_OnExecuteOrder;
            Events.OnLoad += Events_OnLoad;
        }

        private static void Events_OnLoad(object sender, EventArgs e)
        {
            Game.PrintMessage("MultiItemUser loaded!");
        }

        private static void Player_OnExecuteOrder(Player sender, ExecuteOrderEventArgs args)
        {
            if (args.OrderId == OrderId.Ability)
            {
                var owner = args.Ability.Owner as Unit;
                if (owner != null)
                {
                    var id = args.Ability.GetAbilityId();
                    var abilities = owner.Inventory.Items.Where(x => x.GetAbilityId() == id).ToList();
                    if (abilities.Count() > 1)
                    {
                        foreach (var ability in abilities)
                        {
                            ability.UseAbility();
                        }
                        args.Process = false;
                    }
                }
            }
            else if (args.OrderId == OrderId.AbilityTarget)
            {
                var owner = args.Ability.Owner as Unit;
                if (owner != null)
                {
                    var id = args.Ability.GetAbilityId();
                    var abilities = owner.Inventory.Items.Where(x => x.GetAbilityId() == id).ToList();
                    if (abilities.Count() > 1)
                    {
                        var target = args.Target as Unit;
                        foreach (var ability in abilities)
                        {
                            ability.UseAbility(target);
                        }
                        args.Process = false;
                    }
                }
            }
        }
    }
}