using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ensage;
using Ensage.Common.Extensions;
using InvokerReborn.Interfaces;
using InvokerReborn.Items;
using log4net;
using PlaySharp.Toolkit.Logging;

namespace InvokerReborn.SequenceHelpers
{
    internal class AwaitBlinkOrMove : ISequenceEntry
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Blink _blink;
        private readonly AwaitMoveToTarget _move;

        public AwaitBlinkOrMove(Blink blink, AwaitMoveToTarget move)
        {
            _blink = blink;
            _move = move;
        }

        public AwaitBlinkOrMove(Hero me, Func<int> engageRange)
        {
            _blink = new Blink(me, engageRange);
            _move = new AwaitMoveToTarget(me, engageRange);
        }

        public async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            Log.Debug($"EngageRange {_blink.EngageRange}");
            if (!_blink.IsSkilled || (_blink.Ability.Cooldown > 0))
            {
                Log.Debug($"Moving to target since blink on cooldown or not bought yet");
                await _move.ExecuteAsync(target, tk);
            }
            else
            {
                var distance = _blink.Owner.Distance2D(target);
                if (distance <= _move.EngageRange*(1.0f + InvokerMenu.MaxWalkDistance/100.0f))
                {
                    Log.Debug($"Moving to target {distance} vs {_move.EngageRange*1.1}");
                    await _move.ExecuteAsync(target, tk);
                }
                else
                {
                    if (distance <= _blink.EngageRange + InvokerMenu.SafeDistance)
                    {
                        Log.Debug(
                            $"Blinking to target {distance} vs {_blink.EngageRange + InvokerMenu.SafeDistance} | {_move.EngageRange*1.1}");
                        await _blink.ExecuteAsync(target, tk);
                    }
                    else
                    {
                        var tooFar = distance - (_blink.EngageRange + InvokerMenu.SafeDistance) + 100;

                        var targetMove = target.NetworkPosition - _blink.Owner.NetworkPosition;
                        targetMove.Normalize();
                        targetMove += tooFar;
                        targetMove = _blink.Owner.NetworkPosition + targetMove;

                        Log.Debug($"Moving and the blinking {distance} | {tooFar}");

                        await _move.ExecuteAsync(targetMove, _blink.EngageRange + InvokerMenu.SafeDistance, tk);
                        await _blink.ExecuteAsync(target, tk);
                    }
                }
            }
        }
    }
}