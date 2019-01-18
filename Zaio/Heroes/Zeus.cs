using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Enums;
using Ensage.Common.Extensions;
using Ensage.Common.Menu;
using Ensage.Common.Threading;
using log4net;
using PlaySharp.Toolkit.Logging;
using Zaio.Helpers;
using Zaio.Interfaces;
using AbilityId = Ensage.AbilityId;

using MyAsyncHelpers = Zaio.Helpers.MyAsyncHelpers;

namespace Zaio.Heroes
{
    internal struct HealthInfo
    {
        public float Time;
        public uint Health;
        public float HealthRegeneration;
        public float MagicRes;

        public HealthInfo(float rawGameTime, uint health, float healthRegeneration, float magicRes)
        {
            Time = rawGameTime;
            Health = health;
            HealthRegeneration = healthRegeneration;
            MagicRes = magicRes;
        }
    }

    [Hero(HeroId.npc_dota_hero_zuus)]
    internal class Zeus : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "zuus_arc_lightning",
            "zuus_lightning_bolt"
            // "zuus_thundergods_wrath",
        };

        private static readonly string[] KillstealAbilities =
        {
            "zuus_arc_lightning",
            "zuus_lightning_bolt",
            "zuus_thundergods_wrath"
        };

        private readonly Dictionary<Hero, HealthInfo> _visibilityInformation = new Dictionary<Hero, HealthInfo>();

        private Ability _qAbility;

        private MenuItem _smartUltKillsteal;
        private Ability _ultAbility;
        private Ability _wAbility;

        private bool ShouldSmartKillsteal => _smartUltKillsteal.GetValue<bool>();

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("Zeus", "zaioZeus", false, "npc_dota_hero_zuus", true);

            heroMenu.AddItem(new MenuItem("zaioZeusAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioZeusAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioZeusKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioZeusKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            OnLoadMenuItems(supportedStuff, supportedKillsteal);

            _smartUltKillsteal = new MenuItem("zaioSmartUltKillsteal", "Smart Killsteal").SetValue(true);
            _smartUltKillsteal.Tooltip =
                "Only use ult to killsteal when enemy is regenerating, not visible, or more than one enemy can be killed.";
            heroMenu.AddItem(_smartUltKillsteal);

            ZaioMenu.LoadHeroSettings(heroMenu);

            _qAbility = MyHero.GetAbilityById(AbilityId.zuus_arc_lightning);
            _wAbility = MyHero.GetAbilityById(AbilityId.zuus_lightning_bolt);
            _ultAbility = MyHero.GetAbilityById(AbilityId.zuus_thundergods_wrath);

            GameDispatcher.OnIngameUpdate += GameDispatcher_OnIngameUpdate;
        }

        private void GameDispatcher_OnIngameUpdate(EventArgs args)
        {
            foreach (
                var hero in
                ObjectManager.GetEntitiesParallel<Hero>()
                             .Where(x => x.IsValid && x.Team != MyHero.Team && x.IsAlive && x.IsVisible && !x.IsIllusion)
                             .ToList())
            {
                HealthInfo value;
                if (!_visibilityInformation.TryGetValue(hero, out value))
                {
                    _visibilityInformation.Add(hero,
                        new HealthInfo(Game.RawGameTime, hero.Health, hero.HealthRegeneration, hero.MagicResistance()));
                }
                else
                {
                    _visibilityInformation[hero] = new HealthInfo(Game.RawGameTime, hero.Health, hero.HealthRegeneration,
                        hero.MagicResistance());
                }
            }
            Await.Block("zaio.zuusVisibilityInfo", MyAsyncHelpers.AsyncSleep);
        }


        public override void OnClose()
        {
            _visibilityInformation.Clear();
            base.OnClose();
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

            var spellAmp = GetSpellAmp();

            if (_qAbility.IsKillstealAbilityEnabled() && _qAbility.CanBeCasted())
            {
                var damage = spellAmp * _qAbility.GetDamage(_qAbility.Level - 1);

                var enemy = ObjectManager.GetEntitiesParallel<Hero>().FirstOrDefault(
                    x =>
                        x.IsValid && x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                        _qAbility.CanBeCasted(x) && _qAbility.CanHit(x) &&
                        x.Health < damage * (1 - x.MagicResistance()) &&
                        !x.CantBeAttacked() && !x.CantBeKilled());
                if (enemy != null)
                {
                    Log.Debug(
                        $"using killsteal Q on {enemy.Name}: {enemy.Health} < {damage * (1 - enemy.MagicResistance())}");
                    _qAbility.UseAbility(enemy);
                    await Await.Delay(GetAbilityDelay(enemy, _qAbility));
                    return true;
                }
            }

            if (_wAbility.IsKillstealAbilityEnabled() && _wAbility.CanBeCasted())
            {
                var damage = spellAmp * _wAbility.GetDamage(_wAbility.Level - 1);

                var enemy = ObjectManager.GetEntitiesParallel<Hero>().FirstOrDefault(
                    x =>
                        x.IsValid && x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                        _wAbility.CanBeCasted(x) && _wAbility.CanHit(x) &&
                        x.Health < damage * (1 - x.MagicResistance()) &&
                        !x.CantBeAttacked() && !x.CantBeKilled());
                if (enemy != null)
                {
                    Log.Debug(
                        $"using killsteal W on {enemy.Name}: {enemy.Health} < {damage * (1 - enemy.MagicResistance())}");
                    _wAbility.UseAbility(enemy.NetworkPosition);
                    await Await.Delay(GetAbilityDelay(enemy, _wAbility));
                    return true;
                }
            }

            if (_ultAbility.IsKillstealAbilityEnabled() && _ultAbility.CanBeCasted())
            {
                var damage = _ultAbility.GetAbilityData("damage") * spellAmp;

                var enemy = ObjectManager.GetEntitiesParallel<Hero>().FirstOrDefault(
                    x =>
                        x.IsValid && x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion &&
                        _ultAbility.CanBeCasted(x) && _ultAbility.CanHit(x) &&
                        x.Health < damage * (1 - x.MagicResistance()) &&
                        !x.CantBeAttacked() && !x.CantBeKilled());

                var castPoint = _ultAbility.FindCastPoint();
                if (enemy != null)
                {
                    bool useUlt;
                    if (ShouldSmartKillsteal)
                    {
                        Log.Debug($"{enemy.IsVisible}");
                        if (enemy.Health + enemy.HealthRegeneration * castPoint * 2 >=
                            damage * (1 - enemy.MagicResistance()))
                        {
                            useUlt = true;
                            Log.Debug(
                                $"enemy regenerating too much {enemy.Health} + {enemy.HealthRegeneration * castPoint * 2} >= {damage * (1 - enemy.MagicResistance())}");
                        }
                        else
                        {
                            useUlt =
                                ObjectManager.GetEntitiesParallel<Hero>()
                                             .Any(
                                                 x =>
                                                     x.IsValid && x != enemy && x.IsAlive && x.Team != MyHero.Team &&
                                                     !x.IsIllusion &&
                                                     _ultAbility.CanBeCasted(x) && _ultAbility.CanHit(x) &&
                                                     x.Health < damage * (1 - x.MagicResistance()) &&
                                                     !x.CantBeAttacked() && !x.CantBeKilled());
                            Log.Debug($"more than one enemy low {useUlt}");
                        }
                    }
                    else
                    {
                        useUlt = true;
                    }
                    if (useUlt)
                    {
                        Log.Debug(
                            $"using killsteal ult on {enemy.Name}: {enemy.Health} < {damage * (1 - enemy.MagicResistance())}");
                        _ultAbility.UseAbility();
                        await Await.Delay((int) (castPoint * 1000.0f));
                        return true;
                    }
                }
                if (ShouldSmartKillsteal)
                {
                    var currentTime = Game.RawGameTime;

                    foreach (var entry in _visibilityInformation.ToList())
                    {
                        if (!entry.Key.IsAlive)
                        {
                            continue;
                        }

                        var healthInfo = entry.Value;
                        var timeDiff = currentTime - healthInfo.Time;
                        var dmg = damage * (1 - healthInfo.MagicRes);
                        if (0.1 < timeDiff && timeDiff < 3.0 && healthInfo.Health < dmg &&
                            healthInfo.Health + healthInfo.HealthRegeneration * (castPoint * 2 + timeDiff) < dmg)
                        {
                            Log.Debug(
                                $"enemy not visible1! {timeDiff} | {healthInfo.Health} + {healthInfo.HealthRegeneration * (castPoint * 2 + timeDiff)} >= {damage * (1 - healthInfo.MagicRes)} ");
                            _ultAbility.UseAbility();
                            await Await.Delay((int) (castPoint * 1000.0f));
                        }
                    }
                }
            }
            return false;
        }


        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            await HasNoLinkens(target, tk);
            await UseItems(target, tk);
            await DisableEnemy(target, tk);

            if (!MyHero.IsSilenced())
            {
                if (_wAbility.IsAbilityEnabled() && _wAbility.CanBeCasted(target) && _wAbility.CanHit(target))
                {
                    Log.Debug($"use W");
                    _wAbility.UseAbility(target);
                    await Await.Delay(GetAbilityDelay(target, _wAbility), tk);
                }

                if (_qAbility.IsAbilityEnabled() && _qAbility.CanBeCasted(target) && _qAbility.CanHit(target))
                {
                    Log.Debug($"use Q");
                    _qAbility.UseAbility(target);
                    await Await.Delay(GetAbilityDelay(target, _qAbility), tk);
                }
            }


            // check if we are near the enemy
            if (!await MoveOrBlinkToEnemy(target, tk))
            {
                Log.Debug($"return because of blink");
                return;
            }

            if (ZaioMenu.ShouldUseOrbwalker)
            {
                Orbwalk();
            }
        }
    }
}