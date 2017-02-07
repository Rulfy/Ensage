using Ensage;
using Ensage.Common.Extensions;

namespace Zaio.Helpers
{
    public static class MyUnitExtension
    {
        private static readonly string[] CantAttackModifiers =
        {
            "modifier_obsidian_destroyer_astral_imprisonment_prison",
            "modifier_abaddon_borrowed_time",
            "modifier_brewmaster_primal_split",
            "modifier_phoenix_supernova_hiding",
            "modifier_juggernaut_omnislash_invulnerability",
            "modifier_naga_siren_song_of_the_siren",
            "modifier_puck_phase_shift",
            "modifier_shadow_demon_disruption",
            "modifier_winter_wyvern_winters_curse_aura",
            "modifier_winter_wyvern_winters_curse",
            "modifier_storm_spirit_ball_lightning"
        };

        private static readonly string[] CantKillModifiers =
        {
            "modifier_dazzle_shallow_grave",
            "modifier_oracle_false_promise",
            "modifier_skeleton_king_reincarnation_scepter_active"
            // "modifier_slark_shadow_dance",
        };

        private static readonly string[] CantKillModifiersAxe =
        {
            "modifier_skeleton_king_reincarnation_scepter_active"
        };

        public static float MagicResistance(this Unit unit)
        {
            return unit.HasModifiers(new[] {"modifier_oracle_fates_edict", "modifier_medusa_stone_gaze_stone"}, false)
                ? 1.0f
                : unit.MagicDamageResist;
        }

        public static float PhysicalResistance(this Unit unit)
        {
            return
                unit.HasModifiers(
                    new[]
                    {
                        "modifier_winter_wyvern_cold_embrace",
                        "modifier_winter_wyvern_winters_curse_aura",
                        "modifier_winter_wyvern_winters_curse",
                        "modifier_omninight_guardian_angel"
                    }, false)
                    ? 1.0f
                    : unit.DamageResist;
        }

        public static bool CantBeKilled(this Unit unit)
        {
            return unit.HasModifiers(CantKillModifiers, false);
        }

        public static bool CantBeKilledByAxeUlt(this Unit unit)
        {
            return unit.HasModifiers(CantKillModifiersAxe, false);
        }

        public static bool CantBeAttacked(this Unit unit)
        {
            return unit.HasModifiers(CantAttackModifiers, false);
        }

        public static bool IsMuted(this Unit unit)
        {
            return unit.UnitState.HasFlag(UnitState.Muted);
        }

        public static bool IsAttacking(this Unit unit, Unit target)
        {
            return unit.FindRelativeAngle(target.NetworkPosition) < 0.22 &&
                   unit.UnitDistance2D(target) <= unit.GetAttackRange();
        }

        public static float UnitDistance2D(this Unit unit, Unit other)
        {
            return unit.Distance2D(other) - unit.HullRadius - other.HullRadius;
        }

        public static bool IsDisabled(this Unit unit)
        {
            return unit.IsStunned() || unit.IsHexed() || unit.IsRooted();
        }

        public static bool IsDisabled(this Unit unit, out float duration)
        {
            return unit.IsStunned(out duration) || unit.IsHexed(out duration) || unit.IsRooted(out duration);
        }
    }
}