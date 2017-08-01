// <copyright file="Venomancer.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace Vaper.Heroes
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.SDK.Abilities.Items;
    using Ensage.SDK.Abilities.npc_dota_hero_venomancer;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Handlers;
    using Ensage.SDK.Helpers;
    using Ensage.SDK.Inventory.Metadata;

    using log4net;

    using PlaySharp.Toolkit.Helper.Annotations;
    using PlaySharp.Toolkit.Logging;

    using Vaper.OrbwalkingModes.Combo;

    public class WardCommand
    {
        public WardCommand(Unit ward, Entity targetEntity, float startTime)
        {
            this.Ward = ward;
            this.TargetEntity = targetEntity;
            this.StartTime = startTime;

            this.EndTime = startTime + (ward.AttackPoint() * 1.5f) + (Game.Ping / 1000.0f); // TODO: check where unit is currently in animation
        }

        public float EndTime { get; }

        public float StartTime { get; }

        public Entity TargetEntity { get; }

        public Unit Ward { get; }

        public override bool Equals(object obj)
        {
            var unit = obj as Unit;
            return (unit != null) && (this.Ward == unit);
        }

        public override int GetHashCode()
        {
            return (int)this.Ward.Handle;
        }
    }

    [PublicAPI]
    [ExportHero(HeroId.npc_dota_hero_venomancer)]
    public class Venomancer : BaseHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private HashSet<WardCommand> wardCommandList = new HashSet<WardCommand>();

        [ItemBinding]
        public item_blink Blink { get; private set; }

        [ItemBinding]
        public item_cyclone Euls { get; private set; }

        [ItemBinding]
        public item_force_staff ForceStaff { get; private set; }

        public venomancer_venomous_gale Gale { get; private set; }

        [ItemBinding]
        public item_glimmer_cape GlimmerCape { get; private set; }

        public venomancer_poison_nova Nova { get; private set; }

        public venomancer_poison_sting Sting { get; private set; }

        public TaskHandler UpdateHandler { get; private set; }

        [ItemBinding]
        public item_veil_of_discord Veil { get; private set; }

        public venomancer_plague_ward Ward { get; private set; }

        protected override ComboOrbwalkingMode GetComboOrbwalkingMode()
        {
            return new VenoComboOrbwalker(this);
        }

        protected override void OnActivate()
        {
            base.OnActivate();

            this.Gale = this.Context.AbilityFactory.GetAbility<venomancer_venomous_gale>();
            this.Sting = this.Context.AbilityFactory.GetAbility<venomancer_poison_sting>();
            this.Ward = this.Context.AbilityFactory.GetAbility<venomancer_plague_ward>();
            this.Nova = this.Context.AbilityFactory.GetAbility<venomancer_poison_nova>();

            this.wardCommandList.Clear();
            this.UpdateHandler = UpdateManager.Run(this.OnUpdate);
        }

        protected override void OnDeactivate()
        {
            this.UpdateHandler.Cancel();
            this.wardCommandList.Clear();

            base.OnDeactivate();
        }

        private async Task OnUpdate(CancellationToken token)
        {
            var time = Game.GameTime;

            // update busy list
            this.wardCommandList = this.wardCommandList.Where(x => x.Ward.IsValid && x.Ward.IsAlive && (x.EndTime > time)).ToHashSet();

            var wards = EntityManager<Unit>.Entities.Where(
                                               x => x.IsAlive
                                                    && (x.ClassId == ClassId.CDOTA_BaseNPC_Venomancer_PlagueWard)
                                                    && x.IsControllable
                                                    && this.wardCommandList.All(y => y.Ward != x))
                                           .ToList();
            if (wards.Any())
            {
                var attackRange = (float)wards.First().AttackRange;

                var allies = EntityManager<Hero>.Entities.Where(x => x.IsVisible && x.IsAlive && (x.Team == this.Owner.Team) && !x.IsIllusion).ToList();
                var enemies = EntityManager<Hero>.Entities.Where(x => x.IsVisible && x.IsAlive && this.Owner.IsEnemy(x) && !x.IsIllusion).ToList();

                // check for destroyable items on ground
                var items = EntityManager<PhysicalItem>.Entities.Where(x => x.IsVisible && x.IsAlive && x.Item.IsKillable && this.wardCommandList.All(y => y.TargetEntity != x))
                                                       .ToList();
                if (items.Any())
                {
                    // try to destroy enemy physical items
                    foreach (var physicalItem in items.Where(x => (x.Item.Owner != null) && this.Owner.IsEnemy(x.Item.Owner) && (x.Item.Id != AbilityId.item_aegis) && (x.Item.Id != AbilityId.item_cheese)))
                    {
                        if (!physicalItem.IsValid)
                        {
                            continue;
                        }

                        var ward = wards.FirstOrDefault(x => x.Distance2D(physicalItem.NetworkPosition) <= attackRange);
                        if (ward != null)
                        {
                            wards.Remove(ward);
                            ward.Attack(physicalItem);
                            this.wardCommandList.Add(new WardCommand(ward, physicalItem, time));
                            await Task.Delay(50, token);
                        }
                    }

                    // try to destroy aegis and cheese if enemy too close and none of our allies is close enough to grab it
                    foreach (var physicalItem in items.Where(x => x.IsValid && (x.Item != null) && ((x.Item.Id == AbilityId.item_aegis) || (x.Item.Id == AbilityId.item_cheese))))
                    {
                        if (physicalItem.IsValid && allies.All(x => x.Distance2D(physicalItem) > 250.0f) && enemies.Any(x => !x.IsStunned() && x.Distance2D(physicalItem) <= 200.0f))
                        {
                            var ward = wards.FirstOrDefault(x => x.Distance2D(physicalItem.NetworkPosition) <= attackRange);
                            if (ward != null)
                            {
                                wards.Remove(ward);
                                ward.Attack(physicalItem);
                                this.wardCommandList.Add(new WardCommand(ward, physicalItem, time));
                                await Task.Delay(50, token);
                            }
                        }
                    }
                }

                // remove enemies which are already targeted
                enemies = enemies.Where(x => this.wardCommandList.All(y => y.TargetEntity != x)).ToList();

                // disable blink from enemies
                var blinkEnemies = enemies.Where(x => (x.GetItemById(AbilityId.item_blink) != null) && !x.HasModifier(this.Sting.TargetModifierName)).ToList();
                foreach (var blinkEnemy in blinkEnemies)
                {
                    if (!blinkEnemy.IsValid)
                    {
                        continue;
                    }

                    var ward = wards.FirstOrDefault(x => x.IsInAttackRange(blinkEnemy) && x.CanAttack(blinkEnemy));
                    if (ward != null)
                    {
                        wards.Remove(ward);
                        enemies.Remove(blinkEnemy);

                        ward.Attack(blinkEnemy);
                        this.wardCommandList.Add(new WardCommand(ward, blinkEnemy, time));
                        await Task.Delay(50, token);
                    }
                }

                if (this.Sting.Enabled)
                {
                    // apply poisen buff to as many enemy heroes as possible  
                    var tmp = enemies.Where(x => !x.HasModifier(this.Sting.TargetModifierName)).ToList();
                    foreach (var enemy in tmp)
                    {
                        if (!enemy.IsValid)
                        {
                            continue;
                        }

                        var ward = wards.FirstOrDefault(x => x.IsInAttackRange(enemy) && x.CanAttack(enemy));
                        if (ward != null)
                        {
                            wards.Remove(ward);
                            enemies.Remove(enemy);

                            ward.Attack(enemy);
                            this.wardCommandList.Add(new WardCommand(ward, enemy, time));
                            await Task.Delay(50, token);
                        }
                    }
                }

                // todo: lasthit

                // deny own wards
                foreach (var ward in wards.Where(x => x.IsValid && (x.HealthPercent() <= 0.5f) && enemies.Any(y => y.IsInAttackRange(x, 200.0f)) && this.wardCommandList.All(y => y.TargetEntity != x)).OrderBy(x => x.Health).ToList())
                {
                    // find enough wards to deny
                    List<Unit> attacker = new List<Unit>();
                    foreach (var attackUnit in wards.Where(x => (x != ward) && x.IsInAttackRange(ward)))
                    {
                        attacker.Add(attackUnit);
                        if (attacker.Sum(x => x.DamageAverage) >= ward.Health)
                        {
                            break;
                        }
                    }

                   // Log.Debug($"{attacker.Sum(x => x.DamageAverage)} >= {ward.Health} with cout {attacker.Count}");
                    // issue attack command if we have enough damage
                    if (attacker.Sum(x => x.DamageAverage) >= ward.Health)
                    {
                        foreach (var w in attacker)
                        {
                            wards.Remove(w);
                            this.wardCommandList.Add(new WardCommand(w, ward, time));
                        }
                        Player.EntitiesAttack(attacker, ward);
                        await Task.Delay(50, token);
                    }
                }

                // attack lowest hp enemy
                foreach (var ward in wards.Where(x => x.IsValid))
                {
                    var lowestEnemy = enemies.Where(x => x.IsValid && ward.IsInAttackRange(x)).OrderBy(x => x.Health).FirstOrDefault();
                    if (lowestEnemy != null)
                    {
                        ward.Attack(lowestEnemy);
                        this.wardCommandList.Add(new WardCommand(ward, lowestEnemy, time));
                        await Task.Delay(50, token);
                    }
                }
            }

            await Task.Delay(125, token);
        }
    }
}