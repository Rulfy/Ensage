namespace Zaio.Heroes
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.Common.Enums;
    using Ensage.Common.Extensions;
    using Ensage.Common.Extensions.SharpDX;
    using Ensage.Common.Menu;
    using Ensage.Common.Threading;

    using log4net;

    using PlaySharp.Toolkit.Logging;

    using Zaio.Helpers;
    using Zaio.Interfaces;
    using Zaio.Prediction;

    using AbilityId = Ensage.AbilityId;
    using Prediction = Ensage.Common.Prediction;

    [Hero(HeroId.npc_dota_hero_queenofpain)]
    internal class QueenOfPain : ComboHero
    {
        private static readonly string[] KillstealAbilities =
            {
                "queenofpain_scream_of_pain",
                "queenofpain_shadow_strike",
                "queenofpain_sonic_wave"
            };

        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
            {
                "queenofpain_shadow_strike",
                "queenofpain_blink",
                "queenofpain_scream_of_pain",
                "queenofpain_sonic_wave"
            };

        private Ability _eAbility;

        private MenuItem _minimumEnemyUltCount;

        private Ability _qAbility;

        private Ability _ultAbility;

        private Ability _wAbility;

        private int EnemyCountForUlt => this._minimumEnemyUltCount.GetValue<Slider>().Value;

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            // check if we are near the enemy
            var myHeroNetworkPosition = this.MyHero.NetworkPosition;
            if (!await this.MoveOrBlinkToEnemy(target, tk, 200, 475))
            {
                if (!this.MyHero.IsSilenced() && this.MyHero.Distance2D(target) >= 700)
                {
                    var pos = (target.NetworkPosition - myHeroNetworkPosition).Normalized();
                    pos *= 475;
                    pos = target.NetworkPosition - pos;

                    if (this._wAbility.IsAbilityEnabled())
                    {
                        if (target.IsMoving)
                        {
                            Log.Debug($"Jumping the gun");
                            var moves = Prediction.InFront(target, 700);
                            this._wAbility.UseAbility(moves);
                            await Await.Delay((int)(this.MyHero.GetTurnTime(moves) * 1000) + ItemDelay, tk);
                        }
                        else
                        {
                            Log.Debug($"Jumping close but far");
                            this._wAbility.UseAbility(pos);
                            await Await.Delay((int)(this.MyHero.GetTurnTime(pos) * 1000) + ItemDelay, tk);
                        }
                    }
                }

                return;
            }

            await this.HasNoLinkens(target, tk);
            await this.UseItems(target, tk);
            await this.DisableEnemy(target, tk);

            if (!this.MyHero.IsSilenced())
            {
                if (this._eAbility.IsAbilityEnabled() && this._eAbility.CanBeCasted(target)
                    && this._eAbility.CanHit(target))
                {
                    Log.Debug($"use e");
                    this._eAbility.UseAbility();
                    await Await.Delay(this.GetAbilityDelay(this._eAbility), tk);
                }

                if (this._qAbility.IsAbilityEnabled() && this._qAbility.CanBeCasted(target)
                    && this._qAbility.CanHit(target) && !target.HasModifier("modifier_queenofpain_shadow_strike"))
                {
                    Log.Debug($"use q");
                    this._qAbility.UseAbility(target);
                    await Await.Delay(this.GetAbilityDelay(target, this._qAbility), tk);
                }

                if (this._ultAbility.IsAbilityEnabled() && this._ultAbility.CanBeCasted(target)
                    && this._ultAbility.CanHit(target))
                {
                    var enemyClose =
                        ObjectManager.GetEntitiesParallel<Hero>()
                                     .Any(
                                         x =>
                                             x.IsAlive && x != target && x.Team != this.MyHero.Team && !x.IsIllusion
                                             && this._ultAbility.CanBeCasted(x) && this._ultAbility.CanHit(x)
                                             && x.Distance2D(this.MyHero) <= 1500);
                    List<Hero> enemies = null;
                    if (enemyClose)
                    {
                        var startWidth = this._ultAbility.GetAbilityData("starting_aoe");
                        var endWidth = this._ultAbility.GetAbilityData("final_aoe");
                        var distance = this._ultAbility.GetAbilityData("distance");
                        var endPos = myHeroNetworkPosition
                                     + (target.NetworkPosition - myHeroNetworkPosition).Normalized() * distance;
                        var polygon = new Geometry.Polygon.Trapezoid(
                            myHeroNetworkPosition,
                            endPos,
                            startWidth,
                            endWidth);

                        enemies =
                            ObjectManager.GetEntitiesParallel<Hero>()
                                         .Where(
                                             x =>
                                                 x.IsAlive && x != target && x.Team != this.MyHero.Team
                                                 && !x.IsIllusion && this._ultAbility.CanBeCasted(x)
                                                 && this._ultAbility.CanHit(x) && polygon.IsInside(x.NetworkPosition)
                                                 && !x.CantBeKilled())
                                         .ToList();
                    }

                    if (!enemyClose || enemies.Count() >= this.EnemyCountForUlt)
                    {
                        Log.Debug(
                            $"use ult since no enemy {!enemyClose} or {enemies?.Count()} >= {this.EnemyCountForUlt}");
                        this._ultAbility.UseAbility(target.NetworkPosition);
                        await Await.Delay(this.GetAbilityDelay(target.NetworkPosition, this._ultAbility), tk);
                    }
                }
            }

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                this.Orbwalk();
            }
        }

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("QueenOfPain", "zaioQueenOfPain", false, "npc_dota_hero_queenofpain", true);

            heroMenu.AddItem(new MenuItem("zaioQueenOfPainAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioQueenOfPainAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioQueenOfPainKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioQueenOfPainKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            this._minimumEnemyUltCount =
                new MenuItem("zaioQueenOfPainMinEnemyCount", "Minimum Enemies for Ult").SetValue(new Slider(1, 0, 4));
            this._minimumEnemyUltCount.Tooltip = "Minimum enemies besides your target to use ult.";
            heroMenu.AddItem(this._minimumEnemyUltCount);

            this.OnLoadMenuItems(supportedStuff, supportedKillsteal);

            ZaioMenu.LoadHeroSettings(heroMenu);

            this._qAbility = this.MyHero.GetAbilityById(AbilityId.queenofpain_shadow_strike);
            this._wAbility = this.MyHero.GetAbilityById(AbilityId.queenofpain_blink);
            this._eAbility = this.MyHero.GetAbilityById(AbilityId.queenofpain_scream_of_pain);
            this._ultAbility = this.MyHero.GetAbilityById(AbilityId.queenofpain_sonic_wave);
        }

        protected override async Task<bool> Killsteal()
        {
            if (await base.Killsteal())
            {
                return true;
            }

            if (this.MyHero.IsSilenced())
            {
                return false;
            }

            if (this._eAbility.IsKillstealAbilityEnabled() && this._eAbility.CanBeCasted())
            {
                var damage = (float)this._eAbility.GetDamage(this._eAbility.Level - 1);
                damage *= this.GetSpellAmp();
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != this.MyHero.Team && !x.IsIllusion
                                         && this._eAbility.CanBeCasted() && this._eAbility.CanHit(x)
                                         && !x.IsMagicImmune() && x.Health < damage * (1 - x.MagicResistance())
                                         && !x.CantBeAttacked() && !x.CantBeKilled());
                if (enemy != null)
                {
                    Log.Debug($"killsteal with scream based on enemy hp {enemy.Health} <= {damage} ");
                    this._eAbility.UseAbility();
                    var dist =
                        (int)(enemy.Distance2D(this.MyHero) / this._eAbility.GetAbilityData("projectile_speed") * 1000);
                    await Await.Delay(this.GetAbilityDelay(this._eAbility) + dist);
                    return true;
                }
            }

            if (this._ultAbility.IsKillstealAbilityEnabled() && this._ultAbility.CanBeCasted())
            {
                var damage = this.MyHero.HasItem(ItemId.item_ultimate_scepter)
                                 ? this._ultAbility.GetAbilityData("damage_scepter")
                                 : this._ultAbility.GetAbilityData("damage");

                damage *= this.GetSpellAmp();
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != this.MyHero.Team && !x.IsIllusion
                                         && this._ultAbility.CanBeCasted(x) && this._ultAbility.CanHit(x)
                                         && x.Health < damage && !x.CantBeAttacked() && !x.CantBeKilled());
                if (enemy != null)
                {
                    Log.Debug($"killsteal with ult scream based on enemy hp {enemy.Health} <= {damage} ");
                    this._ultAbility.UseAbility(enemy.NetworkPosition);
                    await Await.Delay(this.GetAbilityDelay(enemy.NetworkPosition, this._ultAbility));
                    return true;
                }
            }

            if (this._qAbility.IsKillstealAbilityEnabled() && this._qAbility.CanBeCasted())
            {
                var damage = this._qAbility.GetAbilityData("strike_damage");
                damage *= this.GetSpellAmp();
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != this.MyHero.Team && !x.IsIllusion
                                         && this._qAbility.CanBeCasted() && this._qAbility.CanHit(x)
                                         && !x.IsMagicImmune() && x.Health < damage * (1 - x.MagicResistance())
                                         && !x.IsLinkensProtected() && !x.CantBeAttacked() && !x.CantBeKilled());
                if (enemy != null)
                {
                    Log.Debug($"killsteal with ss based on enemy hp {enemy.Health} <= {damage} ");
                    this._qAbility.UseAbility(enemy);
                    await Await.Delay(this.GetAbilityDelay(enemy, this._qAbility));
                    return true;
                }
            }

            return false;
        }
    }
}