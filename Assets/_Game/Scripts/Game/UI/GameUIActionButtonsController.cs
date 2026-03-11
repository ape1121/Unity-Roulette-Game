using TMPro;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Ape.Game
{
    public sealed class GameUIActionButtonsController
    {
        private Button _spinButton;
        private Button _cashOutButton;
        private Button _continueButton;
        private Button _restartButton;
        private Button _inventoryButton;
        private TextMeshProUGUI _continueButtonLabel;

        public void Configure(
            Button spinButton,
            Button cashOutButton,
            Button continueButton,
            Button restartButton,
            Button inventoryButton,
            TextMeshProUGUI continueButtonLabel)
        {
            _spinButton = spinButton;
            _cashOutButton = cashOutButton;
            _continueButton = continueButton;
            _restartButton = restartButton;
            _inventoryButton = inventoryButton;
            _continueButtonLabel = continueButtonLabel;
        }

        public void Bind(
            UnityAction onSpinClicked,
            UnityAction onCashOutClicked,
            UnityAction onContinueClicked,
            UnityAction onRestartClicked,
            UnityAction onInventoryClicked)
        {
            Unbind(onSpinClicked, onCashOutClicked, onContinueClicked, onRestartClicked, onInventoryClicked);

            if (_spinButton != null)
                _spinButton.onClick.AddListener(onSpinClicked);

            if (_cashOutButton != null)
                _cashOutButton.onClick.AddListener(onCashOutClicked);

            if (_continueButton != null)
                _continueButton.onClick.AddListener(onContinueClicked);

            if (_restartButton != null)
                _restartButton.onClick.AddListener(onRestartClicked);

            if (_inventoryButton != null)
                _inventoryButton.onClick.AddListener(onInventoryClicked);
        }

        public void Unbind(
            UnityAction onSpinClicked,
            UnityAction onCashOutClicked,
            UnityAction onContinueClicked,
            UnityAction onRestartClicked,
            UnityAction onInventoryClicked)
        {
            if (_spinButton != null)
                _spinButton.onClick.RemoveListener(onSpinClicked);

            if (_cashOutButton != null)
                _cashOutButton.onClick.RemoveListener(onCashOutClicked);

            if (_continueButton != null)
                _continueButton.onClick.RemoveListener(onContinueClicked);

            if (_restartButton != null)
                _restartButton.onClick.RemoveListener(onRestartClicked);

            if (_inventoryButton != null)
                _inventoryButton.onClick.RemoveListener(onInventoryClicked);
        }

        public void ApplyState(
            bool canSpin,
            bool canCashOut,
            bool canContinue,
            bool canRestart,
            bool showContinueButton,
            string continueButtonLabel)
        {
            SetButtonInteractable(_spinButton, canSpin);
            SetButtonInteractable(_cashOutButton, canCashOut);
            SetButtonInteractable(_continueButton, canContinue);
            SetButtonInteractable(_restartButton, canRestart);
            SetButtonVisible(_continueButton, showContinueButton);
            SetText(_continueButtonLabel, continueButtonLabel);
        }

        private static void SetButtonInteractable(Button button, bool isInteractable)
        {
            if (button != null)
                button.interactable = isInteractable;
        }

        private static void SetButtonVisible(Button button, bool isVisible)
        {
            if (button != null)
                button.gameObject.SetActive(isVisible);
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
                text.text = value;
        }
    }
}
