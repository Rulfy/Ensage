using System.Collections.Generic;
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
using Zaio.Prediction;

using AbilityId = Ensage.Common.Enums.AbilityId;
using Prediction = Ensage.Common.Prediction;


namespace Zaio.Heroes
{
    [Hero(ClassID.CDOTA_Unit_Hero_AntiMage)]
    internal class AntiMage : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "antimage_blink",
            "antimage_mana_void"
        };

        private static readonly string[] KillstealAbilities =
        {
            "antimage_mana_void"
        };

        private Ability _blinkAbility;

        private Ability _ultAbility;

        private MenuItem _minimumEnemyUltCount;

        private int EnemyCountForUlt => this._minimumEnemyUltCount.GetValue<Slider>().Value;

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("AntiMage", "zaioAntiMage", false, "npc_dota_hero_antimage", true);

            heroMenu.AddItem(new MenuItem("zaioAntiMageAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioAntiMageAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioAntiMageKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioAntiMageKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            this._minimumEnemyUltCount =
            new MenuItem("zaioAntiMageMinEnemyCount", "Minimum Enemies for Ult").SetValue(new Slider(1, 0, 4));
            this._minimumEnemyUltCount.Tooltip = "Minimum enemies besides your target to use ult.";
            heroMenu.AddItem(this._minimumEnemyUltCount);

            this.OnLoadMenuItems(supportedStuff, supportedKillsteal);

            ZaioMenu.LoadHeroSettings(heroMenu);

            this._blinkAbility = MyHero.GetAbilityById(AbilityId.antimage_blink);
            this._ultAbility = MyHero.GetAbilityById(AbilityId.antimage_mana_void);
        }

        protected override async Task<bool> Killsteal()
        {
            if (await base.Killsteal())
            {
                return true;
            }

            if (!this.MyHero.IsSilenced() && this._ultAbility.IsKillstealAbilityEnabled() && this._ultAbility.CanBeCasted())
            {
                var damage = _ultAbility.GetAbilityData("mana_void_damage_per_mana");
                //Create a list of all enemies which can be ulted by AM
                List<Hero> enemies = null;
                enemies =
                    ObjectManager.GetEntitiesParallel<Hero>()
                        .Where(
                             x =>
                                 x.IsAlive && x.Team != this.MyHero.Team && !x.IsIllusion
                                 && this._ultAbility.CanBeCasted() && this._ultAbility.CanHit(x)
                                 && !x.IsLinkensProtected()
                                 && !x.CantBeAttacked() && !x.CantBeKilled())
                                 .ToList();

                var current_damage = 0.0;
                var previous_target = default(Ensage.Hero);
                var best_target = default(Ensage.Hero);
                var killable_enemies = 0;

                //Radius KS
                foreach (var enemy in enemies)
                {

                    var possible_damage = damage;
                    possible_damage *= (enemy.MaximumMana - enemy.Mana);

                    //Check if the possible damage exceeds what the damage would be to our previous enemy
                    if (possible_damage >= current_damage)
                    {
                        best_target = enemy;
                    }

                    //Set the current damage to this enemy
                    current_damage = possible_damage;

                    //Check if an enemy is killable in the radius of AM's ult
                    var radius = _ultAbility.GetAbilityData("mana_void_aoe_radius");
                    var polygon = new Geometry.Polygon.Circle(best_target.NetworkPosition, radius);

                    if(previous_target != default(Ensage.Hero) && polygon.IsInside(previous_target.NetworkPosition) 
                      && (current_damage * (1 - previous_target.MagicResistance()) >= previous_target.Health))
                    {
                        //Add enemy to amount that can be killed
                        killable_enemies += 1;

                        //Ult once we reach the amount of enemies we want to kill
                        if (EnemyCountForUlt == 0 && killable_enemies >= EnemyCountForUlt)
                        {
                            this._ultAbility.UseAbility(enemy);
                            await Await.Delay(this.GetAbilityDelay(this._ultAbility));
                        }


                    }

                    //Regular KS
                    var enemy_r =
                        ObjectManager.GetEntitiesParallel<Hero>()
                            .FirstOrDefault(
                                x =>
                                    x.IsAlive && x.Team != this.MyHero.Team && !x.IsIllusion
                                    && this._ultAbility.CanBeCasted() && this._ultAbility.CanHit(x)
                                    && x.Health < (damage * (x.MaximumMana - x.Mana) * (1 - x.MagicResistance()))
                                    && !x.IsLinkensProtected() && !x.CantBeAttacked() && !x.CantBeKilled());

                    if (enemy_r != null && killable_enemies >= EnemyCountForUlt)
                    {
                        Log.Debug($"{killable_enemies} {EnemyCountForUlt}");
                        this._ultAbility.UseAbility(enemy_r);
                        await Await.Delay(this.GetAbilityDelay(this._ultAbility));
                    }

                    previous_target = enemy;

                }



            }

            return false;
        }

        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {

          if (!await this.MoveOrBlinkToEnemy(target, tk, 0, 150))
          {

            if (!this.MyHero.IsSilenced())
            {
                if (this._blinkAbility.CanHit(target)
                && this._blinkAbility.IsAbilityEnabled()
                && this._blinkAbility.CanBeCasted(target))
                {
                        bool usePrediction = false;

                        if (target.IsMoving)
                        {
                            usePrediction = true;
                        }
                        if (MyHero.Distance2D(target) >= 350)
                        {
                            if (!usePrediction)
                            {
                                var pos = (target.NetworkPosition - MyHero.NetworkPosition).Normalized();
                                pos *= 150;
                                pos = target.NetworkPosition - pos;
                                Log.Debug($"Jumping");
                                this._blinkAbility.UseAbility(pos);
                            }

                            if (usePrediction)
                            {
                                Log.Debug($"jumping");
                                var jump_pos = Ensage.Common.Prediction.InFront(target, 250);
                                this._blinkAbility.UseAbility(jump_pos);
                            }
                        }

                    
                  }

                
            }
          }

            await HasNoLinkens(target, tk);
            await UseItems(target, tk);
            await DisableEnemy(target, tk);

            if(!MyHero.IsSilenced())
            {
                if (_ultAbility.IsAbilityEnabled() && _ultAbility.CanBeCasted(target) && _ultAbility.CanHit(target))
                {
                    var damage = _ultAbility.GetAbilityData("mana_void_damage_per_mana");
                    var target_mana = target.Mana;
                    var total_mana = target.MaximumMana;
                    damage *= total_mana - target_mana;
                    damage = damage * (1 - target.MagicResistance());


                    if (damage > target.Health)
                    {
                        Log.Debug($"Kill it with fire");
                        this._ultAbility.UseAbility(target);
                    }
                }
            }

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk();
            }

            else
            {
                MyHero.Attack(target);
                await Await.Delay(125, tk);
            }
        }


    }
}
