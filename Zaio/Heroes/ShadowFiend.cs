using System;
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
using SharpDX;
using Zaio.Helpers;
using Zaio.Interfaces;
using AbilityId = Ensage.AbilityId;

using MyAsyncHelpers = Zaio.Helpers.MyAsyncHelpers;

namespace Zaio.Heroes
{
    [Hero(HeroId.npc_dota_hero_nevermore)]
    internal class ShadowFiend : ComboHero
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string[] SupportedAbilities =
        {
            "nevermore_shadowraze1",
            //"nevermore_shadowraze2",
            //"nevermore_shadowraze3",
            "nevermore_requiem"
        };

        private static readonly string[] KillstealAbilities =
        {
            "nevermore_shadowraze1"
        };

        private readonly Ability[] _razeAbilities = new Ability[3];

        private readonly ParticleEffect[] _razeEffects = new ParticleEffect[3];

        private MenuItem _drawRazesItem;
        private Ability _ultAbility;
        private bool ShouldDrawRazes => _drawRazesItem.GetValue<bool>();

        public override void OnLoad()
        {
            base.OnLoad();

            var heroMenu = new Menu("ShadowFiend", "zaioShadowFiend", false, "npc_dota_hero_nevermore", true);

            heroMenu.AddItem(new MenuItem("zaioShadowFiendAbilitiesText", "Supported Abilities"));
            var supportedStuff = new MenuItem("zaioShadowFiendAbilities", string.Empty);
            supportedStuff.SetValue(new AbilityToggler(SupportedAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedStuff);

            heroMenu.AddItem(new MenuItem("zaioShadowFiendKillstealAbilitiesText", "Supported Killsteal Abilities"));
            var supportedKillsteal = new MenuItem("zaioShadowFiendKillstealAbilities", string.Empty);
            supportedKillsteal.SetValue(new AbilityToggler(KillstealAbilities.ToDictionary(x => x, y => true)));
            heroMenu.AddItem(supportedKillsteal);

            OnLoadMenuItems(supportedStuff, supportedKillsteal);

            _razeAbilities[0] = MyHero.GetAbilityById(AbilityId.nevermore_shadowraze1);
            _razeAbilities[1] = MyHero.GetAbilityById(AbilityId.nevermore_shadowraze2);
            _razeAbilities[2] = MyHero.GetAbilityById(AbilityId.nevermore_shadowraze3);

            _drawRazesItem = new MenuItem("zaioShadowFiendDrawRazes", "Draw Razes").SetValue(true);
            _drawRazesItem.Tooltip = "Draws the hitbox of each raze with a circle.";
            _drawRazesItem.ValueChanged += _drawRazesItem_ValueChanged;
            heroMenu.AddItem(_drawRazesItem);


            ToggleRazeEffects(_drawRazesItem.GetValue<bool>());

            ZaioMenu.LoadHeroSettings(heroMenu);

            _ultAbility = MyHero.GetAbilityById(AbilityId.nevermore_requiem);

            GameDispatcher.OnIngameUpdate += GameDispatcher_OnIngameUpdate;
        }

        public override void OnClose()
        {
            GameDispatcher.OnIngameUpdate -= GameDispatcher_OnIngameUpdate;
            base.OnClose();
        }

        private void GameDispatcher_OnIngameUpdate(EventArgs args)
        {
            for (var index = 0; index < _razeEffects.Length; index++)
            {
                var ability = _razeAbilities[index];
                var effect = _razeEffects[index];
                if (ability == null || effect == null)
                {
                    Await.Block("zaio.updateRazes", MyAsyncHelpers.AsyncSleep);
                    return;
                }
                var range = ability.GetAbilityData("shadowraze_range");
                effect.SetControlPoint(0, MyHero.InFront(range));
            }
        }

        private void ToggleRazeEffects(bool value)
        {
            if (value)
            {
                for (var index = 0; index < _razeEffects.Length; index++)
                {
                    var ability = _razeAbilities[index];
                    var range = ability.GetAbilityData("shadowraze_range");
                    var radius = ability.GetAbilityData("shadowraze_radius");

                    var effect = new ParticleEffect(@"particles\ui_mouseactions\drag_selected_ring.vpcf", MyHero,
                        ParticleAttachment.AbsOrigin);

                    effect.SetControlPoint(0, MyHero.InFront(range));
                    effect.SetControlPoint(1, new Vector3(255, 0, 0));
                    effect.SetControlPoint(2, new Vector3(radius, 255, 0));
                    effect.SetControlPoint(3, new Vector3(5, 0, 0));
                    _razeEffects[index] = effect;
                }
            }
            else
            {
                for (var index = 0; index < _razeEffects.Length; index++)
                {
                    if (_razeEffects[index] == null)
                        continue;
                    _razeEffects[index].Dispose();
                    _razeEffects[index] = null;
                }
            }
        }

        private void _drawRazesItem_ValueChanged(object sender, OnValueChangeEventArgs e)
        {
            ToggleRazeEffects(e.GetNewValue<bool>());
        }

        private async Task<bool> UseKillstealRaze(Ability ability, float spellAmp)
        {
            if (ability.CanBeCasted())
            {
                var range = ability.GetAbilityData("shadowraze_range");
                var radius = ability.GetAbilityData("shadowraze_radius");
                var point = MyHero.InFront(range);
                var damage = ability.GetDamage(ability.Level - 1) * spellAmp;
                var delay = ability.FindCastPoint() * 1000.0f;
                var enemy =
                    ObjectManager.GetEntitiesParallel<Hero>()
                                 .FirstOrDefault(
                                     x =>
                                         x.IsAlive && x.Team != MyHero.Team && !x.IsIllusion && ability.CanBeCasted(x) &&
                                         x.Health < damage * (1 - x.MagicResistance()) && !x.CantBeAttacked() &&
                                         !x.CantBeKilled() &&
                                         Prediction.Prediction.PredictPosition(x, (int) delay).Distance2D(point) <=
                                         radius);
                if (enemy != null)
                {
                    Log.Debug($"using {ability.Name} on {enemy.Name}");
                    ability.UseAbility();
                    await Await.Delay((int) (ability.FindCastPoint() * 1000 + Game.Ping));
                    return true;
                }
            }
            return false;
        }

        private async Task<bool> UseRazeOnTarget(Unit target, Ability ability)
        {
            if (ability.CanBeCasted(target) && !target.IsMagicImmune())
            {
                var range = ability.GetAbilityData("shadowraze_range");
                var radius = ability.GetAbilityData("shadowraze_radius");
                var point = MyHero.InFront(range);
                var delay = ability.FindCastPoint() * 1000.0f;
                var pos = Prediction.Prediction.PredictPosition(target, (int) delay);
                if (pos.Distance2D(point) <= radius)
                {
                    Log.Debug($"using {ability.Name}");
                    ability.UseAbility();
                    await Await.Delay((int) (ability.FindCastPoint() * 1000 + Game.Ping));
                    return true;
                }
            }
            return false;
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

            if (!_razeAbilities[0].IsKillstealAbilityEnabled())
                return false;

            var spellAmp = GetSpellAmp();
            foreach (var razeAbility in _razeAbilities)
            {
                if (await UseKillstealRaze(razeAbility, spellAmp))
                {
                    return true;
                }
            }

            return false;
        }


        public override async Task ExecuteComboAsync(Unit target, CancellationToken tk = new CancellationToken())
        {
            var eulsModifier = target.FindModifier("modifier_eul_cyclone");
            if (_ultAbility.IsAbilityEnabled() && eulsModifier == null && _ultAbility.CanBeCasted(target) && !MyHero.IsVisibleToEnemies ||
                eulsModifier != null && _ultAbility.CanBeCasted())
            {
                if (MyHero.IsInvisible() || eulsModifier != null)
                {
                    // Log.Debug($"using invis ult on enemy");
                    var distance = target.Distance2D(MyHero);
                    if (_ultAbility.IsInAbilityPhase)
                    {
                        if (distance > 400)
                        {
                            Log.Debug($"stopping ult since enemy too far!");
                            MyHero.Stop();
                            await Await.Delay(100, tk);
                        }
                        else
                        {
                            Log.Debug($"dist{distance}");
                            return;
                        }
                    }
                    if (distance > 50)
                    {
                        Log.Debug($"approaching target {distance}");
                        MyHero.Move(target.NetworkPosition);
                    }
                    else if (eulsModifier == null || eulsModifier.RemainingTime < _ultAbility.FindCastPoint())
                    {
                        Log.Debug($"{_ultAbility.IsInAbilityPhase}");
                        if (!MyHero.IsSilenced() && !_ultAbility.IsInAbilityPhase)
                        {
                            Log.Debug($"using ult on {target.Name}");
                            _ultAbility.UseAbility();
                            await Await.Delay(250, tk);
                        }
                    }
                    return;
                }
                else
                {
                    var shadowBlade = MyHero.GetItemById(ItemId.item_invis_sword) ??
                                      MyHero.GetItemById(ItemId.item_silver_edge);
                    var distance = MyHero.Distance2D(target);
                    if (shadowBlade != null && shadowBlade.IsAbilityEnabled() && shadowBlade.CanBeCasted() && distance < 6000)
                    {
                        Log.Debug($"using invis");
                        shadowBlade.UseAbility();
                        await Await.Delay(500, tk);
                        return;
                    }
                }
            }

            var euls = MyHero.GetItemById(ItemId.item_cyclone);
            if (euls != null && euls.IsAbilityEnabled() && euls.CanBeCasted(target) && _ultAbility.CanBeCasted(target))
            {
                if (euls.CanHit(target))
                {
                    Log.Debug($"using euls to disable enemy before stun");
                    euls.UseAbility(target);
                    await Await.Delay(125, tk);
                    return;
                }
                // check if we are near the enemy
                if (!await MoveOrBlinkToEnemy(target, tk, minimumRange: 0.1f, maximumRange: euls.GetCastRange()))
                {
                    Log.Debug($"return because of blink and euls ready");
                    return;
                }
            }


            await HasNoLinkens(target, tk);
            await UseItems(target, tk);

            if (!MyHero.IsSilenced() && _razeAbilities[0].IsAbilityEnabled())
            {
                foreach (var razeAbility in _razeAbilities)
                {
                    await UseRazeOnTarget(target, razeAbility);
                }
            }

            // make him disabled
            if (await DisableEnemy(target, tk) == DisabledState.UsedAbilityToDisable)
            {
                Log.Debug($"disabled!");
                // return;
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