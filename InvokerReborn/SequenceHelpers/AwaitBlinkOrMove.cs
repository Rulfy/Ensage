namespace InvokerReborn.SequenceHelpers
{
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

    internal class AwaitBlinkOrMove : ISequenceEntry
    {
        private static readonly ILog Log = AssemblyLogs.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Blink blink;

        private readonly AwaitMoveToTarget move;

        public AwaitBlinkOrMove(Blink blink, AwaitMoveToTarget move)
        {
            this.blink = blink;
            this.move = move;
        }

        public AwaitBlinkOrMove(Hero me, Func<int> engageRange)
        {
            this.blink = new Blink(me, engageRange);
            this.move = new AwaitMoveToTarget(me, engageRange);
        }

        public async Task ExecuteAsync(Unit target, CancellationToken tk = default(CancellationToken))
        {
            Log.Debug($"EngageRange {this.blink.EngageRange}");
            if (!this.blink.IsSkilled || (this.blink.Ability.Cooldown > 0))
            {
                Log.Debug($"Moving to target since blink on cooldown or not bought yet");
                await this.move.ExecuteAsync(target, tk);
            }
            else
            {
                var distance = this.blink.Owner.Distance2D(target);
                if (distance <= (this.move.EngageRange * (1.0f + (InvokerMenu.MaxWalkDistance / 100.0f))))
                {
                    Log.Debug($"Moving to target {distance} vs {this.move.EngageRange * 1.1}");
                    await this.move.ExecuteAsync(target, tk);
                }
                else
                {
                    if (distance <= this.blink.EngageRange + InvokerMenu.SafeDistance)
                    {
                        Log.Debug(
                            $"Blinking to target {distance} vs {this.blink.EngageRange + InvokerMenu.SafeDistance} | {this.move.EngageRange * 1.1}");
                        await this.blink.ExecuteAsync(target, tk);
                    }
                    else
                    {
                        var tooFar = distance - (this.blink.EngageRange + InvokerMenu.SafeDistance) + 100;

                        var targetMove = target.NetworkPosition - this.blink.Owner.NetworkPosition;
                        targetMove.Normalize();
                        targetMove += tooFar;
                        targetMove = this.blink.Owner.NetworkPosition + targetMove;

                        Log.Debug($"Moving and the blinking {distance} | {tooFar}");

                        await
                            this.move.ExecuteAsync(targetMove, this.blink.EngageRange + InvokerMenu.SafeDistance, tk);
                        await this.blink.ExecuteAsync(target, tk);
                    }
                }
            }
        }
    }
}