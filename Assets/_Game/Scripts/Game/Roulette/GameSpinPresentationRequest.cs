using System;

namespace Ape.Game
{
    public readonly struct GameSpinPresentationRequest
    {
        public GameSpinPresentationRequest(RouletteResolvedWheel wheel, int targetSliceIndex, Action onCompleted)
        {
            Wheel = wheel;
            TargetSliceIndex = targetSliceIndex;
            OnCompleted = onCompleted;
        }

        public RouletteResolvedWheel Wheel { get; }
        public int TargetSliceIndex { get; }
        public Action OnCompleted { get; }

        public void Complete()
        {
            OnCompleted?.Invoke();
        }
    }
}
