using Ape.Sounds;

namespace Ape.Game
{
    public readonly struct GameFeedbackRequest
    {
        public GameFeedbackRequest(GameFeedbackType type, Sound sound, float pitchMultiplier)
        {
            Type = type;
            Sound = sound;
            PitchMultiplier = pitchMultiplier;
        }

        public GameFeedbackType Type { get; }
        public Sound Sound { get; }
        public float PitchMultiplier { get; }

        public static GameFeedbackRequest CreateSound(Sound sound, float pitchMultiplier = 1f)
        {
            return new GameFeedbackRequest(GameFeedbackType.PlaySound, sound, pitchMultiplier);
        }

        public static GameFeedbackRequest CreateSpinStartShake()
        {
            return new GameFeedbackRequest(GameFeedbackType.SpinStartShake, null, 1f);
        }

        public static GameFeedbackRequest CreateBombShake()
        {
            return new GameFeedbackRequest(GameFeedbackType.BombShake, null, 1f);
        }
    }
}
