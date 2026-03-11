using System;

namespace Ape.Game
{
    public readonly struct GameSpinRevealPresentationRequest
    {
        public GameSpinRevealPresentationRequest(
            RouletteResolvedWheel nextWheel,
            int selectedSliceIndex,
            RouletteResolvedSlice selectedSlice,
            Action onCompleted)
        {
            NextWheel = nextWheel;
            SelectedSliceIndex = selectedSliceIndex;
            SelectedSlice = selectedSlice;
            OnCompleted = onCompleted;
        }

        public RouletteResolvedWheel NextWheel { get; }
        public int SelectedSliceIndex { get; }
        public RouletteResolvedSlice SelectedSlice { get; }
        public Action OnCompleted { get; }

        public void Complete()
        {
            OnCompleted?.Invoke();
        }
    }
}
