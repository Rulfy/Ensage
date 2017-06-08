namespace Zaio.Heroes
{
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Ensage;
    using Ensage.Common.Extensions;
    using Ensage.Common.Menu;

    using log4net;

    using PlaySharp.Toolkit.Logging;

    using Zaio.Helpers;
    using Zaio.Interfaces;

    using AbilityId = Ensage.AbilityId;
    using Prediction = Ensage.Common.Prediction;
    using Ensage.Common.Threading;
    using Ensage.Common.Extensions.SharpDX;
    using System;

    [Hero(ClassId.CDOTA_Unit_Hero_Oracle)]
    internal class Oracle : ComboHero
    {
        private static readonly string[] KillstealAbilities =
            {
                "oracle_purifying_flames",
                "oracle_fates_edict"
            };

        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
            {
                "oracle_fortunes_end",
                "oracle_fates_edict",
                "oracle_purifying_flames"
            };
        private MenuItem _HPThreshold;

        private Ability _eAbility;

        private Ability _qAbility;

        private Ability _ultAbility;

        private Ability _wAbility;

        private float AllyHPforHeal => this._HPThreshold.GetValue<Slider>().Value;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Oracle", "zaioOracle", false, "npc_dota_hero_oracle", true);

            heroMenu.AddItem(new MenuItem("zaioOracleAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioOracleAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioOracleKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioOracleKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            this._HPThreshold =
                new MenuItem("zaioOracleHealPercentage", "Minimum Percent for Heal").SetValue(new Slider(1, 0, 100));
            this._HPThreshold.Tooltip = "Percent of ally life before heal";
            heroMenu.AddItem(this._HPThreshold);

            this.OnLoadMenuItems(supportedStuff, supportedKillsteal);

            ZaioMenu.LoadHeroSettings(heroMenu);

            this._qAbility = this.MyHero.GetAbilityById(AbilityId.oracle_fortunes_end);
            this._wAbility = this.MyHero.GetAbilityById(AbilityId.oracle_fates_edict);
            this._eAbility = this.MyHero.GetAbilityById(AbilityId.oracle_purifying_flames);
            this._ultAbility = this.MyHero.GetAbilityById(AbilityId.oracle_false_promise);
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {



            // check if we are near the enemy
            var myHeroNetworkPosition = this.MyHero.NetworkPosition;
            if (!await this.MoveOrBlinkToEnemy(target, tk, 200, 800))
            {
               return;
            }

            await this.HasNoLinkens(target, tk);
            await this.UseItems(target, tk);
            await this.DisableEnemy(target, tk);

            if (!this.MyHero.IsSilenced())
            {
                var pos = (target.NetworkPosition - myHeroNetworkPosition).Normalized();



                    if (this.MyHero.Distance2D(target) >= 600)
                {
                    if (this._qAbility.IsAbilityEnabled() && this._qAbility.CanBeCasted(target) && this._eAbility.CanBeCasted(target))
                    {
                        Log.Debug($"use q");
                        this._qAbility.UseAbility(target);
                        await Await.Delay(this.GetAbilityDelay(this._qAbility), tk);

                        if (this._eAbility.IsAbilityEnabled() && this._eAbility.CanBeCasted(target))
                        {
                            Log.Debug($"use e");
                            this._eAbility.UseAbility(target);
                            await Await.Delay(this.GetAbilityDelay(this._eAbility), tk);
                        }
                    }


                }

                if(this.MyHero.Distance2D(target) <= 600)
                {
                    if (this._eAbility.IsAbilityEnabled() && this._eAbility.CanBeCasted(target) && this._qAbility.CanBeCasted(target))
                    {
                        Log.Debug($"use e");
                        this._eAbility.UseAbility(target);
                        await Await.Delay(this.GetAbilityDelay(this._eAbility), tk);
                    }

                    if (this._qAbility.IsAbilityEnabled() && this._qAbility.CanBeCasted(target))
                    {
                        Log.Debug($"use q");
                        this._qAbility.UseAbility(target);
                    }
                }

                if (ZaioMenu.ShouldUseOrbwalker)
                {
                    this.Orbwalk();
                }


            }
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
                var damage = (float)this._eAbility.GetAbilityData("damage");
                damage *= this.GetSpellAmp();
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                        .FirstOrDefault(
                             x =>
                                 x.IsAlive && x.Team != this.MyHero.Team && !x.IsIllusion
                                 && this._eAbility.CanBeCasted() && !x.IsMagicImmune()
                                 && x.Health < damage * (1 - x.MagicResistance())
                                 && !x.CantBeKilled());
                if (enemy != null)
                {
                    Log.Debug($"Killstealing");
                    this._eAbility.UseAbility(enemy);
                    await Await.Delay(0);
                }
            }

            if (this._wAbility.IsKillstealAbilityEnabled())
            {
                var _tarHeal =
                                     ObjectManager.GetEntitiesParallel<Hero>()
                                        .FirstOrDefault(
                                             x =>
                                             x.IsAlive && x.Team == this.MyHero.Team && !x.IsIllusion
                                             && !x.IsMagicImmune() && Math.Round((double)x.Health / x.MaximumHealth * 100) <= AllyHPforHeal);



                    if (_tarHeal != null && this._eAbility.CanBeCasted()
                        && _tarHeal.HasModifier("modifier_oracle_fates_edict") && this.MyHero.Distance2D(_tarHeal) <= 800)
                    {
                        
                        Log.Debug($"heal {_tarHeal}");
                        this._eAbility.UseAbility(_tarHeal);
                        await Await.Delay(this.GetAbilityDelay(this._eAbility));
                    }

                    if (_tarHeal != null && this._wAbility.CanBeCasted()
                        && !_tarHeal.HasModifier("modifier_oracle_fates_edict") && this.MyHero.Distance2D(_tarHeal) <= 800)
                    {
                        Log.Debug($"w {_tarHeal}");
                        this._wAbility.UseAbility(_tarHeal);
                        await Await.Delay(this.GetAbilityDelay(this._wAbility));
                    }

                    var _ultAura =
                          ObjectManager.GetEntitiesParallel<Hero>()
                                        .FirstOrDefault(
                                             x =>
                                             x.IsAlive && x.Team == this.MyHero.Team && !x.IsIllusion
                                             && !x.IsMagicImmune() && x.HasModifier("modifier_oracle_false_promise_timer"));

                if (_ultAura != null && this._eAbility.CanBeCasted() && this.MyHero.Distance2D(_tarHeal) <= 800)
                {
                    Log.Debug($"heal ulted target");
                    this._eAbility.UseAbility(_ultAura);
                    await Await.Delay(this.GetAbilityDelay(this._eAbility));
                }

            }



            return false;
        }
    }
}