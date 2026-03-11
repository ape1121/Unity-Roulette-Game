
using Ape.Sounds;
using UnityEngine;

namespace Ape.Data
{
    [CreateAssetMenu(fileName = "RoulettePresentationConfig", menuName = "CriticalShot/Roulette/Roulette Presentation Config", order = 3)]
    public sealed class RoulettePresentationConfig : ScriptableObject
    {
        [Header("Audio")]
        [SerializeField] private Sound _spinRewardSound;
        [SerializeField] private Sound _spinBombSound;
        [SerializeField] private Sound _spinStartSound;
        [SerializeField] private Sound _spinTickSound;
        [SerializeField] private Sound _spinSlowExcitementSound;
        [SerializeField] private Sound _spinStopSound;
        [SerializeField] private Sound _cashOutSound;
        [SerializeField] private Sound _yeetItemSound;
        [SerializeField] private Sound _replaceSmokeSound;

        [Header("Text")]
        [SerializeField] private string _bombDisplayName = "Bomb";

        public Sound SpinRewardSound => _spinRewardSound;
        public Sound SpinBombSound => _spinBombSound;
        public Sound SpinStartSound => _spinStartSound;
        public Sound SpinTickSound => _spinTickSound;
        public Sound SpinSlowExcitementSound => _spinSlowExcitementSound;
        public Sound SpinStopSound => _spinStopSound;
        public Sound CashOutSound => _cashOutSound;
        public Sound YeetItemSound => _yeetItemSound;
        public Sound ReplaceSmokeSound => _replaceSmokeSound;
        public string BombDisplayName => _bombDisplayName;
    }
}
