using System;
using System.Collections.Generic;
using Ensage;
using Ensage.Common.Extensions;
using SharpDX;

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

        public static float GetIllusionRemainingTime(this Unit illusion)
        {
            if (!illusion.IsIllusion)
                return 0.0f;

            var mod = illusion.FindModifier("modifier_illusion");
            if (mod != null)
                return mod.RemainingTime;

            mod = illusion.FindModifier("modifier_manta");
            if (mod != null)
                return mod.RemainingTime;

            return 0; // TODO: check for more modifiers
        }

        public static float GetShortestDistance(this Unit unit, Vector3 start, Vector3 end)
        {
            var dir = end - start;
            var pos = unit.NetworkPosition;
            var closestPoint = (Vector3.Dot(pos - start, dir) / dir.LengthSquared()) * dir;
            var targetVec = (pos - start) - closestPoint;
            return targetVec.Length();
        }

        public static float GetShortestDistance(this Unit unit, List<Vector3> positions )
        {
            var result = float.MaxValue;
            for (var i = 0; i < positions.Count - 1; ++i )
            {
                result = Math.Min(result, unit.GetShortestDistance(positions[i], positions[i + 1]));
            }
            return result;
        }
    }
}