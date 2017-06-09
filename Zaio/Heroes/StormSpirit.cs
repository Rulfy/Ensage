using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Ensage;
using Ensage.Common.Extensions;
using Ensage.Common.Extensions.SharpDX;
using Ensage.Common.Menu;
using Ensage.Common.Threading;

using log4net;

using PlaySharp.Toolkit.Logging;

using Zaio.Helpers;
using Zaio.Interfaces;
using AbilityId = Ensage.AbilityId;
using Cock = Ensage.Common.Prediction;
using System;
using Ensage.Common;

namespace Zaio.Heroes
{
    [Hero(ClassId.CDOTA_Unit_Hero_StormSpirit)]
    internal class StormSpirit : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "storm_spirit_static_remnant",
            "storm_spirit_electric_vortex"
        };

        private static readonly string[] KillstealAbilities =
        {
            "storm_spirit_ball_lightning",
            "storm_spirit_static_remnant"
        };

        private Ability _ultAbility;

        private Ability _qAbility;

        private Ability _eAbility;

        private Ability _wAbility;

        private MenuItem _maxChaseDistance;

        private MenuItem _minMana;

        private int maxYards => this._maxChaseDistance.GetValue<Slider>().Value;

        private int minMana => this._minMana.GetValue<Slider>().Value;



        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("StormSpirit", "zaioStormSpirit", false, "npc_dota_hero_storm_spirit", true);

            heroMenu.AddItem(new MenuItem("zaioStormSpiritAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioStormSpiritAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioStormSpiritKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioStormSpiritKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            this._maxChaseDistance =
            new MenuItem("zaioMaxUltDistance", "Maximum distance for ult").SetValue(new Slider(1000, 0, 10000));
            this._maxChaseDistance.Tooltip = "Maximum number of yards between you and your target before you ult";
            heroMenu.AddItem(this._maxChaseDistance);

            this._minMana =
            new MenuItem("zaioMinMana", "Min Mana after ult").SetValue(new Slider(1, 0, (int)MyHero.Mana));
            this._minMana.Tooltip = "Min Mana after ult to catch up";
            heroMenu.AddItem(this._minMana);

            OnLoadMenuItems(supportedStuff, supportedKillsteal);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _qAbility = MyHero.GetAbilityById(AbilityId.storm_spirit_static_remnant);
            _wAbility = MyHero.GetAbilityById(AbilityId.storm_spirit_electric_vortex);
            _eAbility = MyHero.GetAbilityById(AbilityId.storm_spirit_overload);
            _ultAbility = MyHero.GetAbilityById(AbilityId.storm_spirit_ball_lightning);
        }

        public async Task StormAuto()
        {
            await Task.Delay(900);
        }

        protected override async Task<bool> Killsteal()
        {

            if (await base.Killsteal())
            {
                return true;
            }

            if (MyHero.IsSilenced())
            {
                return false;
            }

            float _qAutoDamage = this._qAbility.GetAbilityData("static_remnant_damage") + this._eAbility.GetDamage(_eAbility.Level -1);
            _qAutoDamage += (MyHero.MinimumDamage + MyHero.BonusDamage);
            _qAutoDamage *= GetSpellAmp();

            float _eAutoDamage = this._eAbility.GetDamage(_eAbility.Level -1);
            _eAutoDamage += (MyHero.MinimumDamage + MyHero.BonusDamage);
            _eAutoDamage *= GetSpellAmp();

            if (this._qAbility.IsKillstealAbilityEnabled() && this._qAbility.CanBeCasted())
            {
                var qAutokillableTar =
                                ObjectManager.GetEntitiesParallel<Hero>()
                                             .FirstOrDefault(
                                                 x =>
                                                     x.IsAlive && x.Team != this.MyHero.Team && !x.IsIllusion
                                                     && this._qAbility.CanBeCasted() && this._qAbility.CanHit(x) 
                                                     && x.Health < (_qAutoDamage * (1 - x.MagicResistance()))
                                                     && !x.IsMagicImmune() && !x.CantBeKilled() && !x.CantBeAttacked()
                                                     && x.Distance2D(this.MyHero) <= 235);

                var AutokillableTar =
                ObjectManager.GetEntitiesParallel<Hero>()
                             .FirstOrDefault(
                                 x =>
                                     x.IsAlive && x.Team != this.MyHero.Team && !x.IsIllusion
                                     && x.Health < _eAutoDamage * (1 - x.MagicResistance())
                                     && !x.IsMagicImmune() && !x.CantBeKilled() && !x.CantBeAttacked()
                                     && x.Distance2D(this.MyHero) <= 480);

                if (this.MyHero.HasModifier("modifier_storm_spirit_overload") && AutokillableTar != null)
                {
                    MyHero.Attack(AutokillableTar);
                    Await.Block("zaioAutoAttack", StormAuto);
                }

                if (AutokillableTar != null && _qAbility.CanBeCasted() && !MyHero.HasModifier("modifier_storm_spirit_overload"))
                {
                    Log.Debug($"Killing with auto {MyHero.HasModifier("modifier_storm_spirit_overload")}");
                    _qAbility.UseAbility();
                    await Await.Delay(GetAbilityDelay(_qAbility));
                    MyHero.Attack(AutokillableTar);
                    Await.Block("zaioAutoAttack", StormAuto);

                }

                if (AutokillableTar != null && _ultAbility.CanBeCasted() && !_qAbility.CanBeCasted() && !MyHero.HasModifier("modifier_storm_spirit_overload"))
                {
                    var moves = Cock.InFront(AutokillableTar, 50);
                    Log.Debug($"Killable with auto, q not available {MyHero.HasModifier("modifier_storm_spirit_overload")}");
                    _ultAbility.UseAbility(moves);
                    await Await.Delay(GetAbilityDelay(_ultAbility));
                    MyHero.Attack(AutokillableTar);
                    Await.Block("zaioAutoAttack", StormAuto);
                }

                if(qAutokillableTar != null && _qAbility.CanBeCasted())
                {
                    Log.Debug($"Killing with q and auto");
                    _qAbility.UseAbility();
                    await Await.Delay(GetAbilityDelay(_qAbility));
                    MyHero.Attack(qAutokillableTar);
                    Await.Block("zaioAutoAttack", StormAuto);
                }



            }
                


            return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {

            var myHeroNetworkPosition = this.MyHero.NetworkPosition;
            if (!await MoveOrBlinkToEnemy(target, tk, 235, 480))
            {
                if (!this.MyHero.IsSilenced() && this.MyHero.Distance2D(target) >= 235 && this.MyHero.Distance2D(target) <= maxYards)
                {
                    var pos = (target.NetworkPosition - myHeroNetworkPosition).Normalized();
                    pos *= 100;
                    pos = target.NetworkPosition + pos;
                    float mana = MyHero.MaximumMana;
                    
                    if (this._ultAbility.IsAbilityEnabled())
                    {
                        if (target.IsMoving)
                        {
                            Log.Debug($"Jumping the gun");
                            var moves = Cock.InFront(target, 300);
                            double consumedMana = (_ultAbility.GetAbilityData("ball_lightning_initial_mana_base") + ((_ultAbility.GetAbilityData("ball_lightning_initial_mana_percentage") / 100) * mana))
                                + ((MyHero.Distance2D(moves) / 100) * (((_ultAbility.GetAbilityData("ball_lightning_travel_cost_percent") / 100) * mana)));

                            Log.Debug($"{consumedMana}");

                            if (consumedMana <= MyHero.Mana && (MyHero.Mana - consumedMana >= minMana))
                            {
                                this._ultAbility.UseAbility(moves);
                                await Await.Delay(GetAbilityDelay(_ultAbility) + (int)(this.MyHero.GetTurnTime(pos) * 1000), tk);
                                MyHero.Attack(target);
                                Await.Block("zaioAutoAttack", StormAuto);
                            }

                        }

                        else
                        {
                            Log.Debug($"Jumping close but far");
                            double consumedMana = (_ultAbility.GetAbilityData("ball_lightning_initial_mana_base") + ((_ultAbility.GetAbilityData("ball_lightning_initial_mana_percentage") / 100) * mana))
                                + ((MyHero.Distance2D(pos) / 100) * (((_ultAbility.GetAbilityData("ball_lightning_travel_cost_percent") / 100) * mana)));
                            Log.Debug($"{consumedMana} {(MyHero.Distance2D(pos) / 100)}");
                            if (consumedMana <= MyHero.Mana && (MyHero.Mana - consumedMana >= minMana))
                            {
                                this._ultAbility.UseAbility(pos);
                                await Await.Delay(GetAbilityDelay(_ultAbility) + (int)(this.MyHero.GetTurnTime(pos) * 1000), tk);
                                MyHero.Attack(target);
                                Await.Block("zaioAutoAttack", StormAuto);
                            }
                        }
                    }
                }
                    Log.Debug($"return because of blink");
                return;
            }



            await this.HasNoLinkens(target, tk);
            await this.UseItems(target, tk);
            await this.DisableEnemy(target, tk);

            if (!MyHero.IsSilenced())
            {
                bool hasAgha = MyHero.HasItem(ClassId.CDOTA_Item_UltimateScepter);
                float qCost = _qAbility.GetAbilityData("Mana cost");
                float wCost = _wAbility.GetAbilityData("Mana cost");
                float myMana = MyHero.Mana;
                float mana = MyHero.MaximumMana;
                var pos = (target.NetworkPosition - myHeroNetworkPosition).Normalized();
                pos *= 100;
                pos = target.NetworkPosition + pos;
                double consumedMana = (_ultAbility.GetAbilityData("ball_lightning_initial_mana_base") + ((_ultAbility.GetAbilityData("ball_lightning_initial_mana_percentage") / 100) * mana))
                    + ((MyHero.Distance2D(pos) / 100) * (((_ultAbility.GetAbilityData("ball_lightning_travel_cost_percent") / 100) * mana)));

                if (_qAbility.IsAbilityEnabled() && _qAbility.CanBeCasted()
                    && _wAbility.IsAbilityEnabled() && _wAbility.CanBeCasted()
                    && (qCost + wCost) <= myMana && !this.MyHero.HasModifier("modifier_storm_spirit_overload"))
                {
                    if(!hasAgha)
                    {
                        this._wAbility.UseAbility(target);
                        await Await.Delay(GetAbilityDelay(_wAbility), tk);
                        MyHero.Attack(target);
                        await Await.Delay(850);
                    }

                    else
                    {
                        this._wAbility.UseAbility();
                        await Await.Delay(GetAbilityDelay(_wAbility), tk);
                        MyHero.Attack(target);
                        await Await.Delay(850);
                    }
                    
                }

                if(_qAbility.IsAbilityEnabled() && _qAbility.CanBeCasted() && !this.MyHero.HasModifier("modifier_storm_spirit_overload")
                    && (!_wAbility.CanBeCasted() || (qCost + wCost >= myMana)) && (MyHero.Mana - consumedMana >= _qAbility.GetAbilityData("Mana cost")))
                {
                    this._qAbility.UseAbility();
                    await Await.Delay(GetAbilityDelay(_qAbility), tk);
                    MyHero.Attack(target);
                    await Await.Delay(850);
                }

                if(_ultAbility.IsAbilityEnabled() && (!_qAbility.CanBeCasted() || this.MyHero.Distance2D(target) >= _qAbility.GetAbilityData("static_remnant_radius")) && (!_wAbility.CanBeCasted(target) || MyHero.Distance2D(target) >= _wAbility.GetAbilityData("Cast range"))
                    && this.MyHero.Distance2D(target) <= maxYards && !this.MyHero.HasModifier("modifier_storm_spirit_overload"))
                {
                    this._ultAbility.UseAbility(pos);
                    await Await.Delay(GetAbilityDelay(_ultAbility), tk);
                    MyHero.Attack(target);
                    await Await.Delay(850);
                }

                if(this.MyHero.HasModifier("modifier_storm_spirit_overload"))
                {
                    MyHero.Attack(target);
                    await Await.Delay(850);
                }

            }

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk();
            }
        }


    }
}